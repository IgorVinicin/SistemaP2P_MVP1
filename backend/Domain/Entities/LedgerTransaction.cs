using System;

namespace backend.Domain.Entities
{
    public enum LedgerAccount
    {
        PlatformCash,
        InvestorWallet,
        ProtectionFund,
        PlatformRevenue,
        BorrowerAccount,
        ExternalGateway,
        AdministrativeAdjustment
    }

    public enum LedgerTransactionType
    {
        LoanDisbursement,
        InstallmentPayment,
        InvestorDeposit,
        InvestorWithdrawal,
        RevenueDistribution,
        ProtectionFundCredit,
        ProtectionFundDebit,
        AdministrativeAdjustment,
        InternalTransfer,
        ManualEntry
    }

    public class LedgerTransaction
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime TransactionDate { get; set; }
        public LedgerTransactionType TipoMovimentacao { get; set; }
        
        // Partidas Dobradas
        public LedgerAccount ContaOrigem { get; set; }
        public LedgerAccount ContaDestino { get; set; }
        public decimal Valor { get; set; }
        
        // Rastreabilidade Opcional
        public string? ReferenceType { get; set; } // "Loan", "Installment", "Deposit", etc.
        public Guid? ReferenceId { get; set; }
        public Guid? LoanId { get; set; }
        public Guid? InstallmentId { get; set; }
        public Guid? UserId { get; set; }
        
        public string? Observacao { get; set; }
        public string CreatedBy { get; set; } = "Sistema";
    }
}
