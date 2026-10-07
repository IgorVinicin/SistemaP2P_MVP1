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
    [Route("api")]
    [Authorize]
    public class LoanInstallmentController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DelinquencyService _delinquencyService;

        public LoanInstallmentController(AppDbContext context, DelinquencyService delinquencyService)
        {
            _context = context;
            _delinquencyService = delinquencyService;
        }

        // GET /api/loans/{loanRequestId}/installments
        [HttpGet("loans/{loanRequestId}/installments")]
        public async Task<IActionResult> GetInstallments(Guid loanRequestId)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = "Usuário não autenticado." });
                }

                bool isAdmin = User.IsInRole("Administrador") || User.FindFirst(ClaimTypes.Role)?.Value == "Administrador";

                var loan = await _context.LoanRequests.AsNoTracking().FirstOrDefaultAsync(l => l.Id == loanRequestId);
                if (loan == null)
                {
                    if (isAdmin)
                    {
                        return NotFound(new { message = "Empréstimo não encontrado." });
                    }
                    return StatusCode(403, new { message = "Acesso negado." });
                }

                if (!isAdmin && loan.UserId != userId)
                {
                    return StatusCode(403, new { message = "Acesso negado." });
                }

                // Atualizar parcelas atrasadas antes de retornar
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var installments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == loanRequestId)
                    .OrderBy(i => i.NumeroParcela)
                    .ToListAsync();

                return Ok(installments);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/loans/installments
        [HttpGet("admin/loans/installments")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> GetAdminInstallments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? cliente = null,
            [FromQuery] string? status = null,
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null,
            [FromQuery] bool? somenteAtrasadas = null)
        {
            try
            {
                // Atualizar parcelas atrasadas antes de retornar
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var query = _context.LoanInstallments
                    .AsNoTracking()
                    .Include(i => i.LoanRequest)
                    .ThenInclude(l => l!.User)
                    .ThenInclude(u => u!.Profile)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(cliente))
                {
                    var term = cliente.ToLower();
                    query = query.Where(i => (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Email.ToLower().Contains(term)) ||
                                             (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null && i.LoanRequest.User.Profile.Nome.ToLower().Contains(term)));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(i => i.Status.ToLower() == status.ToLower());
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(i => i.DataVencimento >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(i => i.DataVencimento <= periodoFim.Value.ToUniversalTime());
                }

                if (somenteAtrasadas == true)
                {
                    query = query.Where(i => i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "COBRANCA_INTENSIVA" || i.Status == "INADIMPLENTE" || i.Status == "Atrasada");
                }

                page = Math.Max(1, page);
                pageSize = Math.Clamp(pageSize, 1, 100);
                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var list = await query
                    .OrderBy(i => i.DataVencimento)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(i => new
                    {
                        i.Id,
                        i.LoanRequestId,
                        i.NumeroParcela,
                        i.ValorParcela,
                        i.ValorPrincipal,
                        i.ValorJuros,
                        i.DataVencimento,
                        i.DataPagamento,
                        i.Status,
                        i.ValorPago,
                        i.DiasAtraso,
                        i.Multa,
                        i.JurosMora,
                        ClienteNome = i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null ? i.LoanRequest.User.Profile.Nome : "Sem Nome",
                        ClienteEmail = i.LoanRequest != null && i.LoanRequest.User != null ? i.LoanRequest.User.Email : string.Empty
                    })
                    .ToListAsync();

                var result = new backend.Application.DTOs.PagedResultDto<object>
                {
                    Items = list.Cast<object>().ToList(),
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

        // POST /api/admin/loans/installments/{id}/settle
        [HttpPost("admin/loans/installments/{id}/settle")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> AdminSettleInstallment(Guid id, [FromBody] AdminSettleInstallmentDto? dto)
        {
            try
            {
                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .ThenInclude(l => l!.User)
                    .ThenInclude(u => u!.Profile)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (installment == null || installment.LoanRequest == null)
                {
                    return NotFound(new { message = "Parcela não encontrada." });
                }

                if (installment.Status == "Paga")
                {
                    return BadRequest(new { message = "Esta parcela já está paga." });
                }

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";
                if (await _context.GatewayCharges.AnyAsync(c => c.Kind == "Installment" && c.ReferenceId == id && c.IsActive))
                    return Conflict(new { message = "Cancele a cobrança PIX ativa antes de registrar o recebimento manual." });

                decimal totalDevido = installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago;
                decimal valorEfetivo = dto?.ValorPago ?? totalDevido;
                if (string.IsNullOrWhiteSpace(dto?.Observacao))
                    return BadRequest(new { message = "Informe o comprovante ou justificativa do recebimento manual confirmado." });
                await new InstallmentPaymentService(_context).ApplyPaymentAsync(id, valorEfetivo, adminEmail);

                return Ok(new 
                { 
                    message = "Parcela quitada com sucesso pelo administrador.", 
                    installment = new 
                    {
                        id = installment.Id,
                        status = installment.Status,
                        valorPago = installment.ValorPago,
                        dataPagamento = installment.DataPagamento
                    }
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

    }

    public class AdminSettleInstallmentDto
    {
        public decimal? ValorPago { get; set; }
        public string? FormaPagamento { get; set; }
        public string? Observacao { get; set; }
    }

}
