using System;

namespace backend.Application.DTOs
{
    public class DepositRequestDto
    {
        public Guid Id { get; set; }
        public Guid InvestorId { get; set; }
        public decimal Valor { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? GatewayReference { get; set; }
        public string? Observacao { get; set; }
    }

    public class CreateDepositDto
    {
        public decimal Valor { get; set; }
        public string? Observacao { get; set; }
    }
}
