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

        public LoanInstallmentController(AppDbContext context)
        {
            _context = context;
        }

        // POST /api/loans/installments/{id}/pay
        [HttpPost("loans/installments/{id}/pay")]
        public async Task<IActionResult> PayInstallment(Guid id)
        {
            try
            {
                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (installment == null)
                {
                    return NotFound(new { message = "Parcela não encontrada." });
                }

                if (installment.Status == "Paga")
                {
                    return BadRequest(new { message = "Esta parcela já está paga." });
                }

                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                decimal percentualMulta = config?.PercentualMultaAtraso ?? 2.00m;
                decimal percentualJurosDiario = config?.PercentualJurosDiario ?? 0.033m;

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

                // Salvar as alterações da parcela primeiro
                await _context.SaveChangesAsync();

                // Integrar preparação de carteira de investidores
                var distService = new WalletDistributionService(_context);
                await distService.DistributeInstallmentPaymentAsync(installment.Id);

                return Ok(new { message = "Pagamento da parcela simulado com sucesso.", installment });
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
                // Atualizar parcelas atrasadas antes de retornar
                await UpdateOverdueInstallmentsAsync();

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
            [FromQuery] string? cliente,
            [FromQuery] string? status,
            [FromQuery] DateTime? periodoInicio,
            [FromQuery] DateTime? periodoFim,
            [FromQuery] bool? somenteAtrasadas)
        {
            try
            {
                // Atualizar parcelas atrasadas antes de retornar
                await UpdateOverdueInstallmentsAsync();

                var query = _context.LoanInstallments
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
                    query = query.Where(i => i.Status == "Atrasada");
                }

                var list = await query
                    .OrderBy(i => i.DataVencimento)
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

                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task UpdateOverdueInstallmentsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var overdue = await _context.LoanInstallments
                .Where(i => i.Status == "Pendente" && i.DataVencimento.Date < today)
                .ToListAsync();

            if (overdue.Any())
            {
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                decimal percentualMulta = config?.PercentualMultaAtraso ?? 2.00m;
                decimal percentualJurosDiario = config?.PercentualJurosDiario ?? 0.033m;

                foreach (var inst in overdue)
                {
                    inst.Status = "Atrasada";
                    int dias = (int)(today - inst.DataVencimento.Date).TotalDays;
                    inst.DiasAtraso = dias;
                    inst.Multa = Math.Round(inst.ValorParcela * (percentualMulta / 100m), 2);
                    inst.JurosMora = Math.Round(inst.ValorParcela * (percentualJurosDiario / 100m) * dias, 2);
                    inst.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
        }
    }
}
