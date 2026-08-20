namespace backend.Application.DTOs
{
    public class RequestLoanDto
    {
        public decimal ValorSolicitado { get; set; }
        public int QuantidadeParcelas { get; set; }
        public string Finalidade { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public bool SolicitaAumentoLimite { get; set; }
    }

    public class LoanEligibilityDto
    {
        public string Risco { get; set; } = string.Empty;
        public decimal TaxaJuros { get; set; }
        public decimal LimiteDisponivel { get; set; }
        public bool PodeSolicitar { get; set; }
        public string MensagemBlock { get; set; } = string.Empty;
        public int ParcelasPagas { get; set; }
        public int MinimoExigido { get; set; }
        public bool TemEmprestimoAtivo { get; set; }
    }

    public class AdminReproveLoanDto
    {
        public string Observacao { get; set; } = string.Empty;
    }

    public class ApproveLoanRequestDto
    {
        public string TipoEmprestimo { get; set; } = string.Empty; // Master, Plus, Gold
        public int QuantidadeParcelas { get; set; }
        public decimal TaxaJuros { get; set; }
        public string? PeriodicidadeGold { get; set; } // Semanal, Quinzenal, Mensal
        public decimal? ValorAprovado { get; set; }
        public Guid? InvestorGroupId { get; set; }
        public DateTime? DataPrimeiraParcela { get; set; }
    }
}
