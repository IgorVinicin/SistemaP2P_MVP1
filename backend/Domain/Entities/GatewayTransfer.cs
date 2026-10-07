using System.ComponentModel.DataAnnotations;
namespace backend.Domain.Entities;
public class GatewayTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string? TransferId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public Guid ReferenceId { get; set; }
    public Guid UserId { get; set; }
    public decimal Value { get; set; }
    public decimal? Fee { get; set; }
    public string PixKey { get; set; } = string.Empty;
    public string PixKeyType { get; set; } = string.Empty;
    public string Status { get; set; } = "Creating";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SettledAt { get; set; }
    [ConcurrencyCheck] public uint Version { get; set; }
}
