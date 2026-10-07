using System.Data;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services;

public class InstallmentPaymentService(AppDbContext context)
{
    public async Task<LoanInstallment> ApplyPaymentAsync(Guid installmentId, decimal amount, string actor = "Sistema", LedgerAccount paymentSource = LedgerAccount.BorrowerAccount)
    {
        await using var transaction = context.Database.IsRelational() && context.Database.CurrentTransaction == null
            ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var installment = await context.LoanInstallments.Include(i => i.LoanRequest).FirstOrDefaultAsync(i => i.Id == installmentId)
            ?? throw new InvalidOperationException("Parcela nao encontrada.");
        if (installment.Status is "Paga" or "Cancelada") throw new InvalidOperationException("Parcela encerrada.");
        if (installment.LoanRequest?.Status is not ("EmPagamento" or "Ativo" or "Inadimplente"))
            throw new InvalidOperationException("O emprestimo ainda nao possui desembolso confirmado.");
        EnsureRecognizedPaymentHistory(installment);
        decimal due = installment.ValorParcela + installment.Multa + installment.JurosMora;
        decimal remaining = due - installment.ValorPago;
        if (amount <= 0 || decimal.Round(amount, 2) != amount || amount > remaining)
            throw new InvalidOperationException("Valor recebido invalido ou superior ao saldo devedor.");
        var next = amount < remaining
            ? await context.LoanInstallments.Where(i => i.LoanRequestId == installment.LoanRequestId && i.NumeroParcela > installment.NumeroParcela && i.Status != "Paga" && i.Status != "Cancelada").OrderBy(i => i.NumeroParcela).FirstOrDefaultAsync() : null;
        if (next != null) EnsureRecognizedPaymentHistory(next);
        if (next != null && await context.GatewayCharges.AnyAsync(c => c.Kind == "Installment" && c.ReferenceId == next.Id && c.IsActive))
            throw new InvalidOperationException("Cancele a cobranca PIX da proxima parcela antes de transferir o saldo remanescente.");

        var components = new[] {
            Math.Max(0, installment.ValorPrincipal - installment.PrincipalPago),
            Math.Max(0, installment.ValorJuros - installment.JurosPagos),
            Math.Max(0, installment.Multa - installment.MultaPaga),
            Math.Max(0, installment.JurosMora - installment.JurosMoraPagos)
        };
        if (components.Sum() != remaining) throw new InvalidOperationException("Componentes da parcela divergentes. Necessaria conciliacao.");
        var payment = Split(amount, components);
        installment.PrincipalPago += payment[0]; installment.JurosPagos += payment[1];
        installment.MultaPaga += payment[2]; installment.JurosMoraPagos += payment[3];
        installment.ValorPago += amount;
        installment.DataPagamento = DateTime.UtcNow;
        installment.UpdatedAt = DateTime.UtcNow;
        installment.Status = amount == remaining ? "Paga" : "Parcial";
        context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = actor, EventoExecutado = "PAGAMENTO_PARCELA", LoanRequestId = installment.LoanRequestId, Valor = amount, Observacao = $"Recebimento confirmado da parcela {installment.NumeroParcela}. Saldo restante: {remaining - amount:C2}." });
        await new WalletDistributionService(context).DistributeInstallmentPaymentAsync(installment.Id, actor, paymentSource);
        if (next != null)
        {
            var rollover = new DebtRollover {
                SourceInstallmentId = installment.Id, TargetInstallmentId = next.Id,
                Principal = installment.ValorPrincipal - installment.PrincipalPago,
                Interest = installment.ValorJuros - installment.JurosPagos,
                Penalty = installment.Multa - installment.MultaPaga,
                LateInterest = installment.JurosMora - installment.JurosMoraPagos,
                Status = "Applied", AppliedAt = DateTime.UtcNow, Actor = actor
            };
            context.DebtRollovers.Add(rollover);
            next.ValorPrincipal += rollover.Principal;
            next.ValorJuros += rollover.Interest;
            next.ValorParcela += rollover.Principal + rollover.Interest;
            next.Multa += rollover.Penalty;
            next.JurosMora += rollover.LateInterest;
            next.MultaCarregada += rollover.Penalty;
            next.JurosMoraCarregados += rollover.LateInterest;
            next.UpdatedAt = DateTime.UtcNow;
            installment.ValorPrincipal = installment.PrincipalPago;
            installment.ValorJuros = installment.JurosPagos;
            installment.ValorParcela = installment.PrincipalPago + installment.JurosPagos;
            installment.Multa = installment.MultaPaga;
            installment.JurosMora = installment.JurosMoraPagos;
            installment.Status = "Paga";
            context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = actor, EventoExecutado = "ROLAGEM_SALDO_PARCELA", LoanRequestId = installment.LoanRequestId, Valor = rollover.Principal + rollover.Interest + rollover.Penalty + rollover.LateInterest, Observacao = $"Saldo transferido da parcela {installment.NumeroParcela} para {next.NumeroParcela}; principal {rollover.Principal:C2}, juros {rollover.Interest:C2}, multa {rollover.Penalty:C2}, mora {rollover.LateInterest:C2}." });
        }
        await context.SaveChangesAsync();
        await new DelinquencyService(context).UpdateLoanStatusFromInstallmentsAsync(installment.LoanRequestId, actor);
        if (transaction != null) await transaction.CommitAsync();
        return installment;
    }

    public static void EnsureRecognizedPaymentHistory(LoanInstallment installment)
    {
        if (installment.ValorPago <= 0) return;
        var paid = new[] { installment.PrincipalPago, installment.JurosPagos, installment.MultaPaga, installment.JurosMoraPagos };
        var due = new[] { installment.ValorPrincipal, installment.ValorJuros, installment.Multa, installment.JurosMora };
        if (paid.Sum() != installment.ValorPago || paid.Where((component, index) => component < 0 || component > due[index]).Any() ||
            installment.PrincipalDistribuido != installment.PrincipalPago)
            throw new InvalidOperationException("Necessária conciliação histórica: os componentes pagos e o principal distribuído desta parcela não estão comprovados.");
    }

    private static decimal[] Split(decimal amount, decimal[] components)
    {
        decimal total = components.Sum();
        if (amount == total) return components.ToArray();
        var exact = components.Select(c => amount * c / total).ToArray();
        var result = exact.Select(c => Math.Floor(c * 100m) / 100m).ToArray();
        int cents = (int)Math.Round((amount - result.Sum()) * 100m);
        foreach (var index in Enumerable.Range(0, components.Length).OrderByDescending(i => exact[i] - result[i]).ThenBy(i => i).Take(cents)) result[index] += 0.01m;
        return result;
    }
}
