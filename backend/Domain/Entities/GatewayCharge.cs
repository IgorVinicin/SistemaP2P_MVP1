using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities;

// A durable link between a locally authorized amount and a gateway payment.
public class GatewayCharge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? PaymentId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public Guid UserId { get; set; }
    public decimal Value { get; set; }
    public decimal? NetValue { get; set; }
    public string Status { get; set; } = "Creating";
    public bool IsActive { get; set; } = true;
    public string? QrCodeBase64 { get; set; }
    public string? CopiaECola { get; set; }
    public string? InvoiceUrl { get; set; }
    public DateTime? ExpirationDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }
    [ConcurrencyCheck] public uint Version { get; set; }
}

public class GatewayWebhookEvent
{
    [Key] public string EventId { get; set; } = string.Empty;
    public string Event { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;
    public string Status { get; set; } = "Processed";
    public string? Detail { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}
