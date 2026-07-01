using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services
{
    public class WalletDistributionService
    {
        private readonly AppDbContext _context;
        private readonly PlatformRevenueService _revenueService;
        private readonly LedgerService _ledgerService;

        public WalletDistributionService(AppDbContext context, PlatformRevenueService revenueService = null, LedgerService ledgerService = null)
        {
            _context = context;
            _revenueService = revenueService ?? new PlatformRevenueService(context);
            _ledgerService = ledgerService ?? new LedgerService(context);
        }

        public async Task DistributeInstallmentPaymentAsync(Guid loanInstallmentId, string usuarioResponsavel = "Sistema")
        {
            var installment = await _context.LoanInstallments
                .Include(i => i.LoanRequest)
                .FirstOrDefaultAsync(i => i.Id == loanInstallmentId);

            if (installment == null || installment.LoanRequest == null) return;

            var loan = installment.LoanRequest;

            // Find all investments for this loan
            var investments = await _context.Investments
                .Where(inv => inv.LoanId == loan.Id)
                .ToListAsync();

            if (!investments.Any()) return;

            // Registrar entrada principal da parcela no Ledger: BorrowerAccount -> PlatformCash
            if (_ledgerService != null && installment.ValorPago > 0)
            {
                await _ledgerService.AddTransactionAsync(
                    source: LedgerAccount.BorrowerAccount,
                    destination: LedgerAccount.PlatformCash,
                    amount: installment.ValorPago,
                    type: LedgerTransactionType.InstallmentPayment,
                    referenceType: "Installment",
                    referenceId: installment.Id,
                    loanId: loan.Id,
                    installmentId: installment.Id,
                    userId: loan.UserId,
                    observacao: $"Pagamento da parcela {installment.NumeroParcela} pelo tomador",
                    createdBy: usuarioResponsavel
                );
            }

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    decimal totalPrincipalPaid = 0;
                    decimal totalInterestPaid = 0;

                    foreach (var inv in investments)
                    {
                        var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                        if (wallet == null) continue;

                        // If investment is still committed, move to active invested balance
                        if (inv.Status == "Comprometido")
                        {
                            wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                            wallet.SaldoInvestido += inv.ValorInvestido;
                            inv.Status = "Ativo";
                        }

                        // Calculate participation fraction
                        decimal fraction = inv.ValorInvestido / loan.ValorSolicitado;

                        // Proportional principal paid
                        decimal valorPrincipalRecebido = Math.Round(installment.ValorPrincipal * fraction, 2);

                        // Proportional interest scaled by investor rentabilidade, capped at loan interest rate to prevent creating money out of thin air
                        decimal valorJurosRecebido = 0;
                        if (loan.TaxaJuros > 0)
                        {
                            decimal rentabilidadeFator = Math.Min(1.0m, wallet.Rentabilidade / loan.TaxaJuros);
                            valorJurosRecebido = Math.Round(installment.ValorJuros * fraction * rentabilidadeFator, 2);
                        }
                        else
                        {
                            valorJurosRecebido = Math.Round(installment.ValorJuros * fraction, 2);
                        }

                        decimal valorRecebido = valorPrincipalRecebido + valorJurosRecebido;

                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += valorRecebido;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - valorPrincipalRecebido);
                        inv.SaldoPrincipalPendente = Math.Max(0, inv.SaldoPrincipalPendente - valorPrincipalRecebido);
                        wallet.Rendimentos += valorJurosRecebido;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        totalPrincipalPaid += valorPrincipalRecebido;
                        totalInterestPaid += valorJurosRecebido;

                        // Record Investor Transaction
                        var investorTx = new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "RECEBIMENTO_PARCELA",
                            Valor = valorRecebido,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Recebimento de parcela {installment.NumeroParcela} do empréstimo {loan.Id} (Principal: {valorPrincipalRecebido:C2}, Rendimento: {valorJurosRecebido:C2}).",
                            UsuarioResponsavel = usuarioResponsavel
                        };
                        _context.InvestorTransactions.Add(investorTx);

                        // Add to financial history event
                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = usuarioResponsavel,
                            EventoExecutado = "RENDIMENTO_PAGO",
                            Observacao = $"Investidor {inv.InvestorId} recebeu {valorRecebido:C2} da parcela {installment.NumeroParcela} (Principal: {valorPrincipalRecebido:C2}, Rendimento: {valorJurosRecebido:C2}).",
                            LoanRequestId = loan.Id,
                            Valor = valorRecebido
                        });

                        // Registrar no Ledger: PlatformCash -> InvestorWallet
                        if (_ledgerService != null && valorRecebido > 0)
                        {
                            await _ledgerService.AddTransactionAsync(
                                source: LedgerAccount.PlatformCash,
                                destination: LedgerAccount.InvestorWallet,
                                amount: valorRecebido,
                                type: LedgerTransactionType.RevenueDistribution,
                                referenceType: "Installment",
                                referenceId: installment.Id,
                                loanId: loan.Id,
                                installmentId: installment.Id,
                                userId: inv.InvestorId,
                                observacao: $"Repasse da parcela {installment.NumeroParcela} para o investidor",
                                createdBy: usuarioResponsavel
                            );
                        }
                    }

                    // Platform spread & fines go to PlatformRevenue
                    decimal totalFinesAndLateInterest = installment.Multa + installment.JurosMora;
                    decimal remainingAmount = installment.ValorPago - totalPrincipalPaid - totalInterestPaid - totalFinesAndLateInterest;
                    
                    // Registrar multas e juros de atraso
                    if (totalFinesAndLateInterest > 0 && _revenueService != null)
                    {
                        await _revenueService.RegisterRevenueAsync(
                            PlatformRevenueType.MultaAtraso, 
                            totalFinesAndLateInterest, 
                            loan.Id, 
                            installment.Id, 
                            loan.UserId, 
                            $"Multas e juros de atraso da parcela {installment.NumeroParcela}", 
                            usuarioResponsavel);

                        if (_ledgerService != null)
                        {
                            await _ledgerService.AddTransactionAsync(
                                source: LedgerAccount.PlatformCash,
                                destination: LedgerAccount.PlatformRevenue,
                                amount: totalFinesAndLateInterest,
                                type: LedgerTransactionType.RevenueDistribution,
                                referenceType: "Installment",
                                referenceId: installment.Id,
                                loanId: loan.Id,
                                installmentId: installment.Id,
                                userId: loan.UserId,
                                observacao: $"Multas e juros de atraso repassados da parcela {installment.NumeroParcela}",
                                createdBy: usuarioResponsavel
                            );
                        }
                    }

                    // Registrar spread da plataforma
                    if (remainingAmount > 0 && _revenueService != null)
                    {
                        await _revenueService.RegisterRevenueAsync(
                            PlatformRevenueType.TarifaOperacional, 
                            remainingAmount, 
                            loan.Id, 
                            installment.Id, 
                            loan.UserId, 
                            $"Spread retido da parcela {installment.NumeroParcela}", 
                            usuarioResponsavel);
                            
                        if (_ledgerService != null)
                        {
                            await _ledgerService.AddTransactionAsync(
                                source: LedgerAccount.PlatformCash,
                                destination: LedgerAccount.PlatformRevenue,
                                amount: remainingAmount,
                                type: LedgerTransactionType.RevenueDistribution,
                                referenceType: "Installment",
                                referenceId: installment.Id,
                                loanId: loan.Id,
                                installmentId: installment.Id,
                                userId: loan.UserId,
                                observacao: $"Spread da plataforma repassado da parcela {installment.NumeroParcela}",
                                createdBy: usuarioResponsavel
                            );
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    
                    // Sucesso, quebra o loop de retry
                    break;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    if (attempt == maxRetries)
                    {
                        throw new Exception("Falha ao distribuir pagamentos devido a intensa concorrência de acesso. Tente novamente mais tarde.");
                    }
                    
                    // Recarrega as carteiras alteradas para a versão do banco
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.Entity is InvestorWallet))
                    {
                        await entry.ReloadAsync();
                    }
                    await Task.Delay(100);
                }
                catch (Exception)
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
}
