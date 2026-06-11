using System;

namespace backend.Application.DTOs
{
    public class CreditRuleDto
    {
        public Guid? Id { get; set; }
        public int ScoreMin { get; set; }
        public int ScoreMax { get; set; }
        public decimal LimiteEmprestimo { get; set; }
        public decimal TaxaJuros { get; set; }
        public string NivelRisco { get; set; } = string.Empty;
        public bool Ativo { get; set; } = true;
    }

    public class CreditConfigDto
    {
        public int ScoreMinimoParaSolicitacao { get; set; }
        public decimal PercentualMultaAtraso { get; set; }
        public decimal PercentualJurosDiario { get; set; }
        public int DiasParaCobranca { get; set; }
        public int DiasParaCobrancaIntensiva { get; set; }
        public int DiasParaInadimplencia { get; set; }
    }
}
