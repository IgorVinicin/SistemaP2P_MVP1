using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
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

        public AdminLoanController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/admin/loans/pending
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingLoans()
        {
            var pendingLoans = await _context.LoanRequests
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Where(l => l.Status == "Pendente")
                .OrderByDescending(l => l.DataCriacao)
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
                    Score = l.ScoreNoMomento > 0 ? l.ScoreNoMomento : l.ScoreNaSolicitacao
                })
                .ToListAsync();

            return Ok(pendingLoans);
        }

        // GET /api/admin/loans/active
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveLoans()
        {
            var activeLoans = await _context.LoanRequests
                .Include(l => l.User)
                .ThenInclude(u => u!.Profile)
                .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmAnalise")
                .OrderByDescending(l => l.DataCriacao)
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
                    Score = l.ScoreNoMomento > 0 ? l.ScoreNoMomento : l.ScoreNaSolicitacao
                })
                .ToListAsync();

            return Ok(activeLoans);
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
        public async Task<IActionResult> ApproveLoan(Guid id)
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

                loan.Status = "Aprovado";
                loan.DataAnalise = DateTime.UtcNow;
                loan.DataAprovacao = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(new { message = "Solicitação de empréstimo aprovada com sucesso.", status = loan.Status });
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
