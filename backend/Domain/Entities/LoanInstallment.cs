using System;
using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class LoanInstallment
    {
        public Guid Id { get; set; }
        [ConcurrencyCheck]
        public uint Version { get; set; }
        public Guid LoanRequestId { get; set; }
        public int NumeroParcela { get; set; }
        public decimal ValorParcela { get; set; }
        public decimal ValorPrincipal { get; set; }
        public decimal ValorJuros { get; set; }
        public DateTime DataVencimento { get; set; }
        public DateTime? DataPagamento { get; set; }
        public string Status { get; set; } = "Pendente"; // Pendente, Paga, Atrasada, Parcial, Cancelada
        public decimal ValorPago { get; set; } = 0;
        public decimal PrincipalPago { get; set; }
        public decimal JurosPagos { get; set; }
        public decimal MultaPaga { get; set; }
        public decimal JurosMoraPagos { get; set; }
        public decimal PrincipalDistribuido { get; set; }
        public decimal MultaCarregada { get; set; }
        public decimal JurosMoraCarregados { get; set; }
        public int DiasAtraso { get; set; } = 0;
        public decimal Multa { get; set; } = 0;
        public decimal JurosMora { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public LoanRequest? LoanRequest { get; set; }
    }
}
