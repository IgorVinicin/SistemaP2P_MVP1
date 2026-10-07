using System.Data;
using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services.Asaas;

public class GatewayWorkflowService(AppDbContext context, IAsaasService gateway, ITreasuryService treasury)
{
    public async Task<GatewayCharge> CreateDepositAsync(Guid userId, decimal value, string? observation)
    {
        EnsureConfigured();
        var user = await context.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new ArgumentException("Investidor não encontrado.");
        if (user.Status != "Aprovado" || user.Role != "Investidor") throw new ArgumentException("Investidor ainda não aprovado.");
        if (!ValidationHelper.IsValidCpf(user.Profile?.CPF)) throw new ArgumentException("CPF válido obrigatório para emitir PIX.");
        var deposit = await treasury.RequestDepositAsync(userId, value, observation);
        var charge = new GatewayCharge { Kind = "Deposit", ReferenceId = deposit.Id, UserId = userId, Value = value };
        context.GatewayCharges.Add(charge);
        await context.SaveChangesAsync();
        return await IssueChargeAsync(charge, user, true);
    }

    public async Task<GatewayCharge> CreateInstallmentAsync(Guid installmentId, Guid userId, bool isAdmin, decimal? requestedValue)
    {
        EnsureConfigured();
        await using var tx = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var installment = await context.LoanInstallments.Include(i => i.LoanRequest).ThenInclude(l => l!.User).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(i => i.Id == installmentId) ?? throw new ArgumentException("Parcela não encontrada.");
        if (!isAdmin && installment.LoanRequest?.UserId != userId) throw new UnauthorizedAccessException("Acesso negado.");
        if (!isAdmin && (installment.LoanRequest?.User?.Role != "Cliente" || installment.LoanRequest.User.Status != "Aprovado"))
            throw new UnauthorizedAccessException("Tomador ainda não aprovado para emitir cobranças.");
        if (installment.LoanRequest?.Status is not ("EmPagamento" or "Ativo" or "Inadimplente")) throw new ArgumentException("Empréstimo ainda não possui desembolso confirmado.");
        if (installment.Status is "Paga" or "Cancelada") throw new ArgumentException("Parcela encerrada.");
        InstallmentPaymentService.EnsureRecognizedPaymentHistory(installment);
        var value = requestedValue ?? (installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago);
        var remaining = installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago;
        ValidateMoney(value);
        if (value > remaining) throw new ArgumentException("Valor superior ao saldo devedor.");
        var existing = await context.GatewayCharges.FirstOrDefaultAsync(c => c.Kind == "Installment" && c.ReferenceId == installmentId && c.IsActive);
        bool isNew = existing == null;
        var charge = existing ?? new GatewayCharge { Kind = "Installment", ReferenceId = installmentId, UserId = installment.LoanRequest!.UserId, Value = value };
        if (existing != null && charge.Value != value) throw new ArgumentException("Já existe cobrança PIX em aberto com outro valor. Utilize a cobrança existente.");
        if (isNew) context.GatewayCharges.Add(charge);
        await context.SaveChangesAsync();
        if (tx != null) { await tx.CommitAsync(); await tx.DisposeAsync(); }
        return await IssueChargeAsync(charge, installment.LoanRequest!.User!, isNew);
    }

    // No automatic repeat POST after an uncertain network outcome: reconcile by the persisted external reference.
    public async Task<GatewayCharge> IssueChargeAsync(GatewayCharge charge, User user, bool isNew = false)
    {
        EnsureConfigured();
        if (charge.SettledAt != null) return charge;
        if (charge.PaymentId == null)
        {
            AsaasPaymentResponseDto? payment;
            try
            {
                payment = isNew ? await gateway.CreatePaymentAsync(charge, user, DateTime.UtcNow.AddDays(1))
                    : await gateway.FindPaymentAsync($"CHARGE_{charge.Id}");
            }
            catch (HttpRequestException ex) when (isNew && DefinitelyRejected(ex))
            {
                charge.Status = "FAILED"; charge.IsActive = false;
                if (charge.Kind == "Deposit")
                {
                    var deposit = await context.DepositRequests.FindAsync(charge.ReferenceId);
                    if (deposit != null) { deposit.Status = "Failed"; deposit.ProcessedAt = DateTime.UtcNow; }
                }
                await context.SaveChangesAsync();
                throw;
            }
            if (payment == null) throw new InvalidOperationException("Emissão não confirmada. Concilie a referência no Asaas antes de emitir novamente.");
            if (string.IsNullOrWhiteSpace(payment.Id) || payment.Value != charge.Value || payment.ExternalReference != $"CHARGE_{charge.Id}")
                throw new InvalidOperationException("Cobrança retornada não corresponde à operação autorizada.");
            await context.Entry(charge).ReloadAsync();
            if (charge.PaymentId != null && charge.PaymentId != payment.Id) throw new InvalidOperationException("ID de cobrança divergente. Conciliação necessária.");
            charge.PaymentId = payment.Id;
            if (charge.Status == "Creating") charge.Status = "PENDING";
            if (charge.Kind == "Deposit")
            {
                var deposit = await context.DepositRequests.FindAsync(charge.ReferenceId);
                if (deposit != null)
                {
                    await context.Entry(deposit).ReloadAsync();
                    deposit.GatewayReference = payment.Id;
                    if (deposit.Status == "Pending") deposit.Status = "Processing";
                }
            }
            await context.SaveChangesAsync();
        }
        if (charge.Status is "CANCELLED" or "NeedsReview") throw new InvalidOperationException("Cobrança cancelada ou aguardando conciliação.");
        if (charge.SettledAt != null) return charge;
        if (string.IsNullOrWhiteSpace(charge.CopiaECola))
        {
            var details = await gateway.GetPixDetailsAsync(charge.PaymentId);
            if (details.Value != charge.Value || details.PaymentId != charge.PaymentId) throw new InvalidOperationException("PIX retornado não corresponde à cobrança.");
            charge.QrCodeBase64 = details.QrCodeBase64; charge.CopiaECola = details.CopiaECola;
            charge.InvoiceUrl = details.InvoiceUrl; charge.ExpirationDate = details.ExpirationDate;
            await context.SaveChangesAsync();
        }
        return charge;
    }

    public async Task<GatewayTransfer> SubmitWithdrawalAsync(Guid withdrawalId)
    {
        EnsureConfigured();
        await EnsureNoReviewAsync();
        await using var tx = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var withdrawal = await context.WithdrawalRequests.FindAsync(withdrawalId) ?? throw new ArgumentException("Saque não encontrado.");
        var existing = await context.GatewayTransfers.FirstOrDefaultAsync(t => t.Kind == "Withdrawal" && t.ReferenceId == withdrawalId);
        if (existing == null && withdrawal.Status != "Approved") throw new ArgumentException("Aprove o saque antes de enviar ao Asaas.");
        var transfer = existing ?? new GatewayTransfer { Kind = "Withdrawal", ReferenceId = withdrawalId, UserId = withdrawal.InvestorId,
            Value = withdrawal.Valor, PixKey = withdrawal.ChavePix ?? "", PixKeyType = withdrawal.TipoChavePix ?? "" };
        ValidatePix(transfer.PixKey, transfer.PixKeyType);
        if (existing == null) { context.GatewayTransfers.Add(transfer); withdrawal.Status = "Processing"; }
        await context.SaveChangesAsync();
        if (tx != null) { await tx.CommitAsync(); await tx.DisposeAsync(); }
        return await IssueTransferAsync(transfer, existing == null);
    }
    public async Task CancelChargeAsync(GatewayCharge charge)
    {
        if (charge.Status == "CANCELLED") return;
        if (charge.SettledAt != null) throw new InvalidOperationException("Cobrança já recebida não pode ser cancelada.");
        if (charge.PaymentId == null)
        {
            var remote = await gateway.FindPaymentAsync($"CHARGE_{charge.Id}");
            if (remote == null) throw new InvalidOperationException("Emissão ainda não conciliada. Verifique a referência no Asaas.");
            if (remote.Value != charge.Value || remote.ExternalReference != $"CHARGE_{charge.Id}") throw new InvalidOperationException("Cobrança divergente.");
            charge.PaymentId = remote.Id;
            await context.SaveChangesAsync();
        }
        await gateway.CancelPaymentAsync(charge.PaymentId);
        await context.Entry(charge).ReloadAsync();
        if (charge.SettledAt != null) throw new InvalidOperationException("Pagamento recebido durante cancelamento. Atualize a conciliação.");
        charge.Status = "CANCELLED"; charge.IsActive = false;
        if (charge.Kind == "Deposit")
        {
            var deposit = await context.DepositRequests.FindAsync(charge.ReferenceId);
            if (deposit != null) { await context.Entry(deposit).ReloadAsync(); deposit.Status = "Cancelled"; deposit.ProcessedAt = DateTime.UtcNow; }
        }
        await context.SaveChangesAsync();
    }

    public async Task<GatewayTransfer> SubmitLoanAsync(Guid loanId, string pixKey, string pixType, decimal confirmedValue, bool confirmedRecipient, string actor = "Administrador")
    {
        EnsureConfigured(); ValidatePix(pixKey, pixType);
        await EnsureNoReviewAsync();
        await using var tx = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var loan = await context.LoanRequests.Include(l => l.User).ThenInclude(u => u!.Profile).FirstOrDefaultAsync(l => l.Id == loanId)
            ?? throw new ArgumentException("Empréstimo não encontrado.");
        if (loan.User?.Status != "Aprovado" || loan.User.Role != "Cliente") throw new ArgumentException("Tomador não está aprovado para receber o empréstimo.");
        var borrowerCpf = new string((loan.User.Profile?.CPF ?? "").Where(char.IsDigit).ToArray());
        if (!ValidationHelper.IsValidCpf(borrowerCpf)) throw new ArgumentException("CPF cadastrado do tomador inválido. Corrija o cadastro antes de enviar.");
        if (AsaasService.NormalizePixKeyType(pixType) != "CPF" || new string(pixKey.Where(char.IsDigit).ToArray()) != borrowerCpf)
            throw new ArgumentException("O desembolso deve usar a chave CPF cadastrada do próprio tomador.");
        ValidateMoney(loan.ValorSolicitado);
        if (!confirmedRecipient || confirmedValue != loan.ValorSolicitado)
            throw new ArgumentException("Confirme o tomador, sua chave CPF e o valor aprovado atual antes de enviar.");
        var existing = await context.GatewayTransfers.FirstOrDefaultAsync(t => t.Kind == "Loan" && t.ReferenceId == loanId && t.IsActive);
        if (existing != null)
        {
            if (existing.Value != loan.ValorSolicitado || existing.UserId != loan.UserId || existing.PixKey != borrowerCpf || existing.PixKeyType != "CPF")
                throw new InvalidOperationException("Tentativa anterior diverge do cadastro atual. Concilie antes de enviar.");
            if (tx != null) { await tx.CommitAsync(); await tx.DisposeAsync(); }
            return await IssueTransferAsync(existing, false);
        }
        if (existing == null && loan.Status is not ("Aprovado" or "Financiado" or "FalhaDesembolso")) throw new ArgumentException("Empréstimo não está autorizado para desembolso.");
        if (await context.LedgerTransactions.AnyAsync(l => l.LoanId == loanId && l.TipoMovimentacao == LedgerTransactionType.LoanDisbursement && l.ContaDestino == LedgerAccount.BorrowerAccount))
            throw new ArgumentException("Empréstimo já possui registro de desembolso. Concilie os dados legados antes de transferir.");
        var investments = await context.Investments.Where(i => i.LoanId == loanId).ToListAsync();
        if (investments.Count > 0 && (investments.Sum(i => i.ValorInvestido) != loan.ValorSolicitado || investments.Any(i => i.Status != "Comprometido" || i.SaldoPrincipalPendente != i.ValorInvestido)))
            throw new ArgumentException("Reservas de investimento não cobrem o valor aprovado integralmente. Conciliação necessária.");
        foreach (var group in investments.GroupBy(i => i.InvestorId))
        {
            var wallet = await context.InvestorWallets.FindAsync(group.Key);
            if (wallet == null || wallet.SaldoComprometido < group.Sum(i => i.ValorInvestido)) throw new ArgumentException("Reserva de investimento inconsistente.");
        }
        if (!await context.LoanInstallments.AnyAsync(i => i.LoanRequestId == loanId && i.Status != "Cancelada") ||
            await context.LoanInstallments.AnyAsync(i => i.LoanRequestId == loanId && i.ValorPago > 0) ||
            await context.LoanInstallments.Where(i => i.LoanRequestId == loanId && i.Status != "Cancelada").SumAsync(i => i.ValorPrincipal) != loan.ValorSolicitado)
            throw new ArgumentException("Parcelas não correspondem ao contrato aprovado ou já possuem recebimentos. Conciliação necessária.");
        var transfer = existing ?? new GatewayTransfer { Kind = "Loan", ReferenceId = loanId, UserId = loan.UserId, Value = loan.ValorSolicitado,
            PixKey = borrowerCpf, PixKeyType = "CPF" };
        if (existing == null) { context.GatewayTransfers.Add(transfer); loan.Status = "DesembolsoPendente"; }
        context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = actor,
            EventoExecutado = "ENVIO_DESEMBOLSO_AUTORIZADO", LoanRequestId = loan.Id, Valor = transfer.Value,
            Observacao = $"Envio autorizado para {loan.User.Profile?.Nome}, chave CPF {borrowerCpf}, operação {transfer.Id}." });
        await context.SaveChangesAsync();
        if (tx != null) { await tx.CommitAsync(); await tx.DisposeAsync(); }
        return await IssueTransferAsync(transfer, existing == null);
    }

    private async Task<GatewayTransfer> IssueTransferAsync(GatewayTransfer transfer, bool isNew)
    {
        if (transfer.TransferId != null) return transfer;
        AsaasTransferResponseDto? remote;
        try { remote = isNew ? await gateway.CreateTransferAsync(transfer) : await gateway.FindTransferAsync($"TRANSFER_{transfer.Id}"); }
        catch (HttpRequestException ex) when (isNew && DefinitelyRejected(ex))
        {
            await using var tx = context.Database.IsRelational() ? await context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            transfer.Status = "FAILED"; transfer.IsActive = false;
            if (transfer.Kind == "Withdrawal")
            {
                var withdrawal = await context.WithdrawalRequests.FindAsync(transfer.ReferenceId);
                if (withdrawal != null)
                {
                    withdrawal.Status = "Failed";
                    await treasury.RejectWithdrawalAsync(withdrawal.Id, "Asaas", "Transferência recusada antes do processamento.");
                }
            }
            else
            {
                var loan = await context.LoanRequests.FindAsync(transfer.ReferenceId);
                if (loan != null) loan.Status = "FalhaDesembolso";
            }
            await context.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            throw;
        }
        if (remote == null) throw new InvalidOperationException("Envio não confirmado. Concilie a referência no Asaas antes de transferir novamente.");
        if (string.IsNullOrWhiteSpace(remote.Id) || remote.Value != transfer.Value || remote.ExternalReference != $"TRANSFER_{transfer.Id}")
            throw new InvalidOperationException("Transferência retornada não corresponde à operação autorizada.");
        await context.Entry(transfer).ReloadAsync();
        if (transfer.TransferId != null && transfer.TransferId != remote.Id) throw new InvalidOperationException("ID de transferência divergente. Conciliação necessária.");
        transfer.TransferId = remote.Id;
        if (transfer.Status == "Creating") transfer.Status = "PENDING";
        transfer.Fee ??= remote.TransferFee;
        if (transfer.Kind == "Withdrawal")
        {
            var withdrawal = await context.WithdrawalRequests.FindAsync(transfer.ReferenceId);
            if (withdrawal != null) { await context.Entry(withdrawal).ReloadAsync(); withdrawal.GatewayReference = remote.Id; }
        }
        await context.SaveChangesAsync();
        return transfer;
    }

    public void EnsureConfigured()
    {
        if (!gateway.IsConfigured) throw new InvalidOperationException("Gateway Asaas não configurado. Nenhuma movimentação financeira foi efetuada.");
    }
    private static bool DefinitelyRejected(HttpRequestException ex) => ex.StatusCode is System.Net.HttpStatusCode.BadRequest
        or System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.UnprocessableEntity;
    private async Task EnsureNoReviewAsync()
    {
        if (await context.GatewayWebhookEvents.AnyAsync(e => e.Status == "NeedsReview"))
            throw new InvalidOperationException("Saídas financeiras bloqueadas até conciliação das ocorrências do gateway.");
    }
    public static void ValidateMoney(decimal value)
    {
        if (value <= 0 || decimal.Round(value, 2) != value) throw new ArgumentException("Informe um valor positivo com no máximo duas casas decimais.");
    }
    public static void ValidatePix(string? key, string? type)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Chave PIX obrigatória.");
        AsaasService.NormalizePixKeyType(type);
    }
}
