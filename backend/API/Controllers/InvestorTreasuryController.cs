using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Application.Services;
using backend.Application.DTOs;
using backend.Infrastructure;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/investor/treasury")]
    [Authorize(Roles = "Investidor")]
    public class InvestorTreasuryController : ControllerBase
    {
        private readonly ITreasuryService _treasuryService;
        private readonly AppDbContext _context;

        public InvestorTreasuryController(ITreasuryService treasuryService, AppDbContext context)
        {
            _treasuryService = treasuryService;
            _context = context;
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idClaim) || !Guid.TryParse(idClaim, out Guid id))
            {
                throw new UnauthorizedAccessException("Usuário não identificado.");
            }
            return id;
        }

        [HttpPost("deposit")]
        public async Task<IActionResult> RequestDeposit([FromBody] CreateDepositDto request)
        {
            try
            {
                var userId = GetUserId();
                var deposit = await _treasuryService.RequestDepositAsync(userId, request.Valor, request.Observacao);
                return Ok(new { message = "Solicitação de depósito criada com sucesso. Aguardando pagamento bancário.", depositId = deposit.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("withdrawal")]
        public async Task<IActionResult> RequestWithdrawal([FromBody] CreateWithdrawalDto request)
        {
            try
            {
                var userId = GetUserId();
                var withdrawal = await _treasuryService.RequestWithdrawalAsync(userId, request.Valor, request.Observacao);
                return Ok(new { message = "Solicitação de saque realizada com sucesso. O valor foi bloqueado na sua carteira.", withdrawalId = withdrawal.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = GetUserId();

            var deposits = await _context.DepositRequests
                .Where(d => d.InvestorId == userId)
                .OrderByDescending(d => d.CreatedAt)
                .Select(d => new
                {
                    d.Id,
                    Type = "DEPOSIT",
                    d.Valor,
                    d.Status,
                    d.CreatedAt,
                    d.ProcessedAt
                })
                .ToListAsync();

            var withdrawals = await _context.WithdrawalRequests
                .Where(w => w.InvestorId == userId)
                .OrderByDescending(w => w.CreatedAt)
                .Select(w => new
                {
                    w.Id,
                    Type = "WITHDRAWAL",
                    w.Valor,
                    w.Status,
                    w.CreatedAt,
                    w.ProcessedAt
                })
                .ToListAsync();

            var history = deposits.Concat(withdrawals)
                .OrderByDescending(x => x.CreatedAt)
                .ToList();

            return Ok(history);
        }
    }
}
