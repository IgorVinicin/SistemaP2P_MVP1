using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/ledger")]
    [Authorize(Roles = "Administrador")]
    public class AdminLedgerController : ControllerBase
    {
        private readonly LedgerService _ledgerService;
        private readonly AppDbContext _context;
        private readonly WalletDistributionService _walletDistributionService;

        public AdminLedgerController(LedgerService ledgerService, AppDbContext context, WalletDistributionService walletDistributionService)
        {
            _ledgerService = ledgerService;
            _context = context;
            _walletDistributionService = walletDistributionService;
        }

        // GET /api/admin/ledger
        [HttpGet]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? period = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] LedgerTransactionType? type = null,
            [FromQuery] LedgerAccount? account = null,
            [FromQuery] Guid? loanId = null,
            [FromQuery] Guid? userId = null)
        {
            try
            {
                var result = await _ledgerService.GetHistoryAsync(page, pageSize, period, startDate, endDate, type, account, loanId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                System.IO.File.WriteAllText("ledger_error.log", ex.ToString());
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/ledger/cover-installment/{id}
        [HttpPost("cover-installment/{id}")]
        public async Task<IActionResult> CoverInstallmentByFund(Guid id)
        {
            await using var transaction = _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            try
            {
                var installment = await _context.LoanInstallments.Include(i => i.LoanRequest).FirstOrDefaultAsync(i => i.Id == id);
                if (installment == null) return NotFound(new { message = "Parcela nao encontrada." });
                if (installment.Status is "Paga" or "Cancelada") return BadRequest(new { message = "Parcela encerrada." });
                if (await _context.GatewayCharges.AnyAsync(c => c.Kind == "Installment" && c.ReferenceId == id && c.IsActive))
                    return Conflict(new { message = "Cancele a cobrança PIX ativa antes de usar cobertura do fundo." });
                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null) return BadRequest(new { message = "Fundo nao configurado." });
                // Fund covers the original remaining installment; fines remain payable by the borrower.
                decimal amount = Math.Max(0, installment.ValorParcela - installment.ValorPago);
                if (amount <= 0 || fund.SaldoAtual < amount) return BadRequest(new { message = "Saldo insuficiente ou nenhuma cobertura pendente." });
                var actor = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Administrador";
                fund.SaldoAtual -= amount;
                fund.TotalUtilizado += amount;
                fund.UltimaAtualizacao = DateTime.UtcNow;
                _context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = actor, EventoExecutado = "UTILIZACAO_FUNDO", LoanRequestId = installment.LoanRequestId, Valor = amount, Observacao = "Cobertura do saldo original da parcela pelo fundo." });
                await new InstallmentPaymentService(_context).ApplyPaymentAsync(id, amount, actor, LedgerAccount.ProtectionFund);
                if (transaction != null) await transaction.CommitAsync();
                return Ok(new { message = "Cobertura confirmada pelo fundo.", installment.Status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
