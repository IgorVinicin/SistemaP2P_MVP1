using System;
using System.Collections.Generic;

namespace backend.Domain.Entities
{
    public class LoanRequest
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal ValorSolicitado { get; set; }
        public int Parcelas { get; set; } // Compatibilidade / parcelas
        public int QuantidadeParcelas { get; set; } // Novo campo solicitado
        public string Status { get; set; } = "Pendente"; // Pendente, Aprovado, Reprovado, EmCaptacao, Financiado, EmPagamento, Finalizado, Inadimplente
        public int ScoreNaSolicitacao { get; set; } // Compatibilidade
        public int ScoreNoMomento { get; set; } // Novo campo solicitado
        public string Finalidade { get; set; } = string.Empty;
        public string Observacoes { get; set; } = string.Empty;
        public decimal TaxaJurosAplicada { get; set; }
        public decimal TaxaJuros { get; set; }
        public decimal ValorTotal { get; set; }
        public string NivelRisco { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // Compatibilidade
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow; // Novo campo solicitado
        public DateTime DataSolicitacao { get; set; } = DateTime.UtcNow;
        public DateTime? DataAnalise { get; set; }
        public DateTime? DataAprovacao { get; set; }
        public DateTime? DataEncerramento { get; set; }
        public string ObservacaoAdmin { get; set; } = string.Empty;

        public User? User { get; set; }
        public ICollection<Investment> Investments { get; set; } = new List<Investment>();
    }
}
