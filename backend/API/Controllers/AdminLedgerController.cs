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
            try
            {
                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (installment == null) return NotFound(new { message = "Parcela não encontrada." });

                if (installment.Status == "Paga" || installment.Status == "Cancelada")
                {
                    return BadRequest(new { message = "Esta parcela já está paga ou cancelada." });
                }

                var adminEmail = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "Administrador";

                using var dbTransaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                    if (fund == null) return BadRequest(new { message = "Fundo de proteção não configurado." });

                    // A cobertura paga apenas o Valor da Parcela, sem juros ou multas do tomador, ou cobre tudo?
                    // Para simplificar, o Fundo cobre o Valor da Parcela Original.
                    decimal amountToCover = installment.ValorParcela;

                    if (fund.SaldoAtual < amountToCover)
                    {
                        return BadRequest(new { message = "Saldo insuficiente no Fundo de Proteção para cobrir esta parcela." });
                    }

                    // Deduzir do Fundo
                    fund.SaldoAtual -= amountToCover;
                    fund.TotalUtilizado += amountToCover;
                    fund.UltimaAtualizacao = DateTime.UtcNow;

                    // Marcar parcela como coberta pelo fundo
                    installment.Status = "Paga";
                    installment.ValorPago = amountToCover;
                    installment.DataPagamento = DateTime.UtcNow;
                    installment.UpdatedAt = DateTime.UtcNow;

                    // 1. Ledger: ProtectionFund -> PlatformCash
                    await _ledgerService.AddTransactionAsync(
                        source: LedgerAccount.ProtectionFund,
                        destination: LedgerAccount.PlatformCash,
                        amount: amountToCover,
                        type: LedgerTransactionType.ProtectionFundDebit,
                        referenceType: "Installment",
                        referenceId: installment.Id,
                        loanId: installment.LoanRequestId,
                        installmentId: installment.Id,
                        userId: installment.LoanRequest?.UserId,
                        observacao: $"Fundo de proteção cobrindo parcela {installment.NumeroParcela}",
                        createdBy: adminEmail
                    );

                    // 2. Evento Histórico Genérico
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "UTILIZACAO_FUNDO",
                        Observacao = $"Fundo de proteção utilizado para cobrir a parcela {installment.NumeroParcela} do empréstimo {installment.LoanRequestId}. Valor: {amountToCover:C2}.",
                        LoanRequestId = installment.LoanRequestId,
                        Valor = amountToCover
                    });

                    await _context.SaveChangesAsync();

                    // O WalletDistributionService já foi atualizado para registrar do PlatformCash para InvestorWallet.
                    await _walletDistributionService.DistributeInstallmentPaymentAsync(installment.Id, adminEmail);

                    // E atualizar o status do empréstimo, caso seja a última
                    var delinquencyService = new DelinquencyService(_context);
                    await delinquencyService.UpdateLoanStatusFromInstallmentsAsync(installment.LoanRequestId, adminEmail);

                    await dbTransaction.CommitAsync();

                    return Ok(new { message = "Parcela coberta pelo Fundo de Proteção com sucesso." });
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
    }
}
