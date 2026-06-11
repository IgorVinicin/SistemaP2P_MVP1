using System;

namespace backend.Domain.Entities
{
    public class FinancialEventHistory
    {
        public Guid Id { get; set; }
        public DateTime Data { get; set; } = DateTime.UtcNow;
        public string UsuarioResponsavel { get; set; } = "Sistema";
        public string Observacao { get; set; } = string.Empty;
        public string EventoExecutado { get; set; } = string.Empty; // ex: MUDANCA_STATUS, ENTRADA_COBRANCA, ENTRADA_INADIMPLENCIA, UTILIZACAO_FUNDO, PERDA_INVESTIDOR, INTERVENCAO_ADMIN
        public Guid? LoanRequestId { get; set; }
        public decimal? Valor { get; set; }
    }
}
