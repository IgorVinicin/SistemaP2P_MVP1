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

        // POST /api/loans/installments/{id}/pay
        [HttpPost("loans/installments/{id}/pay")]
        public async Task<IActionResult> PayInstallment(Guid id)
        {
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = "Usuário não autenticado." });
                }

                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .FirstOrDefaultAsync(i => i.Id == id);

                bool isAdmin = User.IsInRole("Administrador") || User.FindFirst(ClaimTypes.Role)?.Value == "Administrador";

                if (installment == null || installment.LoanRequest == null)
                {
                    if (isAdmin)
                    {
                        return NotFound(new { message = "Parcela não encontrada." });
                    }
                    return StatusCode(403, new { message = "Acesso negado." });
                }

                if (!isAdmin && installment.LoanRequest.UserId != userId)
                {
                    return StatusCode(403, new { message = "Acesso negado." });
                }

                if (installment.Status == "Paga")
                {
                    return BadRequest(new { message = "Esta parcela já está paga." });
                }

                try
                {
                    var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                    decimal percentualMulta = installment.LoanRequest.PercentualMultaAtrasoContrato > 0 ? installment.LoanRequest.PercentualMultaAtrasoContrato : (config?.PercentualMultaAtraso ?? 2.00m);
                    decimal percentualJurosDiario = installment.LoanRequest.PercentualJurosDiarioContrato > 0 ? installment.LoanRequest.PercentualJurosDiarioContrato : (config?.PercentualJurosDiario ?? 0.033m);

                    var today = DateTime.UtcNow.Date;
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

                    // Registrar evento de pagamento no histórico
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = User.FindFirst(ClaimTypes.Email)?.Value ?? "Cliente",
                        EventoExecutado = "PAGAMENTO_PARCELA",
                        Observacao = $"Parcela {installment.NumeroParcela} do Empréstimo {installment.LoanRequestId} foi paga com sucesso. Valor Pago: {installment.ValorPago:C2}.",
                        LoanRequestId = installment.LoanRequestId,
                        Valor = installment.ValorPago
                    });

                    // Salvar as alterações da parcela
                    await _context.SaveChangesAsync();

                    // Recalcular status do empréstimo de forma centralizada
                    var userEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Cliente";
                    await _delinquencyService.UpdateLoanStatusFromInstallmentsAsync(installment.LoanRequestId, userEmail);

                    // Integrar preparação de carteira de investidores
                    var distService = new WalletDistributionService(_context);
                    await distService.DistributeInstallmentPaymentAsync(installment.Id, userEmail);

                    return Ok(new 
                    { 
                        message = "Pagamento da parcela simulado com sucesso.", 
                        installment = new 
                        {
                            id = installment.Id,
                            status = installment.Status,
                            valorPago = installment.ValorPago,
                            dataPagamento = installment.DataPagamento
                        }
                    });
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


    }
}
