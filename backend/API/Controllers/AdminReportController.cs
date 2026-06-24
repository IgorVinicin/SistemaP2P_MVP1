using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/reports")]
    [Authorize(Roles = "Administrador")]
    public class AdminReportController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminReportController(AppDbContext context)
        {
            _context = context;
        }

        // 1. Relatório Consolidado de Tomadores
        [HttpGet("consolidated/borrowers")]
        public async Task<IActionResult> GetBorrowersConsolidated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? nome = null,
            [FromQuery] string? cpf = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Profile)
                    .Include(u => u.LoanRequests)
                    .Where(u => u.Role == "Cliente")
                    .AsNoTracking()
                    .AsQueryable();

                // Filtros de busca
                if (!string.IsNullOrWhiteSpace(nome))
                {
                    var term = nome.Trim().ToLower();
                    query = query.Where(u => u.Profile != null && u.Profile.Nome.ToLower().Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    var term = cpf.Trim().Replace(".", "").Replace("-", "");
                    query = query.Where(u => u.Profile != null && u.Profile.CPF.Replace(".", "").Replace("-", "").Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(u => u.Status == status);
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(u => u.CreatedAt >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(u => u.CreatedAt <= periodoFim.Value.ToUniversalTime());
                }

                // Estatísticas Gerais (Consolidado) - não filtradas pela busca de paginação, mas filtradas pelo período se informado
                var statsQuery = _context.Users.Where(u => u.Role == "Cliente").AsQueryable();
                if (periodoInicio.HasValue) statsQuery = statsQuery.Where(u => u.CreatedAt >= periodoInicio.Value.ToUniversalTime());
                if (periodoFim.HasValue) statsQuery = statsQuery.Where(u => u.CreatedAt <= periodoFim.Value.ToUniversalTime());

                var summary = new BorrowerConsolidatedSummaryDto
                {
                    TotalTomadores = await statsQuery.CountAsync(),
                    Ativos = await statsQuery.CountAsync(u => u.Status == "Aprovado"),
                    Inativos = await statsQuery.CountAsync(u => u.Status == "Reprovado" || u.Status == "Inativo"),
                    EmAnalise = await statsQuery.CountAsync(u => u.Status == "EmAnalise" || u.Status == "Pendente")
                };

                // Paginação
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var users = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Carregar as regras de crédito ativas em memória para calcular limite disponível
                var rules = await _context.ScoreCreditRules.AsNoTracking().Where(r => r.Ativo).ToListAsync();

                var items = new List<BorrowerConsolidatedItemDto>();
                foreach (var u in users)
                {
                    int score = u.Profile?.ScoreAtual ?? 0;
                    var rule = rules.FirstOrDefault(r => score >= r.ScoreMin && score <= r.ScoreMax);
                    decimal limiteMaximo = rule?.LimiteEmprestimo ?? 0;

                    // Empréstimos ativos concorrentes
                    var activeLoans = u.LoanRequests
                        .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmPagamento" || l.Status == "Inadimplente")
                        .ToList();

                    decimal limiteUtilizado = activeLoans.Sum(l => l.ValorSolicitado);
                    decimal limiteDisponivel = Math.Max(0, limiteMaximo - limiteUtilizado);

                    items.Add(new BorrowerConsolidatedItemDto
                    {
                        Id = u.Id,
                        Nome = u.Profile?.Nome ?? "Sem Nome",
                        CPF = u.Profile?.CPF ?? string.Empty,
                        LimiteDisponivel = limiteDisponivel,
                        QuantidadeEmprestimos = u.LoanRequests.Count,
                        ValorTotalSolicitado = u.LoanRequests.Sum(l => l.ValorSolicitado),
                        ValorTotalAprovado = u.LoanRequests
                            .Where(l => l.Status != "Pendente" && l.Status != "EmAnalise" && l.Status != "Reprovado")
                            .Sum(l => l.ValorSolicitado)
                    });
                }

                var result = new BorrowerConsolidatedReportDto
                {
                    Summary = summary,
                    Details = new PagedResultDto<BorrowerConsolidatedItemDto>
                    {
                        Items = items,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = totalPages
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 2. Relatório Consolidado de Investidores
        [HttpGet("consolidated/investors")]
        public async Task<IActionResult> GetInvestorsConsolidated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? nome = null,
            [FromQuery] string? cpf = null,
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null)
        {
            try
            {
                var query = _context.Users
                    .Include(u => u.Profile)
                    .Include(u => u.Wallet)
                    .Where(u => u.Role == "Investidor")
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(nome))
                {
                    var term = nome.Trim().ToLower();
                    query = query.Where(u => u.Profile != null && u.Profile.Nome.ToLower().Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(cpf))
                {
                    var term = cpf.Trim().Replace(".", "").Replace("-", "");
                    query = query.Where(u => u.Profile != null && u.Profile.CPF.Replace(".", "").Replace("-", "").Contains(term));
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(u => u.CreatedAt >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(u => u.CreatedAt <= periodoFim.Value.ToUniversalTime());
                }

                // Estatísticas Gerais (Consolidado)
                var statsQuery = _context.Users.Where(u => u.Role == "Investidor").AsQueryable();
                if (periodoInicio.HasValue) statsQuery = statsQuery.Where(u => u.CreatedAt >= periodoInicio.Value.ToUniversalTime());
                if (periodoFim.HasValue) statsQuery = statsQuery.Where(u => u.CreatedAt <= periodoFim.Value.ToUniversalTime());

                var summary = new InvestorConsolidatedSummaryDto
                {
                    TotalInvestidores = await statsQuery.CountAsync(),
                    Ativos = await statsQuery.CountAsync(u => u.Status == "Aprovado"),
                    EmAnalise = await statsQuery.CountAsync(u => u.Status == "EmAnalise" || u.Status == "Pendente")
                };

                // Paginação
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var list = await query
                    .OrderByDescending(u => u.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new InvestorConsolidatedItemDto
                    {
                        Id = u.Id,
                        Nome = u.Profile != null ? u.Profile.Nome : "Sem Nome",
                        CPF = u.Profile != null ? u.Profile.CPF : string.Empty,
                        ValorInvestido = u.Wallet != null ? u.Wallet.SaldoInvestido : 0,
                        Rentabilidade = u.Wallet != null ? u.Wallet.Rentabilidade : 0,
                        SaldoAtual = u.Wallet != null ? (u.Wallet.SaldoDisponivel + u.Wallet.SaldoInvestido) : 0
                    })
                    .ToListAsync();

                var result = new InvestorConsolidatedReportDto
                {
                    Summary = summary,
                    Details = new PagedResultDto<InvestorConsolidatedItemDto>
                    {
                        Items = list,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = totalPages
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 3. Relatório Consolidado de Empréstimos
        [HttpGet("consolidated/loans")]
        public async Task<IActionResult> GetLoansConsolidated(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? tipo = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null)
        {
            try
            {
                var query = _context.LoanRequests
                    .Include(l => l.User)
                    .ThenInclude(u => u!.Profile)
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(tipo))
                {
                    query = query.Where(l => l.TipoEmprestimo == tipo);
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(l => l.Status == status);
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(l => l.DataCriacao >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(l => l.DataCriacao <= periodoFim.Value.ToUniversalTime());
                }

                // Estatísticas Gerais (Consolidado)
                var statsQuery = _context.LoanRequests.AsQueryable();
                if (periodoInicio.HasValue) statsQuery = statsQuery.Where(l => l.DataCriacao >= periodoInicio.Value.ToUniversalTime());
                if (periodoFim.HasValue) statsQuery = statsQuery.Where(l => l.DataCriacao <= periodoFim.Value.ToUniversalTime());

                var summary = new LoanConsolidatedSummaryDto
                {
                    Solicitados = await statsQuery.CountAsync(),
                    Aprovados = await statsQuery.CountAsync(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmPagamento" || l.Status == "Inadimplente" || l.Status == "Quitado" || l.Status == "FinalizadoPorInadimplencia"),
                    Reprovados = await statsQuery.CountAsync(l => l.Status == "Reprovado"),
                    EmAnalise = await statsQuery.CountAsync(l => l.Status == "Pendente" || l.Status == "EmAnalise")
                };

                // Paginação
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var list = await query
                    .OrderByDescending(l => l.DataCriacao)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(l => new LoanConsolidatedItemDto
                    {
                        Id = l.Id,
                        Tomador = l.User != null && l.User.Profile != null ? l.User.Profile.Nome : "Sem Nome",
                        TipoEmprestimo = l.TipoEmprestimo ?? "Pendente",
                        Valor = l.ValorSolicitado,
                        Juros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                        Parcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                        Status = l.Status,
                        DataCriacao = l.DataCriacao
                    })
                    .ToListAsync();

                var result = new LoanConsolidatedReportDto
                {
                    Summary = summary,
                    Details = new PagedResultDto<LoanConsolidatedItemDto>
                    {
                        Items = list,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = totalPages
                    }
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 4. Relatório Financeiro Consolidado
        [HttpGet("consolidated/financial")]
        public async Task<IActionResult> GetFinancialConsolidated(
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null)
        {
            try
            {
                var start = periodoInicio?.ToUniversalTime() ?? DateTime.MinValue;
                var end = periodoFim?.ToUniversalTime() ?? DateTime.MaxValue;

                // 1. ENTRADAS
                // Parcelas pagas e juros recebidos
                var paidQuery = _context.LoanInstallments
                    .Where(i => (i.Status == "Paga" || i.Status == "Parcial") && i.DataPagamento != null)
                    .AsQueryable();

                if (periodoInicio.HasValue) paidQuery = paidQuery.Where(i => i.DataPagamento >= start);
                if (periodoFim.HasValue) paidQuery = paidQuery.Where(i => i.DataPagamento <= end);

                var paidInstallments = await paidQuery
                    .Select(i => new { i.Status, i.ValorPrincipal, i.ValorParcela, i.Multa, i.JurosMora, i.ValorPago })
                    .ToListAsync();

                decimal parcelasRecebidasPrincipal = 0;
                decimal jurosRecebidos = 0;

                foreach (var inst in paidInstallments)
                {
                    if (inst.Status == "Paga")
                    {
                        parcelasRecebidasPrincipal += inst.ValorPrincipal;
                        jurosRecebidos += (inst.ValorPago - inst.ValorPrincipal);
                    }
                    else // Parcial
                    {
                        decimal totalDue = inst.ValorParcela + inst.Multa + inst.JurosMora;
                        decimal principalPaid = totalDue > 0 ? (inst.ValorPago * (inst.ValorPrincipal / totalDue)) : 0;
                        parcelasRecebidasPrincipal += principalPaid;
                        jurosRecebidos += (inst.ValorPago - principalPaid);
                    }
                }

                var entradas = new FinancialEntradasDto
                {
                    ParcelasRecebidas = parcelasRecebidasPrincipal,
                    JurosRecebidos = jurosRecebidos,
                    TaxasAdministrativas = 0 // Preparado para futuro
                };

                // 2. SAÍDAS
                // Empréstimos liberados
                var loanQuery = _context.LoanRequests
                    .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmPagamento" || l.Status == "Inadimplente" || l.Status == "Quitado" || l.Status == "FinalizadoPorInadimplencia")
                    .AsQueryable();

                if (periodoInicio.HasValue) loanQuery = loanQuery.Where(l => l.DataAprovacao >= start);
                if (periodoFim.HasValue) loanQuery = loanQuery.Where(l => l.DataAprovacao <= end);

                decimal emprestimosLiberados = await loanQuery.SumAsync(l => l.ValorSolicitado);

                // Rendimentos pagos
                var txQuery = _context.InvestorTransactions
                    .Where(t => t.Tipo == "RENDIMENTO")
                    .AsQueryable();

                if (periodoInicio.HasValue) txQuery = txQuery.Where(t => t.DataMovimentacao >= start);
                if (periodoFim.HasValue) txQuery = txQuery.Where(t => t.DataMovimentacao <= end);

                decimal rendimentosPagos = await txQuery.SumAsync(t => t.Valor);

                // Coberturas administrativas (payouts do fundo de proteção)
                var coverQuery = _context.FinancialEventHistories
                    .Where(e => e.EventoExecutado == "UTILIZACAO_FUNDO")
                    .AsQueryable();

                if (periodoInicio.HasValue) coverQuery = coverQuery.Where(e => e.Data >= start);
                if (periodoFim.HasValue) coverQuery = coverQuery.Where(e => e.Data <= end);

                decimal coberturasAdministrativas = await coverQuery.SumAsync(e => e.Valor ?? 0);

                var saidas = new FinancialSaidasDto
                {
                    EmprestimosLiberados = emprestimosLiberados,
                    RendimentosPagos = rendimentosPagos,
                    CoberturasAdministrativas = coberturasAdministrativas
                };

                // 3. RESUMO
                // Total em atraso (atual outstanding)
                decimal totalEmAtraso = await _context.LoanInstallments
                    .Where(i => (i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "COBRANCA_INTENSIVA" || i.Status == "Atrasada" || i.Status == "Parcial") && i.DataVencimento < DateTime.UtcNow.Date)
                    .SumAsync(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                // Total inadimplente
                decimal totalInadimplente = await _context.LoanInstallments
                    .Where(i => i.Status == "INADIMPLENTE" && i.DataVencimento < DateTime.UtcNow.Date)
                    .SumAsync(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                var resumo = new FinancialResumoDto
                {
                    TotalRecebido = entradas.TotalEntradas,
                    TotalEmprestado = saidas.EmprestimosLiberados,
                    TotalEmAtraso = totalEmAtraso,
                    TotalInadimplente = totalInadimplente,
                    ResultadoOperacional = entradas.TotalEntradas - saidas.TotalSaidas
                };

                var report = new FinancialConsolidatedReportDto
                {
                    Entradas = entradas,
                    Saidas = saidas,
                    Resumo = resumo
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 5. Relatório Individual de Tomador
        [HttpGet("individual/borrower/{id}")]
        public async Task<IActionResult> GetBorrowerIndividual(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Profile)
                    .Include(u => u.Company)
                    .Include(u => u.LoanRequests)
                    .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Cliente");

                if (user == null)
                {
                    return NotFound(new { message = "Tomador não encontrado." });
                }

                // Limite e Crédito
                var rules = await _context.ScoreCreditRules.AsNoTracking().Where(r => r.Ativo).ToListAsync();
                int score = user.Profile?.ScoreAtual ?? 0;
                var rule = rules.FirstOrDefault(r => score >= r.ScoreMin && score <= r.ScoreMax);
                decimal limiteMaximo = rule?.LimiteEmprestimo ?? 0;

                var activeLoans = user.LoanRequests
                    .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmPagamento" || l.Status == "Inadimplente")
                    .ToList();

                decimal limiteUtilizado = activeLoans.Sum(l => l.ValorSolicitado);
                decimal limiteDisponivel = Math.Max(0, limiteMaximo - limiteUtilizado);

                // Empréstimos
                var loansList = user.LoanRequests
                    .Select(l => new BorrowerIndividualLoanDto
                    {
                        Id = l.Id,
                        ValorSolicitado = l.ValorSolicitado,
                        ValorAprovado = l.Status != "Pendente" && l.Status != "EmAnalise" && l.Status != "Reprovado" ? l.ValorSolicitado : 0,
                        TipoEmprestimo = l.TipoEmprestimo ?? "Pendente",
                        Juros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                        Parcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                        Status = l.Status
                    })
                    .ToList();

                // Buscar parcelas do empréstimo mais recente ou ativo
                var lastLoan = user.LoanRequests
                    .OrderByDescending(l => l.DataCriacao)
                    .FirstOrDefault();

                var installmentsList = new List<BorrowerIndividualInstallmentDto>();
                int parcelasEmAberto = 0;
                int parcelasAtrasadas = 0;
                decimal totalPago = 0;

                if (lastLoan != null)
                {
                    var installments = await _context.LoanInstallments
                        .Where(i => i.LoanRequestId == lastLoan.Id)
                        .OrderBy(i => i.NumeroParcela)
                        .ToListAsync();

                    var today = DateTime.UtcNow.Date;

                    foreach (var i in installments)
                    {
                        installmentsList.Add(new BorrowerIndividualInstallmentDto
                        {
                            Numero = i.NumeroParcela,
                            Valor = i.ValorParcela,
                            Vencimento = i.DataVencimento,
                            Situacao = i.Status,
                            DataPagamento = i.DataPagamento
                        });

                        if (i.Status != "Paga" && i.Status != "Cancelada")
                        {
                            parcelasEmAberto++;
                            if (i.DataVencimento.Date < today)
                            {
                                parcelasAtrasadas++;
                            }
                        }

                        totalPago += i.ValorPago;
                    }
                }

                // Resumos globais do tomador
                var approvedLoans = user.LoanRequests
                    .Where(l => l.Status != "Pendente" && l.Status != "EmAnalise" && l.Status != "Reprovado")
                    .ToList();

                var report = new BorrowerIndividualReportDto
                {
                    Nome = user.Profile?.Nome ?? "Sem Nome",
                    CPF = user.Profile?.CPF ?? string.Empty,
                    Email = user.Email,
                    Telefone = user.Profile?.Telefone ?? string.Empty,
                    Empresa = user.Company?.RazaoSocial ?? string.Empty,
                    CNPJ = user.Company?.CNPJ ?? string.Empty,
                    LimiteDisponivel = limiteDisponivel,
                    LimiteUtilizado = limiteUtilizado,
                    Emprestimos = loansList,
                    Parcelas = installmentsList,
                    TotalSolicitado = user.LoanRequests.Sum(l => l.ValorSolicitado),
                    TotalAprovado = approvedLoans.Sum(l => l.ValorSolicitado),
                    TotalPago = totalPago,
                    ParcelasEmAberto = parcelasEmAberto,
                    ParcelasAtrasadas = parcelasAtrasadas
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 6. Relatório Individual de Investidor
        [HttpGet("individual/investor/{id}")]
        public async Task<IActionResult> GetInvestorIndividual(Guid id)
        {
            try
            {
                var user = await _context.Users
                    .Include(u => u.Profile)
                    .Include(u => u.Wallet)
                    .Include(u => u.Investments)
                        .ThenInclude(i => i.Loan)
                    .FirstOrDefaultAsync(u => u.Id == id && u.Role == "Investidor");

                if (user == null)
                {
                    return NotFound(new { message = "Investidor não encontrado." });
                }

                // Investimentos
                var investmentsList = user.Investments
                    .Select(i => new InvestorIndividualInvestmentDto
                    {
                        ValorInvestido = i.ValorInvestido,
                        Rentabilidade = user.Wallet != null ? user.Wallet.Rentabilidade : 0,
                        DataAprovacao = i.CreatedAt
                    })
                    .ToList();

                // Movimentações
                var transactions = await _context.InvestorTransactions
                    .Where(t => t.InvestorId == id)
                    .OrderByDescending(t => t.DataMovimentacao)
                    .Select(t => new InvestorIndividualTransactionDto
                    {
                        Tipo = t.Tipo,
                        Valor = t.Valor,
                        Data = t.DataMovimentacao,
                        Descricao = t.Descricao
                    })
                    .ToListAsync();

                // Resumo
                decimal totalInvestido = user.Investments.Sum(i => i.ValorInvestido);
                decimal totalRecebido = user.Wallet != null ? user.Wallet.Rendimentos : 0;
                decimal saldoAtual = user.Wallet != null ? (user.Wallet.SaldoDisponivel + user.Wallet.SaldoInvestido) : 0;

                var report = new InvestorIndividualReportDto
                {
                    Nome = user.Profile?.Nome ?? "Sem Nome",
                    CPF = user.Profile?.CPF ?? string.Empty,
                    Email = user.Email,
                    Investimentos = investmentsList,
                    Movimentacoes = transactions,
                    TotalInvestido = totalInvestido,
                    TotalRecebido = totalRecebido,
                    SaldoAtual = saldoAtual
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // 7. Relatório de Auditoria de Alocação
        [HttpGet("allocations")]
        public async Task<IActionResult> GetAllocationsAudit(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? investorName = null,
            [FromQuery] string? status = null)
        {
            try
            {
                var query = _context.Investments
                    .Include(i => i.Investor)
                    .ThenInclude(u => u!.Profile)
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(investorName))
                {
                    var term = investorName.Trim().ToLower();
                    query = query.Where(i => i.Investor != null && i.Investor.Profile != null && i.Investor.Profile.Nome.ToLower().Contains(term));
                }

                if (!string.IsNullOrWhiteSpace(status))
                {
                    query = query.Where(i => i.Status == status);
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var list = await query
                    .OrderByDescending(i => i.CreatedAt)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new AllocationAuditItemDto
                    {
                        Id = i.Id,
                        InvestorName = i.Investor != null && i.Investor.Profile != null ? i.Investor.Profile.Nome : "Sem Nome",
                        LoanId = i.LoanId,
                        ValorInvestido = i.ValorInvestido,
                        Status = i.Status,
                        CreatedAt = i.CreatedAt
                    })
                    .ToListAsync();

                var result = new PagedResultDto<AllocationAuditItemDto>
                {
                    Items = list,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
