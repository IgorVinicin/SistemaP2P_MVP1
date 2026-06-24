using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using backend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
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
            [FromQuery] string? tipoEmprestimo = null)
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
                    l.Periodicidade
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
                    l.Periodicidade
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

        // GET /api/admin/loans/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetLoanDetails(Guid id)
        {
            var loan = await _context.LoanRequests
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
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
                UserProfile = loan.User != null && loan.User.Profile != null ? new
                {
                    Nome = loan.User.Profile.Nome,
                    CPF = loan.User.Profile.CPF,
                    Telefone = loan.User.Profile.Telefone,
                    DataNascimento = loan.User.Profile.DataNascimento,
                    Score = loan.ScoreNoMomento > 0 ? loan.ScoreNoMomento : loan.ScoreNaSolicitacao
                } : null,
                OutrosEmprestimos = outrosEmprestimos
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
                
                using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
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
                    var installments = priceCalc.CalculateInstallments(loan.Id, loan.ValorSolicitado, loan.QuantidadeParcelas, loan.TaxaJuros, loan.Periodicidade);
                    loan.ValorTotal = installments.Sum(inst => inst.ValorParcela);
                    _context.LoanInstallments.AddRange(installments);

                    bool allocated = await _allocationService.AllocateInvestorsForLoanAsync(loan.Id);
                    if (!allocated)
                    {
                        throw new Exception("Falha na alocação: Saldo insuficiente entre investidores elegíveis.");
                    }

                    await _context.SaveChangesAsync();
                    await dbTransaction.CommitAsync();
                    return Ok(new { message = "Solicitação de empréstimo aprovada com sucesso.", status = loan.Status });
                }
                catch (Exception innerEx)
                {
                    await dbTransaction.RollbackAsync();
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
                var loan = await _context.LoanRequests.FindAsync(id);
                if (loan == null)
                {
                    return NotFound(new { message = "Solicitação de empréstimo não encontrada." });
                }

                loan.Status = "Quitado";
                loan.DataEncerramento = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Empréstimo finalizado com sucesso.", status = loan.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
