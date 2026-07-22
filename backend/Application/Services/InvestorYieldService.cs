using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services
{
    public class InvestorYieldService
    {
        private readonly AppDbContext _context;
        private readonly LedgerService _ledgerService;

        public InvestorYieldService(AppDbContext context, LedgerService ledgerService = null)
        {
            _context = context;
            _ledgerService = ledgerService ?? new LedgerService(context);
        }

        public async Task<(int Processed, decimal TotalYield)> ProcessMonthlyYieldAsync(string usuarioResponsavel = "Sistema")
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal taxaRendimento = config?.TaxaRendimentoPool ?? 2.00m;

            if (taxaRendimento <= 0)
                return (0, 0);

            var investors = await _context.Users
                .Where(u => u.Role == "Investidor" && u.Status == "Aprovado")
                .Join(_context.InvestorWallets,
                      u => u.Id,
                      w => w.Id,
                      (u, w) => w)
                .ToListAsync();

            int processedCount = 0;
            decimal totalYieldPaid = 0;

            foreach (var wallet in investors)
            {
                decimal saldoTotalAplicado = wallet.SaldoDisponivel + wallet.SaldoInvestido;
                
                if (saldoTotalAplicado <= 0)
                    continue;

                decimal rendimento = Math.Round(saldoTotalAplicado * (taxaRendimento / 100.0m), 2);

                if (rendimento <= 0)
                    continue;

                decimal saldoAnterior = wallet.SaldoDisponivel;
                
                wallet.SaldoDisponivel += rendimento;
                wallet.Rendimentos += rendimento;
                wallet.DataAtualizacao = DateTime.UtcNow;

                var investorTx = new InvestorTransaction
                {
                    Id = Guid.NewGuid(),
                    InvestorId = wallet.Id,
                    Tipo = "RENDIMENTO_POOL",
                    Valor = rendimento,
                    SaldoAnterior = saldoAnterior,
                    SaldoPosterior = wallet.SaldoDisponivel,
                    DataMovimentacao = DateTime.UtcNow,
                    Descricao = $"Rendimento mensal do Pool de Liquidez ({taxaRendimento}% sobre {saldoTotalAplicado:C2}).",
                    UsuarioResponsavel = usuarioResponsavel
                };
                _context.InvestorTransactions.Add(investorTx);

                if (_ledgerService != null)
                {
                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.PlatformCash,
                        destination: LedgerAccount.InvestorWallet,
                        amount: rendimento,
                        type: LedgerTransactionType.RevenueDistribution,
                        referenceType: "Yield",
                        referenceId: investorTx.Id,
                        userId: wallet.Id,
                        observacao: $"Pagamento de rendimento mensal do pool para o investidor",
                        createdBy: usuarioResponsavel
                    );
                }

                processedCount++;
                totalYieldPaid += rendimento;
            }

            await _context.SaveChangesAsync();

            return (processedCount, totalYieldPaid);
        }

        public async Task<decimal> GetDailyYieldPreviewAsync(Guid investorId)
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal taxaRendimentoMensal = config?.TaxaRendimentoPool ?? 2.00m;

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == investorId);
            if (wallet == null) return 0;

            decimal saldoTotalAplicado = wallet.SaldoDisponivel + wallet.SaldoInvestido;
            if (saldoTotalAplicado <= 0) return 0;

            // Preview diário: Taxa Mensal / 30
            decimal taxaDiaria = taxaRendimentoMensal / 30.0m;
            decimal rendimentoDiario = Math.Round(saldoTotalAplicado * (taxaDiaria / 100.0m), 2);

            return rendimentoDiario;
        }
    }
}
