using System;
using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class InvestorWallet
    {
        public Guid Id { get; set; } // FK para User.Id
        public decimal SaldoDisponivel { get; set; } = 0;
        public decimal SaldoBloqueado { get; set; } = 0;
        public decimal SaldoComprometido { get; set; } = 0;
        public decimal SaldoInvestido { get; set; } = 0;
        public decimal Rendimentos { get; set; } = 0;
        public decimal TotalAportado { get; set; } = 0;
        public decimal TotalSacado { get; set; } = 0;
        public string PerfilInvestimento { get; set; } = "Conservador"; // Conservador, Moderado, Agressivo
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;
        public decimal Rentabilidade { get; set; } = 0;
        public string ObservacaoAdministrativa { get; set; } = string.Empty;
        
        [ConcurrencyCheck]
        public uint Version { get; set; } // Otimistic Concurrency Token

        public User? User { get; set; }
    }
}
