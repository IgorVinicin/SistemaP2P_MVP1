using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<UserProfile> UserProfiles { get; set; }
        public DbSet<CompanyProfile> CompanyProfiles { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Question> Questions { get; set; }
        public DbSet<AnswerOption> AnswerOptions { get; set; }
        public DbSet<UserAnswer> UserAnswers { get; set; }
        public DbSet<LoanRequest> LoanRequests { get; set; }
        public DbSet<Investment> Investments { get; set; }
        public DbSet<InvestorWallet> InvestorWallets { get; set; }
        public DbSet<ProtectionFund> ProtectionFunds { get; set; }
        public DbSet<ScoreCreditRule> ScoreCreditRules { get; set; }
        public DbSet<CreditConfig> CreditConfigs { get; set; }
        public DbSet<ScoreHistory> ScoreHistories { get; set; }
        public DbSet<InvestorTransaction> InvestorTransactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Relacionamento N:1 entre InvestorTransaction e User
            modelBuilder.Entity<InvestorTransaction>()
                .HasOne(it => it.Investor)
                .WithMany()
                .HasForeignKey(it => it.InvestorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<InvestorTransaction>()
                .Property(it => it.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.TotalAportado)
                .HasPrecision(18, 2);

            // Relacionamento 1:1 entre User e UserProfile (compartilhando PK)
            modelBuilder.Entity<UserProfile>()
                .HasKey(up => up.Id);

            modelBuilder.Entity<UserProfile>()
                .HasOne(up => up.User)
                .WithOne(u => u.Profile)
                .HasForeignKey<UserProfile>(up => up.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento 1:1 entre User e CompanyProfile (compartilhando PK)
            modelBuilder.Entity<CompanyProfile>()
                .HasKey(cp => cp.Id);

            modelBuilder.Entity<CompanyProfile>()
                .HasOne(cp => cp.User)
                .WithOne(u => u.Company)
                .HasForeignKey<CompanyProfile>(cp => cp.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento 1:1 entre User e InvestorWallet (compartilhando PK)
            modelBuilder.Entity<InvestorWallet>()
                .HasKey(iw => iw.Id);

            modelBuilder.Entity<InvestorWallet>()
                .HasOne(iw => iw.User)
                .WithOne(u => u.Wallet)
                .HasForeignKey<InvestorWallet>(iw => iw.Id)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento N:1 entre Document e User
            modelBuilder.Entity<Document>()
                .HasOne(d => d.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento N:1 entre AnswerOption e Question
            modelBuilder.Entity<AnswerOption>()
                .HasOne(ao => ao.Question)
                .WithMany(q => q.Options)
                .HasForeignKey(ao => ao.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento de UserAnswer
            modelBuilder.Entity<UserAnswer>()
                .HasOne(ua => ua.User)
                .WithMany(u => u.Answers)
                .HasForeignKey(ua => ua.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento N:1 entre LoanRequest e User
            modelBuilder.Entity<LoanRequest>()
                .HasOne(lr => lr.User)
                .WithMany(u => u.LoanRequests)
                .HasForeignKey(lr => lr.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Relacionamento de Investment
            modelBuilder.Entity<Investment>()
                .HasOne(i => i.Investor)
                .WithMany(u => u.Investments)
                .HasForeignKey(i => i.InvestorId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Investment>()
                .HasOne(i => i.Loan)
                .WithMany(lr => lr.Investments)
                .HasForeignKey(i => i.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Configurações de precisão decimal
            modelBuilder.Entity<CompanyProfile>()
                .Property(cp => cp.FaturamentoMensal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LoanRequest>()
                .Property(lr => lr.ValorSolicitado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Investment>()
                .Property(i => i.ValorInvestido)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.SaldoDisponivel)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.SaldoInvestido)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.Rendimentos)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ProtectionFund>()
                .Property(pf => pf.SaldoAtual)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ScoreCreditRule>()
                .Property(r => r.LimiteEmprestimo)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ScoreCreditRule>()
                .Property(r => r.TaxaJuros)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LoanRequest>()
                .Property(l => l.TaxaJurosAplicada)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LoanRequest>()
                .Property(l => l.TaxaJuros)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LoanRequest>()
                .Property(l => l.ValorTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.TotalSacado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorTransaction>()
                .Property(it => it.SaldoAnterior)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorTransaction>()
                .Property(it => it.SaldoPosterior)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ScoreHistory>()
                .HasOne(sh => sh.User)
                .WithMany()
                .HasForeignKey(sh => sh.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
