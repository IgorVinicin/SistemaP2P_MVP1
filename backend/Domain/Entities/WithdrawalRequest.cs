using System;
using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class WithdrawalRequest
    {
        public Guid Id { get; set; }
        public Guid InvestorId { get; set; }
        public decimal Valor { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Processing, Completed, Failed
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }
        
        // Identificador externo gerado por gateway futuro (ex: endToEndId do PIX)
        public string? GatewayReference { get; set; }
        public string? ChavePix { get; set; }
        public string? TipoChavePix { get; set; }
        public string? Observacao { get; set; }

        [ConcurrencyCheck]
        public uint Version { get; set; } // Otimistic Concurrency Token

        public User? Investor { get; set; }
    }
}
