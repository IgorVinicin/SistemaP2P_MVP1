using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using backend.Application.DTOs;
using backend.Application.Services;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using backend.Infrastructure;

namespace backend.API.Controllers
{
    [ApiController]
    public class AsaasGatewayController : ControllerBase
    {
        private readonly IAsaasService _asaasService;
        private readonly ITreasuryService _treasuryService;
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AsaasGatewayController> _logger;
        private readonly WalletDistributionService _walletDistributionService;
        private readonly DelinquencyService _delinquencyService;

        public AsaasGatewayController(
            IAsaasService asaasService,
            ITreasuryService treasuryService,
            AppDbContext context,
            IConfiguration configuration,
            ILogger<AsaasGatewayController> logger,
            WalletDistributionService walletDistributionService,
            DelinquencyService delinquencyService)
        {
            _asaasService = asaasService;
            _treasuryService = treasuryService;
            _context = context;
            _configuration = configuration;
            _logger = logger;
            _walletDistributionService = walletDistributionService;
            _delinquencyService = delinquencyService;
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(idClaim) || !Guid.TryParse(idClaim, out Guid id))
            {
                throw new UnauthorizedAccessException("Usuário não autenticado.");
            }
            return id;
        }

        // ==========================================
        // 1. DIAGNÓSTICO & TESTE DE CONECTIVIDADE
        // ==========================================
        [HttpGet("api/admin/gateway/test")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> TestGatewayConnection()
        {
            var result = await _asaasService.TestConnectionAsync();
            return Ok(result);
        }

        // ==========================================
        // 2. GERAÇÃO DE PIX PARA DEPÓSITO (INVESTIDOR)
        // ==========================================
        [HttpPost("api/investor/treasury/deposit-pix")]
        [Authorize(Roles = "Investidor")]
        public async Task<IActionResult> GenerateDepositPix([FromBody] CreateDepositDto request)
        {
            try
            {
                var userId = GetUserId();
                var user = await _context.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null) return NotFound(new { message = "Investidor não encontrado." });

                // 1. Cria a solicitação de depósito no banco
                var deposit = await _treasuryService.RequestDepositAsync(userId, request.Valor, request.Observacao);

                // 2. Se o gateway Asaas estiver configurado, gera cobrança e QR Code real
                if (_asaasService.IsConfigured)
                {
                    try
                    {
                        var pixData = await _asaasService.CreatePixDepositAsync(deposit, user);
                        deposit.GatewayReference = pixData.PaymentId;
                        await _context.SaveChangesAsync();

                        return Ok(new
                        {
                            depositId = deposit.Id,
                            paymentId = pixData.PaymentId,
                            valor = pixData.Value,
                            qrCodeBase64 = pixData.QrCodeBase64,
                            copiaECola = pixData.CopiaECola,
                            invoiceUrl = pixData.InvoiceUrl,
                            message = "Cobrança PIX gerada com sucesso no Asaas. Efetue o pagamento para compensação automática."
                        });
                    }
                    catch (Exception asaasEx)
                    {
                        _logger.LogWarning(asaasEx, "[AsaasGateway] Falha ao gerar PIX no Asaas, retornando modo simulado.");
                    }
                }

                // Fallback Mock (Ambiente de desenvolvimento/demonstração)
                return Ok(new
                {
                    depositId = deposit.Id,
                    paymentId = $"SIMULATED_{deposit.Id.ToString().Substring(0, 8)}",
                    valor = deposit.Valor,
                    qrCodeBase64 = "",
                    copiaECola = $"00020126580014br.gov.bcb.pix0136{deposit.Id}5204000053039865405{deposit.Valor:F2}5802BR5913SISTEMA P2P6009SAO PAULO62070503***6304ABCD",
                    invoiceUrl = "",
                    message = "Solicitação de depósito registrada. (Modo Simulação / Gateway em configuração)"
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ==========================================
        // 3. GERAÇÃO DE PIX PARA PARCELA (TOMADOR)
        // ==========================================
        [HttpPost("api/loans/installments/{id}/pix")]
        [Authorize]
        public async Task<IActionResult> GenerateInstallmentPix(Guid id)
        {
            try
            {
                var userId = GetUserId();
                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .ThenInclude(l => l!.User)
                    .ThenInclude(u => u!.Profile)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (installment == null || installment.LoanRequest == null)
                {
                    return NotFound(new { message = "Parcela não encontrada." });
                }

                bool isAdmin = User.IsInRole("Administrador");
                if (!isAdmin && installment.LoanRequest.UserId != userId)
                {
                    return StatusCode(403, new { message = "Acesso negado." });
                }

                if (installment.Status == "Paga")
                {
                    return BadRequest(new { message = "Esta parcela já está quitada." });
                }

                var borrower = installment.LoanRequest.User;
                if (borrower == null) return BadRequest(new { message = "Tomador não encontrado." });

                if (_asaasService.IsConfigured)
                {
                    var pixData = await _asaasService.CreatePixInstallmentPaymentAsync(installment, borrower);
                    return Ok(new
                    {
                        installmentId = installment.Id,
                        paymentId = pixData.PaymentId,
                        valor = pixData.Value,
                        qrCodeBase64 = pixData.QrCodeBase64,
                        copiaECola = pixData.CopiaECola,
                        invoiceUrl = pixData.InvoiceUrl,
                        message = "QR Code PIX gerado com sucesso para pagamento da parcela."
                    });
                }

                // Fallback Simulado
                decimal totalDevido = installment.ValorParcela + installment.Multa + installment.JurosMora - installment.ValorPago;
                return Ok(new
                {
                    installmentId = installment.Id,
                    paymentId = $"SIMULATED_INST_{installment.Id.ToString().Substring(0, 8)}",
                    valor = Math.Round(totalDevido, 2),
                    qrCodeBase64 = "",
                    copiaECola = $"00020126580014br.gov.bcb.pix0136{installment.Id}5204000053039865405{totalDevido:F2}5802BR5913SISTEMA P2P6009SAO PAULO62070503***6304WXYZ",
                    invoiceUrl = "",
                    message = "PIX simulado gerado para a parcela."
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // ==========================================
        // 4. RECEPTOR DE WEBHOOK ASAAS (CALLBACK)
        // ==========================================
        [HttpPost("api/webhooks/asaas")]
        [AllowAnonymous]
        public async Task<IActionResult> HandleAsaasWebhook()
        {
            try
            {
                // 1. Validação de Segurança do Token do Webhook
                var configuredToken = Environment.GetEnvironmentVariable("ASAAS_WEBHOOK_ACCESS_TOKEN")
                                      ?? _configuration["Asaas:WebhookAccessToken"];

                if (!string.IsNullOrEmpty(configuredToken) && !configuredToken.Contains("seu_token"))
                {
                    var headerToken = Request.Headers["asaas-access-token"].FirstOrDefault()
                                      ?? Request.Query["token"].FirstOrDefault();

                    if (string.IsNullOrEmpty(headerToken) || headerToken != configuredToken)
                    {
                        _logger.LogWarning("[AsaasWebhook] Tentativa de acesso não autorizada ao webhook. Token inválido.");
                        return Unauthorized(new { message = "Token de autenticação do webhook inválido." });
                    }
                }

                // 2. Leitura do Payload JSON
                using var reader = new StreamReader(Request.Body);
                var requestBody = await reader.ReadToEndAsync();

                if (string.IsNullOrWhiteSpace(requestBody))
                {
                    return BadRequest(new { message = "Payload vazio." });
                }

                var webhookData = JsonSerializer.Deserialize<AsaasWebhookPayloadDto>(requestBody, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (webhookData == null)
                {
                    return BadRequest(new { message = "Formato de payload inválido." });
                }

                _logger.LogInformation("[AsaasWebhook] Evento recebido: {Event}", webhookData.Event);

                // 3. Processamento de Eventos de Pagamento Recebido/Confirmado
                if (webhookData.Event == "PAYMENT_RECEIVED" || webhookData.Event == "PAYMENT_CONFIRMED")
                {
                    var payment = webhookData.Payment;
                    if (payment == null) return Ok(new { message = "Dados de pagamento não encontrados no evento." });

                    var externalRef = payment.ExternalReference ?? string.Empty;

                    // Caso A: Depósito de Investidor
                    if (externalRef.StartsWith("DEPOSIT_"))
                    {
                        var depositIdStr = externalRef.Replace("DEPOSIT_", "").Trim();
                        if (Guid.TryParse(depositIdStr, out var depositId))
                        {
                            var deposit = await _context.DepositRequests.FindAsync(depositId);
                            if (deposit != null && deposit.Status == "Pending")
                            {
                                await _treasuryService.ConfirmDepositAsync(depositId, payment.Id, "Asaas-Webhook");
                                _logger.LogInformation("[AsaasWebhook] Depósito {DepositId} confirmado automaticamente via Webhook.", depositId);
                            }
                        }
                    }
                    // Caso B: Pagamento de Parcela de Empréstimo
                    else if (externalRef.StartsWith("INSTALLMENT_"))
                    {
                        var instIdStr = externalRef.Replace("INSTALLMENT_", "").Trim();
                        if (Guid.TryParse(instIdStr, out var instId))
                        {
                            var installment = await _context.LoanInstallments
                                .Include(i => i.LoanRequest)
                                .FirstOrDefaultAsync(i => i.Id == instId);

                            if (installment != null && installment.Status != "Paga")
                            {
                                installment.Status = "Paga";
                                installment.DataPagamento = DateTime.UtcNow;
                                installment.ValorPago = payment.Value;
                                installment.UpdatedAt = DateTime.UtcNow;

                                // Dispara distribuição e amortização contábil
                                await _walletDistributionService.DistributeInstallmentPaymentAsync(installment.Id, "Asaas-Webhook");

                                // Atualiza status do empréstimo se todas as parcelas foram pagas
                                var remaining = await _context.LoanInstallments
                                    .CountAsync(i => i.LoanRequestId == installment.LoanRequestId && i.Id != installment.Id && i.Status != "Paga");

                                if (remaining == 0 && installment.LoanRequest != null)
                                {
                                    installment.LoanRequest.Status = "Finalizado";
                                    installment.LoanRequest.DataEncerramento = DateTime.UtcNow;
                                }

                                await _context.SaveChangesAsync();
                                _logger.LogInformation("[AsaasWebhook] Parcela {InstallmentId} quitada e distribuída com sucesso.", instId);
                            }
                        }
                    }
                }
                // 4. Evento de Parcela Vencida
                else if (webhookData.Event == "PAYMENT_OVERDUE")
                {
                    var payment = webhookData.Payment;
                    if (payment != null && !string.IsNullOrEmpty(payment.ExternalReference) && payment.ExternalReference.StartsWith("INSTALLMENT_"))
                    {
                        var instIdStr = payment.ExternalReference.Replace("INSTALLMENT_", "").Trim();
                        if (Guid.TryParse(instIdStr, out var instId))
                        {
                            var installment = await _context.LoanInstallments.FindAsync(instId);
                            if (installment != null && installment.Status == "Pendente")
                            {
                                installment.Status = "Atrasada";
                                installment.UpdatedAt = DateTime.UtcNow;
                                await _context.SaveChangesAsync();
                            }
                        }
                    }
                }

                return Ok(new { success = true, receivedEvent = webhookData.Event });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[AsaasWebhook] Erro no processamento do webhook");
                return StatusCode(500, new { message = "Erro interno ao processar webhook.", error = ex.Message });
            }
        }
    }
}
