using System;
using backend.Domain.Entities;

namespace backend.Domain.Entities
{
    public class InvestorTransaction
    {
        public Guid Id { get; set; }
        public Guid InvestorId { get; set; }
        public string Tipo { get; set; } = "APORTE"; // APORTE, INVESTIMENTO, RENDIMENTO, SAQUE, ESTORNO, AJUSTE_ADMIN
        public decimal Valor { get; set; }
        public decimal SaldoAnterior { get; set; } = 0;
        public decimal SaldoPosterior { get; set; } = 0;
        public DateTime DataMovimentacao { get; set; } = DateTime.UtcNow;
        public string Descricao { get; set; } = string.Empty;
        public string UsuarioResponsavel { get; set; } = "Sistema";

        public User? Investor { get; set; }
    }
}
