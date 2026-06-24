using System;
using System.Collections.Generic;

namespace backend.Application.DTOs
{
    // ==========================================
    // CONSOLIDATED REPORTS DTOs
    // ==========================================

    // 1. Borrower Consolidated DTOs
    public class BorrowerConsolidatedSummaryDto
    {
        public int TotalTomadores { get; set; }
        public int Ativos { get; set; }
        public int Inativos { get; set; }
        public int EmAnalise { get; set; }
    }

    public class BorrowerConsolidatedItemDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public decimal LimiteDisponivel { get; set; }
        public int QuantidadeEmprestimos { get; set; }
        public decimal ValorTotalSolicitado { get; set; }
        public decimal ValorTotalAprovado { get; set; }
    }

    public class BorrowerConsolidatedReportDto
    {
        public BorrowerConsolidatedSummaryDto Summary { get; set; } = new();
        public PagedResultDto<BorrowerConsolidatedItemDto> Details { get; set; } = new();
    }

    // 2. Investor Consolidated DTOs
    public class InvestorConsolidatedSummaryDto
    {
        public int TotalInvestidores { get; set; }
        public int Ativos { get; set; }
        public int EmAnalise { get; set; }
    }

    public class InvestorConsolidatedItemDto
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public decimal ValorInvestido { get; set; }
        public decimal Rentabilidade { get; set; }
        public decimal SaldoAtual { get; set; }
    }

    public class InvestorConsolidatedReportDto
    {
        public InvestorConsolidatedSummaryDto Summary { get; set; } = new();
        public PagedResultDto<InvestorConsolidatedItemDto> Details { get; set; } = new();
    }

    // 3. Loan Consolidated DTOs
    public class LoanConsolidatedSummaryDto
    {
        public int Solicitados { get; set; }
        public int Aprovados { get; set; }
        public int Reprovados { get; set; }
        public int EmAnalise { get; set; }
    }

    public class LoanConsolidatedItemDto
    {
        public Guid Id { get; set; }
        public string Tomador { get; set; } = string.Empty;
        public string TipoEmprestimo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public decimal Juros { get; set; }
        public int Parcelas { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }
    }

    public class LoanConsolidatedReportDto
    {
        public LoanConsolidatedSummaryDto Summary { get; set; } = new();
        public PagedResultDto<LoanConsolidatedItemDto> Details { get; set; } = new();
    }

    // 4. Financial Consolidated DTOs
    public class FinancialEntradasDto
    {
        public decimal ParcelasRecebidas { get; set; }
        public decimal JurosRecebidos { get; set; }
        public decimal TaxasAdministrativas { get; set; }
        public decimal TotalEntradas => ParcelasRecebidas + JurosRecebidos + TaxasAdministrativas;
    }

    public class FinancialSaidasDto
    {
        public decimal EmprestimosLiberados { get; set; }
        public decimal RendimentosPagos { get; set; }
        public decimal CoberturasAdministrativas { get; set; }
        public decimal TotalSaidas => EmprestimosLiberados + RendimentosPagos + CoberturasAdministrativas;
    }

    public class FinancialResumoDto
    {
        public decimal TotalRecebido { get; set; }
        public decimal TotalEmprestado { get; set; }
        public decimal TotalEmAtraso { get; set; }
        public decimal TotalInadimplente { get; set; }
        public decimal ResultadoOperacional { get; set; }
    }

    public class FinancialConsolidatedReportDto
    {
        public FinancialEntradasDto Entradas { get; set; } = new();
        public FinancialSaidasDto Saidas { get; set; } = new();
        public FinancialResumoDto Resumo { get; set; } = new();
    }


    // ==========================================
    // INDIVIDUAL REPORTS DTOs
    // ==========================================

    // 1. Individual Borrower DTO
    public class BorrowerIndividualReportDto
    {
        // Dados Cadastrais
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public string Empresa { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;

        // Crédito
        public decimal LimiteDisponivel { get; set; }
        public decimal LimiteUtilizado { get; set; }

        // Empréstimos
        public List<BorrowerIndividualLoanDto> Emprestimos { get; set; } = new();

        // Parcelas
        public List<BorrowerIndividualInstallmentDto> Parcelas { get; set; } = new();

        // Resumo
        public decimal TotalSolicitado { get; set; }
        public decimal TotalAprovado { get; set; }
        public decimal TotalPago { get; set; }
        public int ParcelasEmAberto { get; set; }
        public int ParcelasAtrasadas { get; set; }
    }

    public class BorrowerIndividualLoanDto
    {
        public Guid Id { get; set; }
        public decimal ValorSolicitado { get; set; }
        public decimal ValorAprovado { get; set; }
        public string TipoEmprestimo { get; set; } = string.Empty;
        public decimal Juros { get; set; }
        public int Parcelas { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class BorrowerIndividualInstallmentDto
    {
        public int Numero { get; set; }
        public decimal Valor { get; set; }
        public DateTime Vencimento { get; set; }
        public string Situacao { get; set; } = string.Empty;
        public DateTime? DataPagamento { get; set; }
    }

    // 2. Individual Investor DTO
    public class InvestorIndividualReportDto
    {
        // Dados Cadastrais
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

        // Investimentos
        public List<InvestorIndividualInvestmentDto> Investimentos { get; set; } = new();

        // Movimentações
        public List<InvestorIndividualTransactionDto> Movimentacoes { get; set; } = new();

        // Resumo
        public decimal TotalInvestido { get; set; }
        public decimal TotalRecebido { get; set; }
        public decimal SaldoAtual { get; set; }
    }

    public class InvestorIndividualInvestmentDto
    {
        public decimal ValorInvestido { get; set; }
        public decimal Rentabilidade { get; set; }
        public DateTime DataAprovacao { get; set; }
    }

    public class InvestorIndividualTransactionDto
    {
        public string Tipo { get; set; } = string.Empty;
        public decimal Valor { get; set; }
        public DateTime Data { get; set; }
        public string Descricao { get; set; } = string.Empty;
    }

    public class AllocationAuditItemDto
    {
        public Guid Id { get; set; }
        public string InvestorName { get; set; } = string.Empty;
        public Guid LoanId { get; set; }
        public decimal ValorInvestido { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
