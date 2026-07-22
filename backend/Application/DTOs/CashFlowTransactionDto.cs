using System;

namespace backend.Application.DTOs
{
    public class CashFlowTransactionDto
    {
        public Guid Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public int TipoMovimentacao { get; set; }
        public decimal Valor { get; set; }
        public string? UserName { get; set; }
        public string? CPF { get; set; }
        public string? Observacao { get; set; }
    }
}
