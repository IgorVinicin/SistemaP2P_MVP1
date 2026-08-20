using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using backend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/loans")]
    [Authorize(Roles = "Administrador")]
    public class AdminLoanController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AllocationEngineService _allocationService;

        public AdminLoanController(AppDbContext context, AllocationEngineService allocationService)
        {
            _context = context;
            _allocationService = allocationService;
        }

        // GET /api/admin/loans/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingLoans(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? nivelRisco = null,
            [FromQuery] string? tipoEmprestimo = null,
            [FromQuery] bool? somenteAumentoLimite = null)
        {
            var query = _context.LoanRequests
                .AsNoTracking()
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Where(l => l.Status == "Pendente")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => (l.User != null && l.User.Email.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.Nome.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.CPF.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(nivelRisco))
            {
                query = query.Where(l => l.NivelRisco == nivelRisco);
            }

            if (!string.IsNullOrWhiteSpace(tipoEmprestimo))
            {
                query = query.Where(l => l.TipoEmprestimo == tipoEmprestimo);
            }

            if (somenteAumentoLimite.HasValue)
            {
                query = query.Where(l => l.SolicitaAumentoLimite == somenteAumentoLimite.Value);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(l => l.DataCriacao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    l.Status,
                    TaxaJuros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                    l.ValorTotal,
                    Risco = l.NivelRisco,
                    l.DataCriacao,
                    l.DataSolicitacao,
                    Nome = l.User != null && l.User.Profile != null ? l.User.Profile.Nome : string.Empty,
                    CPF = l.User != null && l.User.Profile != null ? l.User.Profile.CPF : string.Empty,
                    Score = l.ScoreNoMomento > 0 ? l.ScoreNoMomento : l.ScoreNaSolicitacao,
                    l.TipoEmprestimo,
                    l.Periodicidade,
                    l.SolicitaAumentoLimite,
                    l.LimiteNoMomentoSolicitacao
                })
                .ToListAsync();

            var result = new PagedResultDto<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        // GET /api/admin/loans/active
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveLoans(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? tipoEmprestimo = null)
        {
            var query = _context.LoanRequests
                .AsNoTracking()
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmAnalise" || l.Status == "EmPagamento" || l.Status == "Inadimplente")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => (l.User != null && l.User.Email.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.Nome.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.CPF.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(tipoEmprestimo))
            {
                query = query.Where(l => l.TipoEmprestimo == tipoEmprestimo);
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(l => l.DataCriacao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    l.Status,
                    TaxaJuros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                    l.ValorTotal,
                    Risco = l.NivelRisco,
                    l.DataCriacao,
                    l.DataSolicitacao,
                    Nome = l.User != null && l.User.Profile != null ? l.User.Profile.Nome : string.Empty,
                    CPF = l.User != null && l.User.Profile != null ? l.User.Profile.CPF : string.Empty,
                    Score = l.ScoreNoMomento > 0 ? l.ScoreNoMomento : l.ScoreNaSolicitacao,
                    l.TipoEmprestimo,
                    l.Periodicidade,
                    TemParcelaAtrasada = _context.LoanInstallments.Any(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date),
                    DiasAtrasoMax = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).Select(i => (int?)(DateTime.UtcNow.Date - i.DataVencimento.Date).TotalDays).Max() ?? 0,
                    ParcelaAtrasadaFormatada = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).OrderBy(i => i.NumeroParcela).Select(i => i.NumeroParcela + "/" + (l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas)).FirstOrDefault()
                })
                .ToListAsync();

            var result = new PagedResultDto<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        // GET /api/admin/loans/active/grouped
        [HttpGet("active/grouped")]
        public async Task<IActionResult> GetGroupedActiveLoans(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? tipoEmprestimo = null)
        {
            var query = _context.LoanRequests
                .AsNoTracking()
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmAnalise" || l.Status == "EmPagamento" || l.Status == "Inadimplente")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(l => (l.User != null && l.User.Email.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.Nome.ToLower().Contains(term)) ||
                                         (l.User != null && l.User.Profile != null && l.User.Profile.CPF.Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(l => l.Status == status);
            }

            if (!string.IsNullOrWhiteSpace(tipoEmprestimo))
            {
                query = query.Where(l => l.TipoEmprestimo == tipoEmprestimo);
            }

            var tomadorIdsQuery = query.Select(l => l.UserId).Distinct();
            var totalCount = await tomadorIdsQuery.CountAsync();
            
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 10;
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            var pagedTomadorIds = await tomadorIdsQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var loans = await query
                .Where(l => pagedTomadorIds.Contains(l.UserId))
                .OrderByDescending(l => l.DataCriacao)
                .ToListAsync();

            var grouped = loans.GroupBy(l => l.UserId).Select(g => new
            {
                TomadorId = g.Key,
                Nome = g.First().User?.Profile?.Nome ?? "N/A",
                CPF = g.First().User?.Profile?.CPF ?? "N/A",
                Loans = g.Select(l => new
                {
                    Id = l.Id,
                    ValorSolicitado = l.ValorSolicitado,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    Status = l.Status,
                    TaxaJuros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                    ValorTotal = l.ValorTotal,
                    Risco = l.NivelRisco,
                    DataCriacao = l.DataCriacao,
                    DataSolicitacao = l.DataSolicitacao,
                    TipoEmprestimo = l.TipoEmprestimo,
                    Periodicidade = l.Periodicidade,
                    Score = l.ScoreNoMomento > 0 ? l.ScoreNoMomento : l.ScoreNaSolicitacao,
                    TemParcelaAtrasada = _context.LoanInstallments.Any(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date),
                    DiasAtrasoMax = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).Select(i => (int?)(DateTime.UtcNow.Date - i.DataVencimento.Date).TotalDays).Max() ?? 0,
                    ParcelaAtrasadaFormatada = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).OrderBy(i => i.NumeroParcela).Select(i => i.NumeroParcela + "/" + (l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas)).FirstOrDefault()
                }).ToList()
            }).ToList();

            var result = new PagedResultDto<object>
            {
                Items = grouped.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
        }

        // GET /api/admin/loans/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLoanDetails(Guid id)
        {
            var loan = await _context.LoanRequests
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Include(l => l.User)
                .ThenInclude(u => u!.Company)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound(new { message = "Solicitação de empréstimo não encontrada." });
            }

            var outrosEmprestimos = await _context.LoanRequests
                .Where(l => l.UserId == loan.UserId && l.Id != loan.Id)
                .OrderByDescending(l => l.DataCriacao)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    l.Status,
                    l.DataSolicitacao
                })
                .ToListAsync();

            var historicoPagamentos = await _context.LoanInstallments
                .Include(i => i.LoanRequest)
                .Where(i => i.LoanRequest!.UserId == loan.UserId)
                .OrderByDescending(i => i.DataVencimento)
                .Select(i => new
                {
                    i.LoanRequestId,
                    i.NumeroParcela,
                    i.ValorParcela,
                    i.ValorPago,
                    i.DataVencimento,
                    i.DataPagamento,
                    i.DiasAtraso,
                    i.Status,
                    i.Multa,
                    i.JurosMora
                })
                .ToListAsync();

            var detail = new
            {
                loan.Id,
                loan.ValorSolicitado,
                QuantidadeParcelas = loan.QuantidadeParcelas > 0 ? loan.QuantidadeParcelas : loan.Parcelas,
                loan.Status,
                TaxaJuros = loan.TaxaJuros > 0 ? loan.TaxaJuros : loan.TaxaJurosAplicada,
                loan.ValorTotal,
                Risco = loan.NivelRisco,
                loan.Finalidade,
                loan.Observacoes,
                loan.DataCriacao,
                loan.DataSolicitacao,
                loan.DataAnalise,
                loan.DataAprovacao,
                loan.ObservacaoAdmin,
                loan.TipoEmprestimo,
                loan.Periodicidade,
                loan.SolicitaAumentoLimite,
                loan.LimiteNoMomentoSolicitacao,
                Nome = loan.User?.Profile?.Nome ?? loan.User?.Email ?? string.Empty,
                CPF = loan.User?.Profile?.CPF ?? string.Empty,
                Email = loan.User?.Email ?? string.Empty,
                UserProfile = loan.User != null && loan.User.Profile != null ? new
                {
                    Nome = loan.User.Profile.Nome,
                    CPF = loan.User.Profile.CPF,
                    Telefone = loan.User.Profile.Telefone,
                    DataNascimento = loan.User.Profile.DataNascimento,
                    Score = loan.ScoreNoMomento > 0 ? loan.ScoreNoMomento : loan.ScoreNaSolicitacao
                } : null,
                Company = loan.User != null && loan.User.Company != null ? new
                {
                    loan.User.Company.RazaoSocial,
                    loan.User.Company.NomeFantasia,
                    loan.User.Company.CNPJ,
                    loan.User.Company.FaturamentoMensal
                } : null,
                OutrosEmprestimos = outrosEmprestimos,
                HistoricoPagamentos = historicoPagamentos
            };

            return Ok(detail);
        }

        // POST /api/admin/loans/{id}/approve
        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveLoan(Guid id, [FromBody] ApproveLoanRequestDto dto)
        {
            try
            {
                if (dto == null) return BadRequest(new { message = "Dados de aprovação inválidos." });

                var loan = await _context.LoanRequests.FindAsync(id);
                if (loan == null) return NotFound(new { message = "Solicitação de empréstimo não encontrada." });
                if (loan.Status != "Pendente") return BadRequest(new { message = "Esta solicitação de empréstimo não está pendente de análise." });
                if (string.IsNullOrWhiteSpace(dto.TipoEmprestimo)) return BadRequest(new { message = "Tipo de empréstimo é obrigatório." });

                string tipo = dto.TipoEmprestimo.Trim();
                if (tipo != "Master" && tipo != "Plus" && tipo != "Gold") return BadRequest(new { message = "Tipo de empréstimo inválido." });

                string periodicidade = tipo switch { "Master" => "Diario", "Plus" => "DiarioUtil", _ => dto.PeriodicidadeGold! };
                
                try
                {
                    if (dto.ValorAprovado.HasValue && dto.ValorAprovado.Value > 0)
                    {
                        loan.ValorSolicitado = dto.ValorAprovado.Value;
                    }

                    loan.TipoEmprestimo = tipo;
                    loan.Periodicidade = periodicidade;
                    loan.QuantidadeParcelas = dto.QuantidadeParcelas;
                    loan.Parcelas = dto.QuantidadeParcelas;
                    loan.TaxaJuros = dto.TaxaJuros;
                    loan.TaxaJurosAplicada = dto.TaxaJuros;
                    loan.Status = "Aprovado";
                    loan.DataAnalise = DateTime.UtcNow;
                    loan.DataAprovacao = DateTime.UtcNow;

                    var priceCalc = new PriceCalculationService();
                    var installments = priceCalc.CalculateInstallments(loan.Id, loan.ValorSolicitado, loan.QuantidadeParcelas, loan.TaxaJuros, loan.Periodicidade, 1m, dto.DataPrimeiraParcela);
                    loan.ValorTotal = installments.Sum(inst => inst.ValorParcela);
                    _context.LoanInstallments.AddRange(installments);

                    if (dto.InvestorGroupId.HasValue && dto.InvestorGroupId.Value != Guid.Empty)
                    {
                        var (allocated, errorMessage) = await _allocationService.AllocateInvestorsForLoanAsync(loan.Id, dto.InvestorGroupId);
                        if (!allocated)
                        {
                            throw new Exception(errorMessage);
                        }
                    }
                    else
                    {
                        // Financiado com recursos próprios da administração (operação manual sem desconto de investidores)
                        var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";
                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = adminEmail,
                            EventoExecutado = "APROVACAO_RECURSOS_PROPRIOS",
                            Observacao = $"Empréstimo {loan.Id} aprovado com recursos próprios da administração (sem rateio de investidores). Valor: {loan.ValorSolicitado:C2}.",
                            LoanRequestId = loan.Id,
                            Valor = loan.ValorSolicitado
                        });

                        var ledgerService = new LedgerService(_context);
                        await ledgerService.AddTransactionAsync(
                            source: LedgerAccount.PlatformCash,
                            destination: LedgerAccount.BorrowerAccount,
                            amount: loan.ValorSolicitado,
                            type: LedgerTransactionType.LoanDisbursement,
                            referenceType: "Loan",
                            referenceId: loan.Id,
                            loanId: loan.Id,
                            userId: loan.UserId,
                            observacao: $"Desembolso com recursos próprios da administração para empréstimo {loan.Id}",
                            createdBy: adminEmail
                        );
                    }

                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Solicitação de empréstimo aprovada com sucesso.", status = loan.Status });
                }
                catch (Exception innerEx)
                {
                    return BadRequest(new { message = innerEx.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/loans/{id}/reprove
        [HttpPost("{id}/reprove")]
        public async Task<IActionResult> ReproveLoan(Guid id, [FromBody] AdminReproveLoanDto dto)
        {
            try
            {
                var loan = await _context.LoanRequests.FindAsync(id);
                if (loan == null)
                {
                    return NotFound(new { message = "Solicitação de empréstimo não encontrada." });
                }

                if (loan.Status != "Pendente")
                {
                    return BadRequest(new { message = "Esta solicitação de empréstimo não está pendente de análise." });
                }

                loan.Status = "Reprovado";
                loan.DataAnalise = DateTime.UtcNow;
                loan.ObservacaoAdmin = dto.Observacao;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Solicitação de empréstimo reprovada com sucesso.", status = loan.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/loans/{id}/finish
        [HttpPost("{id}/finish")]
        public async Task<IActionResult> FinishLoan(Guid id)
        {
            try
            {
                var loan = await _context.LoanRequests
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null)
                {
                    return NotFound(new { message = "Solicitação de empréstimo não encontrada." });
                }

                if (loan.Status == "Quitado")
                {
                    return BadRequest(new { message = "Este empréstimo já está quitado." });
                }

                var installments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == id && i.Status != "Paga" && i.Status != "Cancelada")
                    .ToListAsync();

                try
                {
                    var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                    decimal defaultMulta = config?.PercentualMultaAtraso ?? 2.00m;
                    decimal defaultJurosDiario = config?.PercentualJurosDiario ?? 0.033m;
                    decimal percentualMulta = loan.PercentualMultaAtrasoContrato > 0 ? loan.PercentualMultaAtrasoContrato : defaultMulta;
                    decimal percentualJurosDiario = loan.PercentualJurosDiarioContrato > 0 ? loan.PercentualJurosDiarioContrato : defaultJurosDiario;
                    var today = DateTime.UtcNow.Date;
                    var adminEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Administrador";

                    var distService = new WalletDistributionService(_context);

                    foreach (var installment in installments)
                    {
                        var vencimento = installment.DataVencimento.Date;
                        if (vencimento < today)
                        {
                            int dias = (int)(today - vencimento).TotalDays;
                            installment.DiasAtraso = dias;
                            installment.Multa = Math.Round(installment.ValorParcela * (percentualMulta / 100m), 2);
                            installment.JurosMora = Math.Round(installment.ValorParcela * (percentualJurosDiario / 100m) * dias, 2);
                            installment.ValorPago = installment.ValorParcela + installment.Multa + installment.JurosMora;
                        }
                        else
                        {
                            installment.ValorPago = installment.ValorParcela;
                            installment.DiasAtraso = 0;
                            installment.Multa = 0;
                            installment.JurosMora = 0;
                        }

                        installment.DataPagamento = DateTime.UtcNow;
                        installment.Status = "Paga";
                        installment.UpdatedAt = DateTime.UtcNow;

                        // Registrar evento de pagamento no histórico para cada parcela
                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = adminEmail,
                            EventoExecutado = "PAGAMENTO_PARCELA",
                            Observacao = $"Parcela {installment.NumeroParcela} do Empréstimo {installment.LoanRequestId} foi paga e quitada administrativamente. Valor Pago: {installment.ValorPago:C2}.",
                            LoanRequestId = installment.LoanRequestId,
                            Valor = installment.ValorPago
                        });

                        // Salvar as alterações da parcela antes de rodar a distribuição
                        await _context.SaveChangesAsync();

                        // Distribuir os rendimentos aos investidores para essa parcela
                        await distService.DistributeInstallmentPaymentAsync(installment.Id, adminEmail);
                    }

                    // Por fim, atualizar o status do empréstimo para Quitado
                    loan.Status = "Quitado";
                    loan.DataEncerramento = DateTime.UtcNow;

                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "MUDANCA_STATUS",
                        Observacao = $"Empréstimo {loan.Id} foi totalmente quitado administrativamente.",
                        LoanRequestId = loan.Id,
                        Valor = loan.ValorTotal
                    });

                    await _context.SaveChangesAsync();
                    return Ok(new { message = "Empréstimo quitado com sucesso.", status = loan.Status });
                }
                catch (Exception innerEx)
                {
                    return BadRequest(new { message = innerEx.Message });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
