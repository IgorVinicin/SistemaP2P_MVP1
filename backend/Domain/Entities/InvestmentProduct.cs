using System;

namespace backend.Domain.Entities
{
    public class InvestmentProduct
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal RentabilidadeMensal { get; set; } // % ao mês (ex: 2.50)
        public decimal ValorMinimo { get; set; } = 5000.00m;
        public string Descricao { get; set; } = string.Empty;
        public string Condicoes { get; set; } = string.Empty;
        public string Risco { get; set; } = "Baixo"; // Baixo, Moderado, Alto
        public string PrazoMinimo { get; set; } = "Livre"; // Livre, 30 dias, 60 dias, 90 dias, etc.
        public bool Ativo { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
