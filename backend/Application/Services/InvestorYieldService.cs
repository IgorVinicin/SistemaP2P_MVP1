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

        public InvestorYieldService(AppDbContext context, LedgerService? ledgerService = null)
        {
            _context = context;
            _ledgerService = ledgerService ?? new LedgerService(context);
        }

        public async Task<(int Processed, decimal TotalYield)> ProcessMonthlyYieldAsync(string usuarioResponsavel = "Sistema")
        {
            var now = DateTime.UtcNow;
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);

            // Verifica se já existe transação de rendimento nesse mês
            var existingTx = await _context.InvestorTransactions
                .AnyAsync(t => t.Tipo == "RENDIMENTO_POOL" && t.DataMovimentacao >= startOfMonth && t.DataMovimentacao <= endOfMonth);

            if (existingTx)
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

        public async Task<(int Processed, decimal TotalYield)> ProcessDailyYieldAsync(string usuarioResponsavel = "Sistema", DateTime? referenceDate = null)
        {
            var targetDate = (referenceDate ?? DateTime.UtcNow).Date;
            var startOfDay = new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, 0, 0, 0, DateTimeKind.Utc);
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

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
                // Evita duplicidade no mesmo dia
                var alreadyProcessed = await _context.InvestorTransactions
                    .AnyAsync(t => t.InvestorId == wallet.Id && 
                                   t.Tipo == "RENDIMENTO_DIARIO" && 
                                   t.DataMovimentacao >= startOfDay && 
                                   t.DataMovimentacao <= endOfDay);

                if (alreadyProcessed) continue;

                decimal taxaDisponivel = wallet.RentabilidadeSaldoDisponivel > 0 ? wallet.RentabilidadeSaldoDisponivel : 1.00m;
                
                // Cálculo exato diário proporcional (taxa mensal / 30 dias):
                decimal taxaDiaria = (taxaDisponivel / 30.0m) / 100.0m;
                decimal rendimentoDiario = Math.Round(wallet.SaldoDisponivel * taxaDiaria, 2, MidpointRounding.AwayFromZero);

                if (rendimentoDiario <= 0) continue;

                decimal saldoAnterior = wallet.SaldoDisponivel;
                wallet.SaldoDisponivel += rendimentoDiario;
                wallet.Rendimentos += rendimentoDiario;
                wallet.DataAtualizacao = DateTime.UtcNow;

                var investorTx = new InvestorTransaction
                {
                    Id = Guid.NewGuid(),
                    InvestorId = wallet.Id,
                    Tipo = "RENDIMENTO_DIARIO",
                    Valor = rendimentoDiario,
                    SaldoAnterior = saldoAnterior,
                    SaldoPosterior = wallet.SaldoDisponivel,
                    DataMovimentacao = DateTime.UtcNow,
                    Descricao = $"Rendimento diário creditado sobre o saldo disponível ({taxaDisponivel:N2}% a.m. proporcional = R$ {rendimentoDiario:N2}).",
                    UsuarioResponsavel = usuarioResponsavel
                };
                _context.InvestorTransactions.Add(investorTx);

                if (_ledgerService != null)
                {
                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.PlatformCash,
                        destination: LedgerAccount.InvestorWallet,
                        amount: rendimentoDiario,
                        type: LedgerTransactionType.RevenueDistribution,
                        referenceType: "DailyYield",
                        referenceId: investorTx.Id,
                        userId: wallet.Id,
                        observacao: $"Crédito de rendimento diário sobre saldo disponível",
                        createdBy: usuarioResponsavel
                    );
                }

                processedCount++;
                totalYieldPaid += rendimentoDiario;
            }

            if (processedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return (processedCount, totalYieldPaid);
        }

        public async Task<decimal> GetDailyYieldPreviewAsync(Guid investorId)
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal taxaBase = config?.TaxaRendimentoPool ?? 2.00m;

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == investorId);
            if (wallet == null) return 0;

            decimal taxaDisponivel = wallet.RentabilidadeSaldoDisponivel > 0 ? wallet.RentabilidadeSaldoDisponivel : taxaBase;
            decimal taxaComprometido = wallet.RentabilidadeSaldoComprometido > 0 
                ? wallet.RentabilidadeSaldoComprometido 
                : (wallet.Rentabilidade > 0 ? wallet.Rentabilidade : taxaBase);

            decimal rendimentoDiarioDisponivel = Math.Round(wallet.SaldoDisponivel * ((taxaDisponivel / 30.0m) / 100.0m), 2, MidpointRounding.AwayFromZero);
            decimal saldoComprometidoTotal = wallet.SaldoInvestido + wallet.SaldoComprometido;
            decimal rendimentoDiarioComprometido = Math.Round(saldoComprometidoTotal * ((taxaComprometido / 30.0m) / 100.0m), 2, MidpointRounding.AwayFromZero);

            return rendimentoDiarioDisponivel + rendimentoDiarioComprometido;
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

            bool jaDistribuido = existingTx != null;
            DateTime? dataDist = existingTx?.DataMovimentacao;
            string? userResp = existingTx?.UsuarioResponsavel;

            decimal taxaParaPreview = taxaRendimento > 0 ? taxaRendimento : 2.00m;

            var previewDto = new YieldPreviewDto
            {
                TaxaRendimento = taxaRendimento,
                TaxaEfetiva = taxaParaPreview,
                TaxaNaoConfigurada = taxaRendimento <= 0,
                JaDistribuidoEsteMes = jaDistribuido,
                DataDistribuicao = dataDist,
                UsuarioResponsavel = userResp
            };

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
                decimal taxaComp = item.Wallet.RentabilidadeSaldoComprometido > 0 ? item.Wallet.RentabilidadeSaldoComprometido : taxaParaPreview;

                decimal rendDisp = Math.Round(item.Wallet.SaldoDisponivel * (taxaDisp / 100.0m), 2, MidpointRounding.AwayFromZero);
                decimal rendComp = Math.Round((item.Wallet.SaldoInvestido + item.Wallet.SaldoComprometido) * (taxaComp / 100.0m), 2, MidpointRounding.AwayFromZero);
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
