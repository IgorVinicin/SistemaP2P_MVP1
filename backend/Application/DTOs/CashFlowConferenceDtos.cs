using System;
using System.Collections.Generic;

namespace backend.Application.DTOs
{
    public class CashFlowClosingConferenceDto
    {
        public int TotalParcelasPrevistas { get; set; }
        public int TotalParcelasPagas { get; set; }
        public int TotalParcelasParciais { get; set; }
        public int TotalParcelasEmDebito { get; set; }
        public decimal ValorTotalRecebido { get; set; }
        public decimal ValorAindaPendente { get; set; }
        public decimal PercentualAdimplencia { get; set; }
        public List<CashFlowClosingItemDto> Items { get; set; } = new List<CashFlowClosingItemDto>();
    }

    public class CashFlowClosingItemDto
    {
        public Guid InstallmentId { get; set; }
        public Guid LoanRequestId { get; set; }
        public string TomadorNome { get; set; } = string.Empty;
        public string TomadorCpf { get; set; } = string.Empty;
        public string TipoEmprestimo { get; set; } = string.Empty;
        public decimal ValorParcela { get; set; }
        public decimal Multa { get; set; }
        public decimal JurosMora { get; set; }
        public decimal ValorTotalDevido { get; set; }
        public decimal ValorPago { get; set; }
        public decimal ValorRestante { get; set; }
        public decimal PercentualPago { get; set; }
        public int NumeroParcela { get; set; }
        public int QuantidadeTotalParcelas { get; set; }
        public string NumeroParcelaFormatado { get; set; } = string.Empty;
        public DateTime DataVencimento { get; set; }
        public DateTime? DataPagamento { get; set; }
        public string StatusOriginal { get; set; } = string.Empty;
        public string StatusCalculado { get; set; } = string.Empty; // "Paga", "Parcialmente Paga", "Em Débito"
        public int DiasAtraso { get; set; }
    }
}
