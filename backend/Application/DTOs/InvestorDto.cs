using System;

namespace backend.Application.DTOs
{
    public class InvestorWalletDto
    {
        public decimal SaldoDisponivel { get; set; }
        public decimal SaldoInvestido { get; set; }
        public decimal Rendimentos { get; set; }
        public decimal TotalAportado { get; set; }
        public decimal TotalSacado { get; set; }
        public string PerfilInvestimento { get; set; } = string.Empty;
        public decimal ValorProtegido { get; set; }
        public decimal ValorEmRisco { get; set; }
        public decimal ValorPerdido { get; set; }
        public decimal CoberturasRecebidas { get; set; }
        public decimal PerdasDefinitivas { get; set; }
        public decimal CoberturasFundo { get; set; }
    }

    public class MakeDepositDto
    {
        public decimal Valor { get; set; }
    }

    public class MakeWithdrawDto
    {
        public decimal Valor { get; set; }
    }

    public class AdminWalletAdjustmentDto
    {
        public Guid InvestorId { get; set; }
        public decimal Valor { get; set; }
        public string Descricao { get; set; } = string.Empty;
    }

    public class UpdateInvestmentProfileDto
    {
        public string Perfil { get; set; } = string.Empty;
    }

    public class InvestorTransactionDto
    {
        public Guid Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public decimal SaldoAnterior { get; set; }
        public decimal SaldoPosterior { get; set; }
        public DateTime DataMovimentacao { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string UsuarioResponsavel { get; set; } = string.Empty;
    }
}
