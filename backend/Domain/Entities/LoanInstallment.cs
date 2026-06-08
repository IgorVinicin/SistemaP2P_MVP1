using System;

namespace backend.Domain.Entities
{
    public class LoanInstallment
    {
        public Guid Id { get; set; }
        public Guid LoanRequestId { get; set; }
        public int NumeroParcela { get; set; }
        public decimal ValorParcela { get; set; }
        public decimal ValorPrincipal { get; set; }
        public decimal ValorJuros { get; set; }
        public DateTime DataVencimento { get; set; }
        public DateTime? DataPagamento { get; set; }
        public string Status { get; set; } = "Pendente"; // Pendente, Paga, Atrasada, Parcial, Cancelada
        public decimal ValorPago { get; set; } = 0;
        public int DiasAtraso { get; set; } = 0;
        public decimal Multa { get; set; } = 0;
        public decimal JurosMora { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public LoanRequest? LoanRequest { get; set; }
    }
}
