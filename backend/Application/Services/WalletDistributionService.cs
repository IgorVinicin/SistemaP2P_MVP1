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

        public WalletDistributionService(AppDbContext context)
        {
            _context = context;
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

                // Proportional interest scaled by investor rentabilidade
                decimal valorJurosRecebido = 0;
                if (loan.TaxaJuros > 0)
                {
                    valorJurosRecebido = Math.Round(installment.ValorJuros * fraction * (wallet.Rentabilidade / loan.TaxaJuros), 2);
                }
                else
                {
                    valorJurosRecebido = Math.Round(installment.ValorJuros * fraction, 2);
                }

                decimal valorRecebido = valorPrincipalRecebido + valorJurosRecebido;

                decimal saldoAnterior = wallet.SaldoDisponivel;
                wallet.SaldoDisponivel += valorRecebido;
                wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - valorPrincipalRecebido);
                wallet.Rendimentos += valorJurosRecebido;
                wallet.DataAtualizacao = DateTime.UtcNow;

                totalPrincipalPaid += valorPrincipalRecebido;
                totalInterestPaid += valorJurosRecebido;

                // Record Investor Transaction
                var transaction = new InvestorTransaction
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
                _context.InvestorTransactions.Add(transaction);

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
            }

            // Platform spread & fines go to ProtectionFund
            decimal remainingAmount = installment.ValorPago - totalPrincipalPaid - totalInterestPaid;
            if (remainingAmount > 0)
            {
                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund != null)
                {
                    fund.SaldoAtual += remainingAmount;
                    fund.TotalRecebido += remainingAmount;
                    fund.UltimaAtualizacao = DateTime.UtcNow;

                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = usuarioResponsavel,
                        EventoExecutado = "RECEBIMENTO_TAXA_PLATAFORMA",
                        Observacao = $"Fundo de proteção recebeu {remainingAmount:C2} (spread/multas) da parcela {installment.NumeroParcela} do empréstimo {loan.Id}.",
                        LoanRequestId = loan.Id,
                        Valor = remainingAmount
                    });
                }
            }

            await _context.SaveChangesAsync();
        }
    }
}
