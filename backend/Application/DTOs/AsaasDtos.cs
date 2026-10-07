using System;
using System.Text.Json.Serialization;

namespace backend.Application.DTOs
{
    // --- Modelos de Resposta de Teste/Conectividade ---
    public class AsaasConnectionTestResultDto
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty; // Sandbox ou Produção
        public string BaseUrl { get; set; } = string.Empty;
        public decimal? Balance { get; set; }
        public string? AccountName { get; set; }
        public string? CpfCnpj { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    // --- Modelos de Criação e Retorno de Cliente Asaas ---
    public class AsaasCustomerRequestDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("cpfCnpj")]
        public string CpfCnpj { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Email { get; set; }

        [JsonPropertyName("phone")]
        public string? Phone { get; set; }

        [JsonPropertyName("mobilePhone")]
        public string? MobilePhone { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }

    public class AsaasCustomerResponseDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("cpfCnpj")]
        public string? CpfCnpj { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }
    }

    // --- Modelos de Criação de Cobrança PIX ---
    public class AsaasPaymentRequestDto
    {
        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty;

        [JsonPropertyName("billingType")]
        public string BillingType { get; set; } = "PIX";

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("dueDate")]
        public string DueDate { get; set; } = string.Empty; // yyyy-MM-dd

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }

    public class AsaasPaymentResponseDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public string Customer { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("netValue")]
        public decimal? NetValue { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("billingType")]
        public string BillingType { get; set; } = string.Empty;

        [JsonPropertyName("invoiceUrl")]
        public string? InvoiceUrl { get; set; }

        [JsonPropertyName("bankSlipUrl")]
        public string? BankSlipUrl { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
    }

    // --- Modelo de Retorno do QR Code PIX Asaas ---
    public class AsaasPixQrCodeResponseDto
    {
        [JsonPropertyName("encodedImage")]
        public string EncodedImage { get; set; } = string.Empty; // Base64 da imagem PNG

        [JsonPropertyName("payload")]
        public string Payload { get; set; } = string.Empty; // Código PIX Copia e Cola

        [JsonPropertyName("expirationDate")]
        public string? ExpirationDate { get; set; }
    }

    // --- Resumo Unificado retornado ao Frontend ---
    public class PixPaymentDetailsDto
    {
        public string PaymentId { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string QrCodeBase64 { get; set; } = string.Empty;
        public string CopiaECola { get; set; } = string.Empty;
        public string? InvoiceUrl { get; set; }
        public string Status { get; set; } = "PENDING";
        public DateTime? ExpirationDate { get; set; }
        public string ExternalReference { get; set; } = string.Empty;
    }

    // --- Modelos de Transferência PIX (Saque) ---
    public class AsaasTransferRequestDto
    {
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("pixAddressKey")]
        public string PixAddressKey { get; set; } = string.Empty;

        [JsonPropertyName("pixAddressKeyType")]
        public string PixAddressKeyType { get; set; } = "CPF"; // CPF, CNPJ, EMAIL, PHONE, EVP

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    public class AsaasTransferResponseDto
    {
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("netValue")]
        public decimal? NetValue { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("transferFee")]
        public decimal? TransferFee { get; set; }

        [JsonPropertyName("effectiveDate")]
        public string? EffectiveDate { get; set; }
    }

    // --- Modelos do Webhook Asaas ---
    public class AsaasWebhookPayloadDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("payment")]
        public AsaasWebhookPaymentDto? Payment { get; set; }

        [JsonPropertyName("transfer")]
        public AsaasWebhookTransferDto? Transfer { get; set; }
    }

    public class AsaasWebhookPaymentDto
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("customer")]
        public string? Customer { get; set; }

        [JsonPropertyName("value")]
        public decimal Value { get; set; }

        [JsonPropertyName("netValue")]
        public decimal? NetValue { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty; // RECEIVED, CONFIRMED, OVERDUE, etc.

        [JsonPropertyName("billingType")]
        public string? BillingType { get; set; }

        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }

        [JsonPropertyName("paymentDate")]
        public string? PaymentDate { get; set; }
    }

    public class AsaasWebhookTransferDto
    {
        [JsonPropertyName("externalReference")]
        public string? ExternalReference { get; set; }
        [JsonPropertyName("transferFee")]
        public decimal? TransferFee { get; set; }
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public decimal Value { get; set; }
    }
}
