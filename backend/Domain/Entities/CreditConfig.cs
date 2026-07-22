using System;

namespace backend.Domain.Entities
{
    public class CreditConfig
    {
        public Guid Id { get; set; }
        public int ScoreMinimoParaSolicitacao { get; set; } = 150; // Default
        public decimal PercentualMultaAtraso { get; set; } = 2.00m; // 2%
        public decimal PercentualJurosDiario { get; set; } = 0.033m; // 0.033%
        public int DiasParaCobranca { get; set; } = 16;
        public int DiasParaCobrancaIntensiva { get; set; } = 31;
        public int DiasParaInadimplencia { get; set; } = 61;
        public decimal ValorMinimoInvestimento { get; set; } = 5000.00m;
        public int MinimoParcelasPagasNovaSolicitacao { get; set; } = 4;
        public int MaxInvestidoresPorEmprestimo { get; set; } = 10;
        public decimal SpreadMinimoPlataforma { get; set; } = 1.00m; // 1%
        public decimal TaxaRendimentoPool { get; set; } = 2.00m; // Rendimento mensal base para o pool de liquidez
    }
}
