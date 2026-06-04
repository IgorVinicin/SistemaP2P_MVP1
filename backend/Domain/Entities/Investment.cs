using System;

namespace backend.Domain.Entities
{
    public class Investment
    {
        public Guid Id { get; set; }
        public Guid InvestorId { get; set; }
        public Guid LoanId { get; set; }
        public decimal ValorInvestido { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public User? Investor { get; set; }
        public LoanRequest? Loan { get; set; }
    }
}
