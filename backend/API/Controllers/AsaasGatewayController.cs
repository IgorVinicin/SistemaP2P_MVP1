using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Application.DTOs;
using backend.Application.Services;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.API.Controllers;

[ApiController]
public class AsaasGatewayController : ControllerBase
{
    private readonly IAsaasService _gateway;
    private readonly ITreasuryService _treasury;
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AsaasGatewayController> _logger;
    private readonly GatewayWorkflowService _workflow;
    public AsaasGatewayController(IAsaasService asaasService, ITreasuryService treasuryService, AppDbContext context,
        IConfiguration configuration, ILogger<AsaasGatewayController> logger)
    {
        _gateway = asaasService; _treasury = treasuryService; _context = context; _configuration = configuration; _logger = logger;
        _workflow = new GatewayWorkflowService(context, asaasService, treasuryService);
    }
    private Guid UserId => Guid.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : throw new UnauthorizedAccessException("Usuário não autenticado.");

    [HttpGet("api/admin/gateway/test"), Authorize(Roles = "Administrador")]
    public async Task<IActionResult> TestGatewayConnection() => Ok(await _gateway.TestConnectionAsync());

    [HttpPost("api/investor/treasury/deposit-pix"), Authorize(Roles = "Investidor")]
    public async Task<IActionResult> GenerateDepositPix([FromBody] CreateDepositDto request)
    {
        try { return Ok(ChargeResponse(await _workflow.CreateDepositAsync(UserId, request.Valor, request.Observacao))); }
        catch (Exception ex) { return GatewayError(ex); }
    }
    [HttpGet("api/investor/treasury/deposits/{id}/status"), Authorize(Roles = "Investidor")]
    public async Task<IActionResult> GetDepositStatus(Guid id)
    {
        var charge = await _context.GatewayCharges.AsNoTracking().FirstOrDefaultAsync(c => c.Kind == "Deposit" && c.ReferenceId == id && c.UserId == UserId);
        return charge == null ? NotFound(new { message = "Cobrança não encontrada." }) : Ok(ChargeResponse(charge));
    }
    [HttpPost("api/investor/treasury/deposits/{id}/pix"), Authorize(Roles = "Investidor")]
    public async Task<IActionResult> ReopenDepositPix(Guid id)
    {
        try
        {
            var charge = await _context.GatewayCharges.FirstOrDefaultAsync(c => c.Kind == "Deposit" && c.ReferenceId == id && c.UserId == UserId);
            if (charge == null) return NotFound(new { message = "Cobrança não encontrada." });
            var user = await _context.Users.Include(u => u.Profile).FirstAsync(u => u.Id == UserId);
            return Ok(ChargeResponse(await _workflow.IssueChargeAsync(charge, user)));
        }
        catch (Exception ex) { return GatewayError(ex); }
    }
    [HttpPost("api/loans/installments/{id}/pix"), Authorize]
    public async Task<IActionResult> GenerateInstallmentPix(Guid id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] InstallmentPixRequest? request = null)
    {
        try { return Ok(ChargeResponse(await _workflow.CreateInstallmentAsync(id, UserId, User.IsInRole("Administrador"), request?.Valor))); }
        catch (Exception ex) { return GatewayError(ex); }
    }
    [HttpGet("api/loans/installments/{id}/pix/status"), Authorize]
    public async Task<IActionResult> GetInstallmentPixStatus(Guid id)
    {
        var query = _context.GatewayCharges.AsNoTracking().Where(c => c.Kind == "Installment" && c.ReferenceId == id);
        if (!User.IsInRole("Administrador")) query = query.Where(c => c.UserId == UserId);
        var charge = await query.OrderByDescending(c => c.CreatedAt).FirstOrDefaultAsync();
        return charge == null ? NotFound(new { message = "Cobrança não encontrada." }) : Ok(ChargeResponse(charge));
    }
    [HttpPost("api/admin/gateway/loans/{id}/disburse"), Authorize(Roles = "Administrador")]
    public async Task<IActionResult> DisburseLoan(Guid id, [FromBody] LoanDisbursementRequest request)
    {
        try { return Ok(TransferResponse(await _workflow.SubmitLoanAsync(id, request.ChavePix, request.TipoChavePix, request.ValorConfirmado, request.ConfirmacaoDestino, UserId.ToString()))); }
        catch (Exception ex) { return GatewayError(ex); }
    }
    [HttpPost("api/gateway/charges/{id}/cancel"), Authorize]
    public async Task<IActionResult> CancelCharge(Guid id)
    {
        try
        {
            var charge = await _context.GatewayCharges.FirstOrDefaultAsync(c => c.Id == id);
            if (charge == null) return NotFound(new { message = "Cobrança não encontrada." });
            if (!User.IsInRole("Administrador") && charge.UserId != UserId) return Forbid();
            await _workflow.CancelChargeAsync(charge);
            return Ok(ChargeResponse(charge));
        }
        catch (Exception ex) { return GatewayError(ex); }
    }
    [HttpGet("api/admin/gateway/loans/{id}/transfer/status"), Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GetLoanTransferStatus(Guid id)
    {
        var transfer = await _context.GatewayTransfers.AsNoTracking().Where(t => t.Kind == "Loan" && t.ReferenceId == id).OrderByDescending(t => t.CreatedAt).FirstOrDefaultAsync();
        return transfer == null ? NotFound(new { message = "Nenhum desembolso enviado." }) : Ok(TransferResponse(transfer));
    }
    [HttpGet("api/admin/gateway/events"), Authorize(Roles = "Administrador")]
    public async Task<IActionResult> GetEvents([FromQuery] string? status) => Ok(await _context.GatewayWebhookEvents.AsNoTracking()
        .Where(e => status == null || e.Status == status).OrderByDescending(e => e.ReceivedAt).Take(100).ToListAsync());

    [HttpPost("api/webhooks/asaas"), AllowAnonymous, RequestSizeLimit(262144)]
    public async Task<IActionResult> HandleAsaasWebhook()
    {
        var token = Environment.GetEnvironmentVariable("ASAAS_WEBHOOK_ACCESS_TOKEN") ?? _configuration["Asaas:WebhookAccessToken"];
        if (string.IsNullOrWhiteSpace(token) || token.Contains("seu_token", StringComparison.OrdinalIgnoreCase))
            return StatusCode(503, new { message = "Autenticação do webhook não configurada." });
        var supplied = Request.Headers["asaas-access-token"].ToString();
        if (string.IsNullOrWhiteSpace(supplied) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(token), Encoding.UTF8.GetBytes(supplied)))
            return Unauthorized(new { message = "Token de webhook inválido." });
        try
        {
            var payload = await JsonSerializer.DeserializeAsync<AsaasWebhookPayloadDto>(Request.Body);
            if (payload == null || string.IsNullOrWhiteSpace(payload.Id) || payload.Id.Length > 150 || string.IsNullOrWhiteSpace(payload.Event))
                return BadRequest(new { message = "Evento sem identificação válida." });
            if (await _context.GatewayWebhookEvents.AsNoTracking().AnyAsync(e => e.EventId == payload.Id)) return Ok(new { success = true, duplicate = true });
            await CancelTargetChargeBeforePartialSettlementAsync(payload);
            await using var tx = _context.Database.IsRelational() ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable) : null;
            if (await _context.GatewayWebhookEvents.AnyAsync(e => e.EventId == payload.Id)) return Ok(new { success = true, duplicate = true });
            var record = new GatewayWebhookEvent { EventId = payload.Id, Event = payload.Event,
                ResourceId = payload.Payment?.Id ?? payload.Transfer?.Id ?? "", Status = "Ignored" };
            if (payload.Payment != null) await ProcessPaymentAsync(payload, record);
            else if (payload.Transfer != null) await ProcessTransferAsync(payload, record);
            _context.GatewayWebhookEvents.Add(record);
            await _context.SaveChangesAsync();
            if (tx != null) await tx.CommitAsync();
            return Ok(new { success = true, receivedEvent = payload.Event });
        }
        catch (JsonException) { return BadRequest(new { message = "JSON inválido." }); }
        catch (ArgumentException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao processar webhook Asaas");
            return StatusCode(500, new { message = "Falha ao processar evento. Reenvio necessário." });
        }
    }
    private async Task ProcessPaymentAsync(AsaasWebhookPayloadDto payload, GatewayWebhookEvent record)
    {
        var payment = payload.Payment!;
        if (string.IsNullOrWhiteSpace(payment.Id)) throw new ArgumentException("Pagamento sem identificação.");
        var charge = await _context.GatewayCharges.FirstOrDefaultAsync(c => c.PaymentId == payment.Id);
        if (charge == null && payment.ExternalReference?.StartsWith("CHARGE_") == true && Guid.TryParse(payment.ExternalReference[7..], out var chargeId))
            charge = await _context.GatewayCharges.FindAsync(chargeId);
        if (charge == null) return;
        if (payment.ExternalReference != $"CHARGE_{charge.Id}" || payment.Value != charge.Value ||
            (charge.PaymentId != null && charge.PaymentId != payment.Id) || payment.BillingType != "PIX")
            throw new ArgumentException("Pagamento não corresponde à cobrança autorizada.");
        charge.PaymentId = payment.Id;
        if (payload.Event == "PAYMENT_RECEIVED")
        {
            if (payment.Status != "RECEIVED") throw new ArgumentException("Status do recebimento inválido.");
            if (charge.SettledAt != null) { record.Status = "Duplicate"; return; }
            if (payment.NetValue is < 0 || payment.NetValue > payment.Value) throw new ArgumentException("Valor líquido inválido.");
            if (charge.Kind == "Deposit")
            {
                var deposit = await _context.DepositRequests.FindAsync(charge.ReferenceId) ?? throw new ArgumentException("Depósito não encontrado.");
                if (deposit.Valor != charge.Value || deposit.InvestorId != charge.UserId) throw new ArgumentException("Depósito divergente.");
                deposit.GatewayReference = payment.Id;
                await _treasury.ConfirmDepositAsync(deposit.Id, payment.Id, "Asaas-Webhook");
            }
            else if (charge.Kind == "Installment") await new InstallmentPaymentService(_context).ApplyPaymentAsync(charge.ReferenceId, charge.Value, "Asaas-Webhook", LedgerAccount.ExternalGateway);
            else throw new ArgumentException("Tipo de cobrança inválido.");
            charge.NetValue = payment.NetValue; charge.SettledAt = DateTime.UtcNow; charge.Status = "RECEIVED"; charge.IsActive = false;
            if (payment.NetValue.HasValue) await RecordFeeAsync(charge.Value - payment.NetValue.Value, charge.Id, charge.UserId);
            record.Status = "Processed";
        }
        else if (payload.Event is "PAYMENT_REFUNDED" or "PAYMENT_PARTIALLY_REFUNDED" or "PAYMENT_REFUND_IN_PROGRESS" or "PAYMENT_CHARGEBACK_REQUESTED")
        {
            charge.Status = "NeedsReview";
            record.Status = "NeedsReview"; record.Detail = "Estorno recebido. Conciliar saldo e lançamentos antes de liberar novas saídas.";
        }
        else if (payload.Event == "PAYMENT_CONFIRMED")
        {
            // Pix CONFIRMED may be held for review. Credit only funds actually RECEIVED.
            if (charge.SettledAt == null) charge.Status = "CONFIRMED";
            record.Status = "Processed";
        }
        else if (payload.Event == "PAYMENT_DELETED" && charge.SettledAt == null)
        {
            charge.Status = "CANCELLED"; charge.IsActive = false;
            if (charge.Kind == "Deposit")
            {
                var deposit = await _context.DepositRequests.FindAsync(charge.ReferenceId);
                if (deposit != null) { deposit.Status = "Cancelled"; deposit.ProcessedAt = DateTime.UtcNow; }
            }
            record.Status = "Processed";
        }
    }
    private async Task CancelTargetChargeBeforePartialSettlementAsync(AsaasWebhookPayloadDto payload)
    {
        if (payload.Event != "PAYMENT_RECEIVED" || payload.Payment == null) return;
        var remote = payload.Payment;
        var charge = await _context.GatewayCharges.AsNoTracking().FirstOrDefaultAsync(c => c.PaymentId == remote.Id);
        if (charge == null && remote.ExternalReference?.StartsWith("CHARGE_") == true && Guid.TryParse(remote.ExternalReference[7..], out var id))
            charge = await _context.GatewayCharges.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (charge == null || charge.Kind != "Installment" || charge.SettledAt != null) return;
        if (remote.Status != "RECEIVED" || remote.BillingType != "PIX" || remote.Value != charge.Value || remote.ExternalReference != $"CHARGE_{charge.Id}" ||
            (charge.PaymentId != null && charge.PaymentId != remote.Id)) throw new ArgumentException("Pagamento não corresponde à cobrança autorizada.");
        var installment = await _context.LoanInstallments.AsNoTracking().FirstOrDefaultAsync(i => i.Id == charge.ReferenceId);
        if (installment == null || charge.Value >= installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago) return;
        var next = await _context.LoanInstallments.AsNoTracking().Where(i => i.LoanRequestId == installment.LoanRequestId && i.NumeroParcela > installment.NumeroParcela && i.Status != "Paga" && i.Status != "Cancelada")
            .OrderBy(i => i.NumeroParcela).FirstOrDefaultAsync();
        if (next == null) return;
        var nextCharge = await _context.GatewayCharges.FirstOrDefaultAsync(c => c.Kind == "Installment" && c.ReferenceId == next.Id && c.IsActive);
        if (nextCharge != null) await _workflow.CancelChargeAsync(nextCharge);
    }
    private async Task ProcessTransferAsync(AsaasWebhookPayloadDto payload, GatewayWebhookEvent record)
    {
        var remote = payload.Transfer!;
        if (string.IsNullOrWhiteSpace(remote.Id)) throw new ArgumentException("Transferência sem identificação.");
        var transfer = await _context.GatewayTransfers.FirstOrDefaultAsync(t => t.TransferId == remote.Id);
        if (transfer == null && remote.ExternalReference?.StartsWith("TRANSFER_") == true && Guid.TryParse(remote.ExternalReference[9..], out var id))
            transfer = await _context.GatewayTransfers.FindAsync(id);
        if (transfer == null) return;
        if (remote.ExternalReference != $"TRANSFER_{transfer.Id}" || remote.Value != transfer.Value ||
            (transfer.TransferId != null && transfer.TransferId != remote.Id)) throw new ArgumentException("Transferência não corresponde à autorização.");
        transfer.TransferId = remote.Id;
        if (payload.Event == "TRANSFER_DONE")
        {
            if (remote.Status != "DONE") throw new ArgumentException("Status da transferência inválido.");
            if (transfer.SettledAt != null) { record.Status = "Duplicate"; return; }
            if (transfer.Status is "FAILED" or "CANCELLED")
            {
                record.Status = "NeedsReview"; record.Detail = "Conclusão recebida depois de falha/cancelamento. Conciliar antes de liberar saídas.";
                return;
            }
            if (transfer.Kind == "Withdrawal")
            {
                var withdrawal = await _context.WithdrawalRequests.FindAsync(transfer.ReferenceId) ?? throw new ArgumentException("Saque não encontrado.");
                withdrawal.GatewayReference = remote.Id;
                await _treasury.ConfirmWithdrawalAsync(withdrawal.Id, remote.Id, "Asaas-Webhook");
            }
            else if (transfer.Kind == "Loan")
            {
                var loan = await _context.LoanRequests.FindAsync(transfer.ReferenceId) ?? throw new ArgumentException("Empréstimo não encontrado.");
                if (loan.UserId != transfer.UserId || loan.ValorSolicitado != transfer.Value || loan.Status != "DesembolsoPendente" ||
                    await _context.LedgerTransactions.AnyAsync(l => l.LoanId == loan.Id && l.TipoMovimentacao == LedgerTransactionType.LoanDisbursement && l.ContaDestino == LedgerAccount.BorrowerAccount))
                {
                    transfer.Status = "NeedsReview";
                    record.Status = "NeedsReview"; record.Detail = "Contrato ou estado diverge do desembolso confirmado. Conciliar saída antes de liberar movimentações.";
                    return;
                }
                var investments = await _context.Investments.Where(i => i.LoanId == loan.Id && i.Status == "Comprometido").ToListAsync();
                foreach (var investment in investments)
                {
                    var wallet = await _context.InvestorWallets.FindAsync(investment.InvestorId) ?? throw new ArgumentException("Carteira não encontrada.");
                    if (wallet.SaldoComprometido < investment.ValorInvestido) throw new InvalidOperationException("Reserva de investimento inconsistente.");
                    wallet.SaldoComprometido -= investment.ValorInvestido; wallet.SaldoInvestido += investment.ValorInvestido; wallet.DataAtualizacao = DateTime.UtcNow;
                    investment.Status = "Ativo";
                }
                loan.Status = "EmPagamento";
                await new LedgerService(_context).AddTransactionAsync(LedgerAccount.PlatformCash, LedgerAccount.BorrowerAccount, transfer.Value,
                    LedgerTransactionType.LoanDisbursement, "Loan", loan.Id, loanId: loan.Id, userId: loan.UserId, createdBy: "Asaas-Webhook");
                _context.FinancialEventHistories.Add(new FinancialEventHistory { Id = Guid.NewGuid(), Data = DateTime.UtcNow, UsuarioResponsavel = "Asaas-Webhook",
                    EventoExecutado = "DESEMBOLSO_CONFIRMADO", LoanRequestId = loan.Id, Valor = transfer.Value, Observacao = $"Transferência {remote.Id} concluída para o tomador cadastrado." });
            }
            transfer.Status = "DONE"; transfer.SettledAt = DateTime.UtcNow; transfer.Fee = remote.TransferFee ?? transfer.Fee;
            if (transfer.Fee is < 0) throw new ArgumentException("Tarifa inválida.");
            await RecordFeeAsync(transfer.Fee ?? 0, transfer.Id, transfer.UserId); record.Status = "Processed";
        }
        else if (payload.Event is "TRANSFER_FAILED" or "TRANSFER_CANCELLED")
        {
            if ((payload.Event == "TRANSFER_FAILED" && remote.Status != "FAILED") || (payload.Event == "TRANSFER_CANCELLED" && remote.Status != "CANCELLED"))
                throw new ArgumentException("Estado da falha/cancelamento não corresponde ao evento.");
            if (transfer.SettledAt != null) { record.Status = "NeedsReview"; record.Detail = "Falha após transferência liquidada. Conciliação necessária."; return; }
            if (transfer.Status is "FAILED" or "CANCELLED") { record.Status = "Duplicate"; return; }
            transfer.Status = payload.Event == "TRANSFER_FAILED" ? "FAILED" : "CANCELLED";
            transfer.IsActive = false;
            if (transfer.Kind == "Withdrawal")
            {
                var withdrawal = await _context.WithdrawalRequests.FindAsync(transfer.ReferenceId) ?? throw new ArgumentException("Saque não encontrado.");
                withdrawal.Status = "Failed";
                await _treasury.RejectWithdrawalAsync(withdrawal.Id, "Asaas-Webhook", "Transferência não concluída pelo gateway");
            }
            else
            {
                var loan = await _context.LoanRequests.FindAsync(transfer.ReferenceId);
                if (loan != null) loan.Status = "FalhaDesembolso";
            }
            record.Status = "Processed";
        }
    }
    private async Task RecordFeeAsync(decimal fee, Guid referenceId, Guid userId)
    {
        if (fee > 0) await new LedgerService(_context).AddTransactionAsync(LedgerAccount.PlatformCash, LedgerAccount.ExternalGateway,
            fee, LedgerTransactionType.GatewayFee, "GatewayFee", referenceId, userId: userId, createdBy: "Asaas-Webhook");
    }
    private IActionResult GatewayError(Exception ex)
    {
        if (ex is UnauthorizedAccessException) return StatusCode(403, new { message = "Acesso negado." });
        if (ex is ArgumentException) return BadRequest(new { message = ex.Message });
        _logger.LogWarning(ex, "Operação Asaas não concluída");
        return StatusCode(503, new { message = ex is InvalidOperationException ? ex.Message : "Gateway indisponível. Reabra a operação para conciliar o envio." });
    }
    private static object ChargeResponse(GatewayCharge charge) => new { chargeId = charge.Id, depositId = charge.Kind == "Deposit" ? (Guid?)charge.ReferenceId : null,
        installmentId = charge.Kind == "Installment" ? (Guid?)charge.ReferenceId : null, paymentId = charge.PaymentId, valor = charge.Value,
        qrCodeBase64 = charge.QrCodeBase64, copiaECola = charge.CopiaECola, invoiceUrl = charge.InvoiceUrl, expirationDate = charge.ExpirationDate,
        status = charge.Status, paid = charge.SettledAt.HasValue, message = charge.SettledAt.HasValue ? "Pagamento recebido e contabilizado." : "Aguardando confirmação do recebimento pelo Asaas." };
    private static object TransferResponse(GatewayTransfer transfer) => new { operationId = transfer.Id, transferId = transfer.TransferId, status = transfer.Status, valor = transfer.Value,
        chavePix = transfer.PixKey, tipoChavePix = transfer.PixKeyType, settledAt = transfer.SettledAt,
        message = transfer.Status == "DONE" ? "Transferência recebida e contabilizada." : "Transferência registrada. Liquidação depende da confirmação do Asaas." };
}
public class InstallmentPixRequest { public decimal? Valor { get; set; } }
public class LoanDisbursementRequest
{
    public string ChavePix { get; set; } = "";
    public string TipoChavePix { get; set; } = "CPF";
    public decimal ValorConfirmado { get; set; }
    public bool ConfirmacaoDestino { get; set; }
}
