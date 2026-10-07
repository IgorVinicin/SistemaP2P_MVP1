using System.Data;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services;

public class WalletDistributionService
{
    private readonly AppDbContext _context;
    public WalletDistributionService(AppDbContext context, PlatformRevenueService? revenueService = null, LedgerService? ledgerService = null) => _context = context;

    public async Task DistributeInstallmentPaymentAsync(Guid loanInstallmentId, string usuarioResponsavel = "Sistema", LedgerAccount paymentSource = LedgerAccount.BorrowerAccount)
    {
        await using var transaction = _context.Database.IsRelational() && _context.Database.CurrentTransaction == null
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
        var installment = await _context.LoanInstallments.Include(i => i.LoanRequest).FirstOrDefaultAsync(i => i.Id == loanInstallmentId);
        if (installment?.LoanRequest == null || installment.ValorPago <= 0) return;
        var loan = installment.LoanRequest;
        decimal due = installment.ValorParcela + installment.Multa + installment.JurosMora;
        if (due <= 0 || loan.ValorSolicitado <= 0 || installment.ValorPago > due)
            throw new InvalidOperationException("Pagamento incompatível com os valores do contrato.");
        var entries = await _context.LedgerTransactions.Where(t => t.InstallmentId == installment.Id).ToListAsync();
        decimal accounted = entries.Where(t => t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment || t.TipoMovimentacao == LedgerTransactionType.ProtectionFundDebit).Sum(t => t.Valor);
        decimal received = installment.ValorPago - accounted;
        if (received <= 0) return;
        var investments = await _context.Investments.Where(i => i.LoanId == loan.Id).OrderBy(i => i.Id).ToListAsync();
        if (investments.Sum(i => i.ValorInvestido) > loan.ValorSolicitado)
            throw new InvalidOperationException("Capital investido excede o principal do empréstimo.");
        decimal principalTarget = installment.PrincipalPago + installment.JurosPagos + installment.MultaPaga + installment.JurosMoraPagos > 0
            ? installment.PrincipalPago : Math.Round(installment.ValorPrincipal * installment.ValorPago / due, 2, MidpointRounding.AwayFromZero);
        decimal previousPrincipal = installment.PrincipalDistribuido;
        if (previousPrincipal == 0 && accounted > 0) previousPrincipal = Math.Round(installment.ValorPrincipal * accounted / due, 2, MidpointRounding.AwayFromZero);
        decimal principalBudget = Math.Min(received, Math.Max(0, principalTarget - previousPrincipal));
        if (paymentSource is not (LedgerAccount.BorrowerAccount or LedgerAccount.ProtectionFund or LedgerAccount.ExternalGateway))
            throw new InvalidOperationException("Origem de pagamento inválida.");
        AddLedger(paymentSource, LedgerAccount.PlatformCash, received,
            paymentSource == LedgerAccount.ProtectionFund ? LedgerTransactionType.ProtectionFundDebit : LedgerTransactionType.InstallmentPayment, loan.UserId);
        var shares = investments.GroupBy(i => i.InvestorId).Select(g => new {
            InvestorId = g.Key,
            Exact = principalTarget * g.Sum(i => i.ValorInvestido) / loan.ValorSolicitado
        }).ToList();
        var targets = shares.ToDictionary(s => s.InvestorId, s => Math.Floor(s.Exact * 100m) / 100m);
        decimal fundedTarget = Math.Round(principalTarget * investments.Sum(i => i.ValorInvestido) / loan.ValorSolicitado, 2, MidpointRounding.AwayFromZero);
        int cents = (int)Math.Round((fundedTarget - targets.Values.Sum()) * 100m);
        foreach (var share in shares.OrderByDescending(s => s.Exact - targets[s.InvestorId]).ThenBy(s => s.InvestorId).Take(cents))
            targets[share.InvestorId] += 0.01m;
        decimal contractualPrincipal = await _context.LoanInstallments.Where(i => i.LoanRequestId == loan.Id && i.Status != "Cancelada").SumAsync(i => i.ValorPrincipal);
        bool finalPayment = contractualPrincipal == loan.ValorSolicitado && installment.ValorPago == due && principalBudget >= investments.Sum(i => i.SaldoPrincipalPendente) && !await _context.LoanInstallments.AnyAsync(i =>
            i.LoanRequestId == loan.Id && i.Id != installment.Id && i.Status != "Paga" && i.Status != "Cancelada");
        decimal allocated = 0;
        foreach (var inv in investments)
        {
            decimal prior = entries.Where(t => t.ContaDestino == LedgerAccount.InvestorWallet && t.UserId == inv.InvestorId).Sum(t => t.Valor);
            // Allocate by investor rather than investment so multiple positions cannot duplicate prior credits.
            var firstForInvestor = investments.First(i => i.InvestorId == inv.InvestorId);
            if (firstForInvestor.Id != inv.Id) continue;
            var positions = investments.Where(i => i.InvestorId == inv.InvestorId).ToList();
            decimal target = targets[inv.InvestorId];
            if (finalPayment) target = prior + positions.Sum(i => i.SaldoPrincipalPendente);
            decimal amount = Math.Min(Math.Max(0, target - prior), Math.Min(principalBudget - allocated, positions.Sum(i => i.SaldoPrincipalPendente)));
            if (amount <= 0) continue;
            var wallet = await _context.InvestorWallets.FindAsync(inv.InvestorId)
                ?? throw new InvalidOperationException("Carteira do investidor não encontrada.");
            foreach (var position in positions.Where(i => i.Status == "Comprometido"))
            {
                if (wallet.SaldoComprometido < position.ValorInvestido) throw new InvalidOperationException("Saldo comprometido inconsistente.");
                wallet.SaldoComprometido -= position.ValorInvestido;
                wallet.SaldoInvestido += position.ValorInvestido;
                position.Status = "Ativo";
            }
            if (wallet.SaldoInvestido < amount) throw new InvalidOperationException("Saldo investido inconsistente.");
            decimal old = wallet.SaldoDisponivel;
            wallet.SaldoDisponivel += amount;
            wallet.SaldoInvestido -= amount;
            wallet.DataAtualizacao = DateTime.UtcNow;
            decimal remaining = amount;
            foreach (var position in positions)
            {
                decimal part = Math.Min(remaining, position.SaldoPrincipalPendente);
                position.SaldoPrincipalPendente -= part;
                remaining -= part;
            }
            allocated += amount;
            _context.InvestorTransactions.Add(new InvestorTransaction { Id = Guid.NewGuid(), InvestorId = inv.InvestorId, Tipo = "RECEBIMENTO_PARCELA", Valor = amount, SaldoAnterior = old, SaldoPosterior = wallet.SaldoDisponivel, DataMovimentacao = DateTime.UtcNow, Descricao = $"Retorno de capital da parcela {installment.NumeroParcela}", UsuarioResponsavel = usuarioResponsavel });
            _context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = usuarioResponsavel, EventoExecutado = "RETORNO_CAPITAL_POOL", LoanRequestId = loan.Id, Valor = amount, Observacao = $"Retorno de principal para {inv.InvestorId}" });
            AddLedger(LedgerAccount.PlatformCash, LedgerAccount.InvestorWallet, amount, LedgerTransactionType.RevenueDistribution, inv.InvestorId);
        }
        // Principal financed by the platform and cent rounding stay in cash; only earned interest is revenue.
        decimal revenueTarget = installment.ValorPago - principalTarget;
        decimal previousRevenue = entries.Where(t => t.ContaDestino == LedgerAccount.PlatformRevenue).Sum(t => t.Valor);
        decimal revenue = Math.Min(received - allocated, Math.Max(0, revenueTarget - previousRevenue));
        if (revenue > 0)
        {
            _context.PlatformRevenues.Add(new PlatformRevenue { Id = Guid.NewGuid(), DataCriacao = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, TipoReceita = PlatformRevenueType.TarifaOperacional, Valor = revenue, LoanId = loan.Id, InstallmentId = installment.Id, UserId = loan.UserId, CreatedBy = usuarioResponsavel, Observacao = "Juros e encargos efetivamente recebidos" });
            AddLedger(LedgerAccount.PlatformCash, LedgerAccount.PlatformRevenue, revenue, LedgerTransactionType.RevenueDistribution, loan.UserId);
        }
        installment.PrincipalDistribuido = principalTarget;
        await _context.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();

        void AddLedger(LedgerAccount source, LedgerAccount destination, decimal amount, LedgerTransactionType type, Guid userId)
        {
            _context.LedgerTransactions.Add(new LedgerTransaction { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, TransactionDate = DateTime.UtcNow, TipoMovimentacao = type, ContaOrigem = source, ContaDestino = destination, Valor = amount, ReferenceType = "Installment", ReferenceId = installment.Id, LoanId = loan.Id, InstallmentId = installment.Id, UserId = userId, CreatedBy = usuarioResponsavel });
        }
    }
}
