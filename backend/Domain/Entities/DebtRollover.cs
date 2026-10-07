namespace backend.Domain.Entities;

public class DebtRollover
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SourceInstallmentId { get; set; }
    public Guid TargetInstallmentId { get; set; }
    public decimal Principal { get; set; }
    public decimal Interest { get; set; }
    public decimal Penalty { get; set; }
    public decimal LateInterest { get; set; }
    public string Status { get; set; } = "PendingCancellation";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AppliedAt { get; set; }
    public string Actor { get; set; } = "Sistema";
}
