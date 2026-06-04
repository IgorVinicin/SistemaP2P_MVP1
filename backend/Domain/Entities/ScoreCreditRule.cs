using System;

namespace backend.Domain.Entities
{
    public class ScoreCreditRule
    {
        public Guid Id { get; set; }
        public int ScoreMin { get; set; }
        public int ScoreMax { get; set; }
        public decimal LimiteEmprestimo { get; set; }
        public decimal TaxaJuros { get; set; } // Em percentual, ex: 8.5 para 8.5%
        public string NivelRisco { get; set; } = string.Empty; // Alto, Medio, Baixo
        public bool Ativo { get; set; } = true;
    }
}
