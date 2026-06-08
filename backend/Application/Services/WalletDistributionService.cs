using System;
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

        /// <summary>
        /// Processa a distribuição de valores de uma parcela paga para os respectivos investidores.
        /// (Preparação para integração futura, ainda não movimenta dinheiro real/fictício nas carteiras).
        /// </summary>
        public async Task DistributeInstallmentPaymentAsync(Guid loanInstallmentId)
        {
            var installment = await _context.LoanInstallments
                .Include(i => i.LoanRequest)
                .FirstOrDefaultAsync(i => i.Id == loanInstallmentId);

            if (installment == null) return;

            // Estrutura de Integração Futura:
            // 1. Identificar investimentos associados ao LoanRequestId:
            //    var investments = await _context.Investments.Where(inv => inv.LoanId == installment.LoanRequestId).ToListAsync();
            // 2. Para cada investimento, calcular a fração de participação no empréstimo:
            //    fração = inv.ValorInvestido / loan.ValorSolicitado
            // 3. Distribuir a fração correspondente do ValorPrincipal e ValorJuros:
            //    - Creditar no saldo disponível e rendimentos da carteira do investidor.
            //    - Deduzir e creditar taxa da plataforma.
            // 4. Se houver atraso, distribuir multa/juros de mora proporcional ou destinar ao Fundo de Proteção:
            //    - Atualizar saldo do ProtectionFund.

            await Task.CompletedTask;
            Console.WriteLine($"[INTEGRAÇÃO CARTEIRA] Parcela {installment.NumeroParcela} do Empréstimo {installment.LoanRequestId} foi paga. Valor: {installment.ValorPago:C2}. Pronto para distribuição futura.");
        }
    }
}
