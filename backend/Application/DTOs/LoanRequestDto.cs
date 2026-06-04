namespace backend.Application.DTOs
{
    public class RequestLoanDto
    {
        public decimal ValorSolicitado { get; set; }
        public int QuantidadeParcelas { get; set; }
        public string Finalidade { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
    }

    public class LoanEligibilityDto
    {
        public int ScoreAtual { get; set; }
        public string Risco { get; set; } = string.Empty;
        public decimal TaxaJuros { get; set; }
        public decimal LimiteDisponivel { get; set; }
        public bool PodeSolicitar { get; set; }
        public string MensagemBlock { get; set; } = string.Empty;
    }

    public class AdminReproveLoanDto
    {
        public string Observacao { get; set; } = string.Empty;
    }
}
