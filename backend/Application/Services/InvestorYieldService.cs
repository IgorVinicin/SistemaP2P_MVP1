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
        private const string MonthlyCloseEvent = "FECHAMENTO_RENDIMENTO_MENSAL";
        private readonly AppDbContext _context;

        public InvestorYieldService(AppDbContext context)
        {
            _context = context;
        }

        public Task<(int Processed, decimal TotalYield)> ProcessMonthlyYieldAsync(string usuarioResponsavel = "Sistema")
            => ExecuteAtomicAsync(() => ProcessMonthlyYieldCoreAsync(usuarioResponsavel));

        private async Task<(int Processed, decimal TotalYield)> ExecuteAtomicAsync(Func<Task<(int Processed, decimal TotalYield)>> action)
        {
            if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction != null)
                return await action();
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                var result = await action();
                await transaction.CommitAsync();
                return result;
            }
            catch
            {
                await transaction.RollbackAsync();
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private void AddYieldLedger(InvestorTransaction investorTx, string referenceType, string usuarioResponsavel)
        {
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, TransactionDate = investorTx.DataMovimentacao,
                ContaOrigem = LedgerAccount.PlatformCash, ContaDestino = LedgerAccount.InvestorWallet,
                Valor = investorTx.Valor, TipoMovimentacao = LedgerTransactionType.RevenueDistribution,
                ReferenceType = referenceType, ReferenceId = investorTx.Id, UserId = investorTx.InvestorId,
                Observacao = investorTx.Descricao, CreatedBy = usuarioResponsavel
            });
        }

        private async Task<(int Processed, decimal TotalYield)> ProcessMonthlyYieldCoreAsync(string usuarioResponsavel)
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

            // Verifica se já existe transação de rendimento nesse mês
            var existingTx = await _context.InvestorTransactions
                .AnyAsync(t => t.Tipo == "RENDIMENTO_POOL" && t.DataMovimentacao >= startOfMonth && t.DataMovimentacao <= endOfMonth);

            if (existingTx || await _context.FinancialEventHistories.AnyAsync(e =>
                e.EventoExecutado == MonthlyCloseEvent && e.Data >= startOfMonth && e.Data < startOfMonth.AddMonths(1)))
            {
                throw new InvalidOperationException("A distribuição de rendimentos deste mês já foi processada anteriormente.");
            }

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
                decimal taxaDisponivel = wallet.RentabilidadeSaldoDisponivel > 0 ? wallet.RentabilidadeSaldoDisponivel : taxaRendimento;
                decimal taxaComprometido = wallet.RentabilidadeSaldoComprometido > 0 
                    ? wallet.RentabilidadeSaldoComprometido 
                    : (wallet.Rentabilidade > 0 ? wallet.Rentabilidade : taxaRendimento);

                decimal rendimentoDisponivel = Math.Round(wallet.SaldoDisponivel * (taxaDisponivel / 100.0m), 2, MidpointRounding.AwayFromZero);
                decimal saldoComprometidoTotal = wallet.SaldoInvestido + wallet.SaldoComprometido;
                decimal rendimentoComprometido = Math.Round(saldoComprometidoTotal * (taxaComprometido / 100.0m), 2, MidpointRounding.AwayFromZero);

                // Only real legacy credits are deducted; the informational estimate creates no transaction.
                var dailyAdvances = await _context.InvestorTransactions
                    .Where(t => t.InvestorId == wallet.Id && t.Tipo == "RENDIMENTO_DIARIO" &&
                        t.DataMovimentacao >= startOfMonth && t.DataMovimentacao <= endOfMonth)
                    .SumAsync(t => t.Valor);
                decimal advancesOnAvailable = Math.Min(rendimentoDisponivel, dailyAdvances);
                rendimentoDisponivel -= advancesOnAvailable;
                rendimentoComprometido = Math.Max(0, rendimentoComprometido - (dailyAdvances - advancesOnAvailable));
                decimal rendimento = rendimentoDisponivel + rendimentoComprometido;

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
                    Descricao = $"Rendimento mensal (Disponível: {taxaDisponivel:N2}% = {rendimentoDisponivel:C2} | Comprometido: {taxaComprometido:N2}% = {rendimentoComprometido:C2}).",
                    UsuarioResponsavel = usuarioResponsavel
                };
                _context.InvestorTransactions.Add(investorTx);

                AddYieldLedger(investorTx, "Yield", usuarioResponsavel);

                processedCount++;
                totalYieldPaid += rendimento;
            }

            // Persist the close even when advances already cover the entire entitlement.
            _context.FinancialEventHistories.Add(new FinancialEventHistory
            {
                Id = Guid.NewGuid(), Data = now, EventoExecutado = MonthlyCloseEvent,
                UsuarioResponsavel = usuarioResponsavel, Valor = totalYieldPaid,
                Observacao = "Fechamento mensal de rendimentos; apenas créditos diários reais de versões anteriores descontados."
            });
            await _context.SaveChangesAsync();

            return (processedCount, totalYieldPaid);
        }

        public async Task<decimal> GetDailyYieldPreviewAsync(Guid investorId)
        {
            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == investorId);
            if (wallet == null) return 0;
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal fallbackRate = Math.Max(0, config?.TaxaRendimentoPool ?? 2.00m);
            if (fallbackRate <= 0) return 0;
            decimal taxaDisponivel = wallet.RentabilidadeSaldoDisponivel > 0 ? wallet.RentabilidadeSaldoDisponivel : fallbackRate;
            decimal taxaComprometido = wallet.RentabilidadeSaldoComprometido > 0 ? wallet.RentabilidadeSaldoComprometido :
                (wallet.Rentabilidade > 0 ? wallet.Rentabilidade : fallbackRate);
            // Informational estimate only: no wallet movement, transaction or ledger entry.
            return Math.Round((Math.Max(0, wallet.SaldoDisponivel) * taxaDisponivel +
                Math.Max(0, wallet.SaldoInvestido + wallet.SaldoComprometido) * taxaComprometido) / 3000m,
                2, MidpointRounding.AwayFromZero);
        }

        public async Task<YieldPreviewDto> GetMonthlyYieldPreviewAsync(DateTime targetDate)
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal taxaRendimento = config?.TaxaRendimentoPool ?? 2.00m;

            var startOfMonth = new DateTime(targetDate.Year, targetDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

            var existingTx = await _context.InvestorTransactions
                .Where(t => t.Tipo == "RENDIMENTO_POOL" && t.DataMovimentacao >= startOfMonth && t.DataMovimentacao <= endOfMonth)
                .OrderByDescending(t => t.DataMovimentacao)
                .FirstOrDefaultAsync();

            var closing = await _context.FinancialEventHistories
                .Where(e => e.EventoExecutado == MonthlyCloseEvent && e.Data >= startOfMonth && e.Data < startOfMonth.AddMonths(1))
                .OrderByDescending(e => e.Data).FirstOrDefaultAsync();
            bool jaDistribuido = existingTx != null || closing != null;
            DateTime? dataDist = closing?.Data ?? existingTx?.DataMovimentacao;
            string? userResp = closing?.UsuarioResponsavel ?? existingTx?.UsuarioResponsavel;

            decimal taxaParaPreview = Math.Max(0, taxaRendimento);

            var previewDto = new YieldPreviewDto
            {
                TaxaRendimento = taxaRendimento,
                TaxaEfetiva = taxaParaPreview,
                TaxaNaoConfigurada = taxaRendimento <= 0,
                JaDistribuidoEsteMes = jaDistribuido,
                DataDistribuicao = dataDist,
                UsuarioResponsavel = userResp
            };
            if (taxaRendimento <= 0) return previewDto;

            var investors = await _context.Users
                .Where(u => u.Role == "Investidor" && u.Status == "Aprovado")
                .Join(_context.InvestorWallets,
                      u => u.Id,
                      w => w.Id,
                      (u, w) => new { User = u, Wallet = w })
                .ToListAsync();

            decimal totalEstimado = 0;

            foreach (var item in investors)
            {
                decimal saldoTotal = item.Wallet.SaldoDisponivel + item.Wallet.SaldoInvestido + item.Wallet.SaldoComprometido;
                if (saldoTotal <= 0) continue;

                var profile = await _context.UserProfiles.FindAsync(item.User.Id);
                string nome = profile?.Nome ?? item.User.Email ?? "Desconhecido";
                string cpf = profile?.CPF ?? "-";

                decimal taxaDisp = item.Wallet.RentabilidadeSaldoDisponivel > 0 ? item.Wallet.RentabilidadeSaldoDisponivel : taxaParaPreview;
                decimal taxaComp = item.Wallet.RentabilidadeSaldoComprometido > 0 ? item.Wallet.RentabilidadeSaldoComprometido :
                    (item.Wallet.Rentabilidade > 0 ? item.Wallet.Rentabilidade : taxaParaPreview);

                decimal rendDisp = Math.Round(item.Wallet.SaldoDisponivel * (taxaDisp / 100.0m), 2, MidpointRounding.AwayFromZero);
                decimal rendComp = Math.Round((item.Wallet.SaldoInvestido + item.Wallet.SaldoComprometido) * (taxaComp / 100.0m), 2, MidpointRounding.AwayFromZero);
                var dailyAdvances = await _context.InvestorTransactions
                    .Where(t => t.InvestorId == item.User.Id && t.Tipo == "RENDIMENTO_DIARIO" &&
                        t.DataMovimentacao >= startOfMonth && t.DataMovimentacao <= endOfMonth)
                    .SumAsync(t => t.Valor);
                decimal advancesOnAvailable = Math.Min(rendDisp, dailyAdvances);
                rendDisp -= advancesOnAvailable;
                rendComp = Math.Max(0, rendComp - (dailyAdvances - advancesOnAvailable));
                decimal rendimento = rendDisp + rendComp;

                previewDto.Itens.Add(new YieldPreviewItemDto
                {
                    InvestorId = item.User.Id,
                    Nome = nome,
                    CPF = cpf,
                    SaldoDisponivel = item.Wallet.SaldoDisponivel,
                    SaldoInvestido = item.Wallet.SaldoInvestido + item.Wallet.SaldoComprometido,
                    SaldoTotal = saldoTotal,
                    RendimentoEstimado = rendimento
                });

                totalEstimado += rendimento;
            }

            previewDto.TotalInvestidores = previewDto.Itens.Count;
            previewDto.TotalRendimentoEstimado = totalEstimado;

            return previewDto;
        }
    }

    public class YieldPreviewDto
    {
        public decimal TaxaRendimento { get; set; }
        public decimal TaxaEfetiva { get; set; }
        public bool TaxaNaoConfigurada { get; set; }
        public int TotalInvestidores { get; set; }
        public decimal TotalRendimentoEstimado { get; set; }
        public bool JaDistribuidoEsteMes { get; set; }
        public DateTime? DataDistribuicao { get; set; }
        public string? UsuarioResponsavel { get; set; }
        public System.Collections.Generic.List<YieldPreviewItemDto> Itens { get; set; } = new();
    }

    public class YieldPreviewItemDto
    {
        public Guid InvestorId { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public decimal SaldoDisponivel { get; set; }
        public decimal SaldoInvestido { get; set; }
        public decimal SaldoTotal { get; set; }
        public decimal RendimentoEstimado { get; set; }
    }
}
