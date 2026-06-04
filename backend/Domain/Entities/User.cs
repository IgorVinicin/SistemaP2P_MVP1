using System;

namespace backend.Domain.Entities
{
    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = "Cliente"; // Cliente, Investidor, Administrador
        public string Status { get; set; } = "Pendente"; // Pendente, EmAnalise, Aprovado, Reprovado
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Relacionamentos
        public UserProfile? Profile { get; set; }
        public CompanyProfile? Company { get; set; }
        public InvestorWallet? Wallet { get; set; }
        public ICollection<Document> Documents { get; set; } = new List<Document>();
        public ICollection<UserAnswer> Answers { get; set; } = new List<UserAnswer>();
        public ICollection<LoanRequest> LoanRequests { get; set; } = new List<LoanRequest>();
        public ICollection<Investment> Investments { get; set; } = new List<Investment>();
    }
}
