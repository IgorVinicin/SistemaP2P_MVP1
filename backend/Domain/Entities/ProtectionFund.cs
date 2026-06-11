using System;

namespace backend.Domain.Entities
{
    public class ProtectionFund
    {
        public Guid Id { get; set; }
        public decimal SaldoAtual { get; set; }
        public decimal TotalRecebido { get; set; } = 0;
        public decimal TotalUtilizado { get; set; } = 0;
        public DateTime UltimaAtualizacao { get; set; } = DateTime.UtcNow;
    }
}
