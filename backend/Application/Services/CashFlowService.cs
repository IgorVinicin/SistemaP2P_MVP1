using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class CashFlowService
    {
        private readonly AppDbContext _context;

        public CashFlowService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CashFlowSnapshot> GetSummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.LedgerTransactions.AsNoTracking().AsQueryable();

            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value.ToUniversalTime());
            if (endDate.HasValue)
            {
                var end = endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate <= end);
            }

            // Realizamos agrupamento no banco de dados para evitar carregamento em memória (Escalabilidade)
            
            // TotalEntradas = Tudo que tem Destino como PlatformCash
            var totalEntradas = await query
                .Where(t => t.ContaDestino == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // TotalSaidas = Tudo que tem Origem como PlatformCash
            var totalSaidas = await query
                .Where(t => t.ContaOrigem == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // TotalRecebidoParcelas = Onde Tipo = InstallmentPayment
            var totalRecebidoParcelas = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment && t.ContaDestino == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // ReceitaPlataforma = Entradas em PlatformRevenue (Origem: PlatformCash)
            var receitaPlataforma = await query
                .Where(t => t.ContaDestino == LedgerAccount.PlatformRevenue)
                .SumAsync(t => t.Valor);

            // TotalDistribuidoInvestidores = Saídas de PlatformCash para InvestorWallet 
            // do tipo RevenueDistribution
            var totalDistribuidoInvestidores = await query
                .Where(t => t.ContaDestino == LedgerAccount.InvestorWallet && t.ContaOrigem == LedgerAccount.PlatformCash && t.TipoMovimentacao == LedgerTransactionType.RevenueDistribution)
                .SumAsync(t => t.Valor);

            // TotalCobertoProtectionFund = Saídas do ProtectionFund para PlatformCash
            var totalCobertoProtectionFund = await query
                .Where(t => t.ContaOrigem == LedgerAccount.ProtectionFund && t.TipoMovimentacao == LedgerTransactionType.ProtectionFundDebit)
                .SumAsync(t => t.Valor);

            // TotalEmprestado = Desembolsos de Investidor para Tomador
            var totalEmprestado = await query
                .Where(t => t.ContaDestino == LedgerAccount.BorrowerAccount && t.TipoMovimentacao == LedgerTransactionType.LoanDisbursement)
                .SumAsync(t => t.Valor);

            var totalDepositosExternos = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InvestorDeposit)
                .SumAsync(t => t.Valor);

            var totalSaquesExternos = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InvestorWithdrawal)
                .SumAsync(t => t.Valor);

            return new CashFlowSnapshot
            {
                SnapshotDate = endDate ?? DateTime.UtcNow,
                TotalEntradas = totalEntradas,
                TotalSaidas = totalSaidas,
                SaldoOperacional = (totalEntradas + totalDepositosExternos) - (totalSaidas + totalSaquesExternos) + receitaPlataforma,
                ReceitaPlataforma = receitaPlataforma,
                TotalDistribuidoInvestidores = totalDistribuidoInvestidores,
                TotalCobertoProtectionFund = totalCobertoProtectionFund,
                TotalEmprestado = totalEmprestado,
                TotalRecebidoParcelas = totalRecebidoParcelas,
                TotalDepositosExternos = totalDepositosExternos,
                TotalSaquesExternos = totalSaquesExternos,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static DateTime ToBrasiliaDate(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return utc.AddHours(-3).Date;
        }

        public static (DateTime StartUtc, DateTime EndUtc) GetBrasiliaDayRangeUtc(DateTime brasiliaDate)
        {
            var dateOnly = brasiliaDate.Date;
            var startUtc = DateTime.SpecifyKind(dateOnly.AddHours(3), DateTimeKind.Utc);
            var endUtc = DateTime.SpecifyKind(dateOnly.AddDays(1).AddHours(3).AddTicks(-1), DateTimeKind.Utc);
            return (startUtc, endUtc);
        }

        public async Task<CashFlowSnapshot> GenerateSnapshotAsync(DateTime targetDate)
        {
            var brasiliaDate = ToBrasiliaDate(targetDate);
            var (startUtc, endUtc) = GetBrasiliaDayRangeUtc(brasiliaDate);
            var snapshotDateStored = DateTime.SpecifyKind(brasiliaDate, DateTimeKind.Utc);

            var existingSnapshot = await _context.CashFlowSnapshots
                .FirstOrDefaultAsync(s => s.SnapshotDate.Date == snapshotDateStored.Date);

            if (existingSnapshot != null)
                throw new InvalidOperationException($"Já existe um snapshot para a data {brasiliaDate:dd/MM/yyyy}");

            var summary = await GetSummaryAsync(startUtc, endUtc);
            
            summary.Id = Guid.NewGuid();
            summary.SnapshotDate = snapshotDateStored;
            summary.CreatedAt = DateTime.UtcNow;

            _context.CashFlowSnapshots.Add(summary);
            await _context.SaveChangesAsync();

            return summary;
        }

        public async Task<PagedResultDto<CashFlowSnapshot>> GetSnapshotsAsync(int page = 1, int pageSize = 20)
        {
            var query = _context.CashFlowSnapshots.AsNoTracking();

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(s => s.SnapshotDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<CashFlowSnapshot>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<PagedResultDto<CashFlowTransactionDto>> GetTransactionsAsync(
            string? period = "hoje",
            string? searchTerm = null,
            int? type = null,
            int page = 1,
            int pageSize = 20,
            DateTime? startDate = null,
            DateTime? endDate = null)
        {
            var query = _context.LedgerTransactions.AsNoTracking().AsQueryable();

            var now = DateTime.UtcNow;
            var today = now.Date;

            if (startDate.HasValue && endDate.HasValue)
            {
                var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate >= startDate.Value.Date && t.TransactionDate <= end);
            }
            else if (!string.IsNullOrEmpty(period))
            {
                switch (period.ToLower())
                {
                    case "hoje":
                        query = query.Where(t => t.TransactionDate >= today);
                        break;
                    case "ontem":
                        query = query.Where(t => t.TransactionDate >= today.AddDays(-1) && t.TransactionDate < today);
                        break;
                    case "7dias":
                        query = query.Where(t => t.TransactionDate >= today.AddDays(-7));
                        break;
                    case "30dias":
                        query = query.Where(t => t.TransactionDate >= today.AddDays(-30));
                        break;
                    case "mesatual":
                        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(t => t.TransactionDate >= startOfMonth);
                        break;
                }
            }

            // O Fluxo de caixa de transações só importa para Empréstimos e Pagamentos (nesta visão específica)
            if (type.HasValue)
            {
                var ledgerType = (LedgerTransactionType)type.Value;
                query = query.Where(t => t.TipoMovimentacao == ledgerType);
            }
            else
            {
                query = query.Where(t => t.TipoMovimentacao == LedgerTransactionType.LoanDisbursement || t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment);
            }

            var joinedQuery = from t in query
                              join lr in _context.LoanRequests.AsNoTracking() on t.LoanId equals lr.Id into loanGroup
                              from lr in loanGroup.DefaultIfEmpty()
                              
                              let borrowerId = lr != null ? (Guid?)lr.UserId : t.UserId
                              
                              let p = _context.UserProfiles.AsNoTracking().FirstOrDefault(prof => prof.Id == borrowerId)
                              let c = _context.CompanyProfiles.AsNoTracking().FirstOrDefault(comp => comp.Id == borrowerId)
                              
                              select new CashFlowTransactionDto
                              {
                                  Id = t.Id,
                                  TransactionDate = t.TransactionDate,
                                  TipoMovimentacao = (int)t.TipoMovimentacao,
                                  Valor = t.Valor,
                                  UserName = c != null && !string.IsNullOrEmpty(c.RazaoSocial) ? c.RazaoSocial :
                                             p != null && !string.IsNullOrEmpty(p.Nome) ? p.Nome : 
                                             "Desconhecido",
                                  CPF = p != null ? p.CPF : "",
                                  Observacao = t.Observacao
                              };

            // Filtro de Busca (Nome, Razão Social ou CPF)
            if (!string.IsNullOrEmpty(searchTerm))
            {
                var term = searchTerm.ToLower();
                joinedQuery = joinedQuery.Where(dto => dto.UserName!.ToLower().Contains(term) || (dto.CPF != null && dto.CPF.Contains(term)));
            }

            var totalCount = await joinedQuery.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await joinedQuery
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<CashFlowTransactionDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }

        public async Task<byte[]> GetTransactionsCsvAsync(string? period = "hoje", string? searchTerm = null, int? type = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            // Reutiliza a lógica de busca com limite alto para não sobrecarregar
            var result = await GetTransactionsAsync(period, searchTerm, type, 1, 10000, startDate, endDate);

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("DataHora;TipoMovimentacao;Valor;Tomador;CPF;Observacao");

            foreach (var item in result.Items)
            {
                var tipoDesc = item.TipoMovimentacao == 0 ? "Emprestimo" : (item.TipoMovimentacao == 1 ? "Pagamento Parcela" : item.TipoMovimentacao.ToString());
                
                sb.AppendLine($"{item.TransactionDate:yyyy-MM-dd HH:mm};{tipoDesc};{item.Valor};{item.UserName};{item.CPF};{item.Observacao}");
            }

            return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task<CashFlowClosingConferenceDto> GetClosingConferenceAsync(
            string period = "hoje",
            DateTime? startDate = null,
            DateTime? endDate = null,
            string? status = null,
            string? tipoEmprestimo = null,
            string? tomador = null)
        {
            DateTime start;
            DateTime end;
            var now = DateTime.UtcNow;

            if (startDate.HasValue && endDate.HasValue)
            {
                start = startDate.Value.Date;
                end = endDate.Value.Date.AddDays(1).AddTicks(-1);
            }
            else
            {
                switch (period.ToLower())
                {
                    case "ontem":
                        start = now.Date.AddDays(-1);
                        end = start.AddDays(1).AddTicks(-1);
                        break;
                    case "7dias":
                        start = now.Date.AddDays(-7);
                        end = now.Date.AddDays(1).AddTicks(-1);
                        break;
                    case "30dias":
                        start = now.Date.AddDays(-30);
                        end = now.Date.AddDays(1).AddTicks(-1);
                        break;
                    case "mesatual":
                        start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        end = now.Date.AddDays(1).AddTicks(-1);
                        break;
                    case "hoje":
                    default:
                        start = now.Date;
                        end = now.Date.AddDays(1).AddTicks(-1);
                        break;
                }
            }

            var query = _context.LoanInstallments
                .AsNoTracking()
                .Include(i => i.LoanRequest)
                .ThenInclude(l => l!.User)
                .ThenInclude(u => u!.Profile)
                .Where(i => (i.DataVencimento >= start && i.DataVencimento <= end) || 
                            (i.DataPagamento.HasValue && i.DataPagamento.Value >= start && i.DataPagamento.Value <= end))
                .AsQueryable();

            var list = await query.ToListAsync();

            var userIds = list.Where(i => i.LoanRequest?.UserId != null).Select(i => i.LoanRequest!.UserId).Distinct().ToList();
            var companies = await _context.CompanyProfiles.AsNoTracking()
                .Where(c => userIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c);

            var items = list.Select(i => {
                var lr = i.LoanRequest;
                var user = lr?.User;
                var profile = user?.Profile;
                var company = user != null && companies.TryGetValue(user.Id, out var comp) ? comp : null;

                string tomadorNome = company != null && !string.IsNullOrEmpty(company.RazaoSocial) ? company.RazaoSocial :
                                     profile != null && !string.IsNullOrEmpty(profile.Nome) ? profile.Nome :
                                     user?.Email ?? "Tomador";

                string tomadorCpf = profile?.CPF ?? company?.CNPJ ?? "";

                decimal totalDevido = i.ValorParcela + i.Multa + i.JurosMora;
                decimal valorPago = i.ValorPago;
                decimal valorRestante = Math.Max(0, totalDevido - valorPago);
                decimal percentualPago = totalDevido > 0 ? Math.Min(100, Math.Round((valorPago / totalDevido) * 100m, 2)) : 0;

                string statusCalc;
                if (i.Status == "Paga" || (totalDevido > 0 && valorPago >= totalDevido))
                {
                    statusCalc = "Paga";
                }
                else if (valorPago > 0 && valorPago < totalDevido)
                {
                    statusCalc = "Parcialmente Paga";
                }
                else
                {
                    statusCalc = "Em Débito";
                }

                return new CashFlowClosingItemDto
                {
                    InstallmentId = i.Id,
                    LoanRequestId = i.LoanRequestId,
                    TomadorNome = tomadorNome,
                    TomadorCpf = tomadorCpf,
                    TipoEmprestimo = lr?.TipoEmprestimo ?? "Master",
                    ValorParcela = i.ValorParcela,
                    Multa = i.Multa,
                    JurosMora = i.JurosMora,
                    ValorTotalDevido = totalDevido,
                    ValorPago = valorPago,
                    ValorRestante = valorRestante,
                    PercentualPago = percentualPago,
                    NumeroParcela = i.NumeroParcela,
                    QuantidadeTotalParcelas = lr?.QuantidadeParcelas ?? 0,
                    NumeroParcelaFormatado = $"{i.NumeroParcela}/{(lr?.QuantidadeParcelas > 0 ? lr.QuantidadeParcelas : 1)}",
                    DataVencimento = i.DataVencimento,
                    DataPagamento = i.DataPagamento,
                    StatusOriginal = i.Status,
                    StatusCalculado = statusCalc,
                    DiasAtraso = i.DiasAtraso
                };
            }).ToList();

            if (!string.IsNullOrWhiteSpace(tomador))
            {
                var term = tomador.ToLower();
                items = items.Where(x => x.TomadorNome.ToLower().Contains(term) || x.TomadorCpf.Contains(term)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(tipoEmprestimo))
            {
                items = items.Where(x => x.TipoEmprestimo.Equals(tipoEmprestimo, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status.Equals("paga", StringComparison.OrdinalIgnoreCase))
                {
                    items = items.Where(x => x.StatusCalculado == "Paga").ToList();
                }
                else if (status.Equals("parcial", StringComparison.OrdinalIgnoreCase) || status.Equals("parcialmente paga", StringComparison.OrdinalIgnoreCase))
                {
                    items = items.Where(x => x.StatusCalculado == "Parcialmente Paga").ToList();
                }
                else if (status.Equals("debito", StringComparison.OrdinalIgnoreCase) || status.Equals("em débito", StringComparison.OrdinalIgnoreCase))
                {
                    items = items.Where(x => x.StatusCalculado == "Em Débito").ToList();
                }
            }

            int totalPrevistas = items.Count;
            int totalPagas = items.Count(x => x.StatusCalculado == "Paga");
            int totalParciais = items.Count(x => x.StatusCalculado == "Parcialmente Paga");
            int totalEmDebito = items.Count(x => x.StatusCalculado == "Em Débito");

            decimal valorTotalRecebido = items.Sum(x => x.ValorPago);
            decimal valorAindaPendente = items.Sum(x => x.ValorRestante);

            decimal percentualAdimplencia = totalPrevistas > 0 
                ? Math.Round(((decimal)(totalPagas + totalParciais) / totalPrevistas) * 100m, 2)
                : 100m;

            return new CashFlowClosingConferenceDto
            {
                TotalParcelasPrevistas = totalPrevistas,
                TotalParcelasPagas = totalPagas,
                TotalParcelasParciais = totalParciais,
                TotalParcelasEmDebito = totalEmDebito,
                ValorTotalRecebido = valorTotalRecebido,
                ValorAindaPendente = valorAindaPendente,
                PercentualAdimplencia = percentualAdimplencia,
                Items = items
            };
        }
    }
}
