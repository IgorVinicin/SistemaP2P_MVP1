using System;

namespace backend.Application.DTOs
{
    public class InvestmentProductDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal RentabilidadeMensal { get; set; }
        public decimal ValorMinimo { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string Condicoes { get; set; } = string.Empty;
        public string Risco { get; set; } = "Baixo";
        public string PrazoMinimo { get; set; } = "Livre";
        public bool Ativo { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }

    public class CreateInvestmentProductDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal RentabilidadeMensal { get; set; }
        public decimal ValorMinimo { get; set; } = 5000.00m;
        public string Descricao { get; set; } = string.Empty;
        public string Condicoes { get; set; } = string.Empty;
        public string Risco { get; set; } = "Baixo";
        public string PrazoMinimo { get; set; } = "Livre";
        public bool Ativo { get; set; } = true;
    }

    public class UpdateInvestmentProductDto
    {
        public string Nome { get; set; } = string.Empty;
        public decimal RentabilidadeMensal { get; set; }
        public decimal ValorMinimo { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string Condicoes { get; set; } = string.Empty;
        public string Risco { get; set; } = "Baixo";
        public string PrazoMinimo { get; set; } = "Livre";
        public bool Ativo { get; set; } = true;
    }
}
