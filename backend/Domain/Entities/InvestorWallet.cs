using System;

namespace backend.Domain.Entities
{
    public class InvestorWallet
    {
        public Guid Id { get; set; } // FK para User.Id
        public decimal SaldoDisponivel { get; set; } = 0;
        public decimal SaldoInvestido { get; set; } = 0;
        public decimal Rendimentos { get; set; } = 0;
        public decimal TotalAportado { get; set; } = 0;
        public decimal TotalSacado { get; set; } = 0;
        public string PerfilInvestimento { get; set; } = "Conservador"; // Conservador, Moderado, Agressivo
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;

        public User? User { get; set; }
    }
}
