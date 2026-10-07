using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Application.Services;
using backend.Application.Services.Asaas;
using backend.Infrastructure;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/treasury")]
    [Authorize(Roles = "Administrador")]
    public class AdminTreasuryController : ControllerBase
    {
        private readonly ITreasuryService _treasuryService;
        private readonly AppDbContext _context;
        private readonly IAsaasService _gateway;

        public AdminTreasuryController(ITreasuryService treasuryService, AppDbContext context, IAsaasService gateway)
        {
            _treasuryService = treasuryService;
            _context = context;
            _gateway = gateway;
        }

        // --- DEPÓSITOS ---

        [HttpGet("deposits")]
        public async Task<IActionResult> ListDeposits([FromQuery] string? status)
        {
            var query = _context.DepositRequests.Include(d => d.Investor).AsQueryable();
            if (!string.IsNullOrEmpty(status))
                query = query.Where(d => d.Status == status);

            var deposits = await query.OrderByDescending(d => d.CreatedAt).Select(d => new
            {
                d.Id, d.InvestorId, d.Valor, d.Status, d.GatewayReference, d.CreatedAt, d.ProcessedAt, d.Observacao,
                Investor = d.Investor == null ? null : new { d.Investor.Email }
            }).ToListAsync();
            return Ok(deposits);
        }

        [HttpPost("deposits/{id}/confirm")]
        public async Task<IActionResult> ConfirmDeposit(Guid id, [FromBody] ConfirmRequestDto request)
        {
            try
            {
                await Task.CompletedTask;
                return Conflict(new { message = "Depósitos são creditados somente após recebimento confirmado pelo webhook Asaas." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("deposits/{id}/cancel")]
        public async Task<IActionResult> CancelDeposit(Guid id, [FromBody] RejectRequestDto request)
        {
            try
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                var success = await _treasuryService.CancelDepositAsync(id, adminUser, request.Motivo);
                return success ? Ok(new { message = "Depósito cancelado com sucesso." }) : BadRequest("Falha ao cancelar.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }


        // --- SAQUES ---

        [HttpGet("withdrawals")]
        public async Task<IActionResult> ListWithdrawals([FromQuery] string? status)
        {
            var query = _context.WithdrawalRequests
                .Include(w => w.Investor)
                .ThenInclude(i => i!.Profile)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(w => w.Status == status);

            var withdrawals = await query
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new {
                    w.Id,
                    w.InvestorId,
                    Investor = w.Investor == null ? null : new {
                        Nome = w.Investor.Profile != null && !string.IsNullOrEmpty(w.Investor.Profile.Nome) ? w.Investor.Profile.Nome : w.Investor.Email,
                        Cpf = w.Investor.Profile != null ? w.Investor.Profile.CPF : "",
                        Email = w.Investor.Email
                    },
                    w.Valor,
                    ValorSolicitado = w.ValorSolicitado ?? w.Valor,
                    ValorAprovado = w.Status == "Pending" || w.Status == "Rejected" ? (decimal?)null : w.Valor,
                    w.Status,
                    w.ChavePix,
                    w.TipoChavePix,
                    w.Observacao,
                    w.CreatedAt,
                    w.ProcessedAt,
                    w.GatewayReference
                })
                .ToListAsync();

            return Ok(withdrawals);
        }

        [HttpPost("withdrawals/{id}/approve")]
        public async Task<IActionResult> ApproveWithdrawal(Guid id, [FromBody] ApproveWithdrawalRequestDto? request)
        {
            try
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                var success = await _treasuryService.ApproveWithdrawalAsync(id, adminUser, request?.ValorAprovado);
                return success ? Ok(new { message = "Saque aprovado e pronto para envio bancário." }) : BadRequest("Falha.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("withdrawals/{id}/reject")]
        public async Task<IActionResult> RejectWithdrawal(Guid id, [FromBody] RejectRequestDto request)
        {
            try
            {
                var adminUser = User.Identity?.Name ?? "Admin";
                var success = await _treasuryService.RejectWithdrawalAsync(id, adminUser, request.Motivo);
                return success ? Ok(new { message = "Saque rejeitado e saldo desbloqueado." }) : BadRequest("Falha.");
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("withdrawals/{id}/confirm")]
        public async Task<IActionResult> ConfirmWithdrawal(Guid id, [FromBody] ConfirmRequestDto request)
        {
            try
            {
                var transfer = await new GatewayWorkflowService(_context, _gateway, _treasuryService).SubmitWithdrawalAsync(id);
                return Ok(new { message = "Saque enviado ao Asaas. Aguardando confirmação da transferência.", transferId = transfer.TransferId, status = transfer.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }

    public class ConfirmRequestDto { public string? GatewayReference { get; set; } }
    public class RejectRequestDto { public string? Motivo { get; set; } }
    public class ApproveWithdrawalRequestDto { public decimal? ValorAprovado { get; set; } }
}
