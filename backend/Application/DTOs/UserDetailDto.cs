using System;
using System.Collections.Generic;

namespace backend.Application.DTOs
{
    public class UserDetailDto
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        public ProfileDetailDto? Profile { get; set; }
        public CompanyDetailDto? Company { get; set; }
        public List<DocumentDetailDto> Documents { get; set; } = new List<DocumentDetailDto>();
        public List<UserAnswerDetailDto> Answers { get; set; } = new List<UserAnswerDetailDto>();
        public List<UserLoanDetailDto> Loans { get; set; } = new List<UserLoanDetailDto>();
        public List<UserScoreHistoryDto> ScoreHistory { get; set; } = new List<UserScoreHistoryDto>();
    }

    public class UserScoreHistoryDto
    {
        public DateTime DataAlteracao { get; set; }
        public int ScoreAnterior { get; set; }
        public int ScoreNovo { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
    }

    public class UserAnswerDetailDto
    {
        public string Pergunta { get; set; } = string.Empty;
        public string RespostaSelecionada { get; set; } = string.Empty;
        public int Pontuacao { get; set; }
    }

    public class UserLoanDetailDto
    {
        public Guid Id { get; set; }
        public decimal ValorSolicitado { get; set; }
        public decimal ValorTotal { get; set; }
        public int QuantidadeParcelas { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DataSolicitacao { get; set; }
        public DateTime? DataAprovacao { get; set; }
        public DateTime? DataEncerramento { get; set; }
    }

    public class ProfileDetailDto
    {
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
        public int ScoreAtual { get; set; }
        public bool PossuiEmpresa { get; set; }
    }

    public class CompanyDetailDto
    {
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;
        public int TempoEmpresa { get; set; }
        public decimal FaturamentoMensal { get; set; }
    }

    public class DocumentDetailDto
    {
      public Guid Id { get; set; }
      public string TipoDocumento { get; set; } = string.Empty;
      public string Status { get; set; } = string.Empty;
      public DateTime CreatedAt { get; set; }
    }

    public class AdminUpdateProfileDto
    {
      public string Nome { get; set; } = string.Empty;
      public string CPF { get; set; } = string.Empty;
      public string Telefone { get; set; } = string.Empty;
      public string DataNascimento { get; set; } = string.Empty;
    }
}
