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
        public DbSet<LoanInstallment> LoanInstallments { get; set; }
        public DbSet<FinancialEventHistory> FinancialEventHistories { get; set; }
        public DbSet<PlatformRevenue> PlatformRevenues { get; set; }
        public DbSet<LedgerTransaction> LedgerTransactions { get; set; }
        public DbSet<CashFlowSnapshot> CashFlowSnapshots { get; set; }
        public DbSet<DepositRequest> DepositRequests { get; set; }
        public DbSet<WithdrawalRequest> WithdrawalRequests { get; set; }
        public DbSet<InvestorGroup> InvestorGroups { get; set; }
        public DbSet<InvestorGroupMember> InvestorGroupMembers { get; set; }
        public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
        public DbSet<LimitRequest> LimitRequests { get; set; }
        public DbSet<InvestmentProduct> InvestmentProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<IdempotencyRecord>()
                .HasIndex(ir => ir.IdempotencyKey)
                .IsUnique();

            modelBuilder.Entity<InvestorGroupMember>()
                .HasKey(igm => new { igm.GroupId, igm.InvestorId });

            modelBuilder.Entity<InvestorGroupMember>()
                .HasOne(igm => igm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(igm => igm.GroupId);

            modelBuilder.Entity<InvestorGroupMember>()
                .HasOne(igm => igm.Investor)
                .WithMany()
                .HasForeignKey(igm => igm.InvestorId);

            // Relacionamento N:1 entre InvestorTransaction e User
            modelBuilder.Entity<InvestorTransaction>()
                .HasOne(it => it.Investor)
                .WithMany()
                .HasForeignKey(it => it.InvestorId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .OnDelete(DeleteBehavior.Restrict);

            // Relacionamento N:1 entre Document e User
            modelBuilder.Entity<Document>()
                .HasOne(d => d.User)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.Restrict);

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
                .OnDelete(DeleteBehavior.Restrict);

            // Relacionamento N:1 entre LoanRequest e User
            modelBuilder.Entity<LoanRequest>()
                .HasOne(lr => lr.User)
                .WithMany(u => u.LoanRequests)
                .HasForeignKey(lr => lr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relacionamento de Investment
            modelBuilder.Entity<Investment>()
                .HasOne(i => i.Investor)
                .WithMany(u => u.Investments)
                .HasForeignKey(i => i.InvestorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Investment>()
                .HasOne(i => i.Loan)
                .WithMany(lr => lr.Investments)
                .HasForeignKey(i => i.LoanId)
                .OnDelete(DeleteBehavior.Restrict);

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

            modelBuilder.Entity<Investment>()
                .Property(i => i.SaldoPrincipalPendente)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.SaldoDisponivel)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.SaldoComprometido)
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

            modelBuilder.Entity<ProtectionFund>()
                .Property(pf => pf.TotalRecebido)
                .HasPrecision(18, 2);

            modelBuilder.Entity<ProtectionFund>()
                .Property(pf => pf.TotalUtilizado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<FinancialEventHistory>()
                .Property(fe => fe.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<PlatformRevenue>()
                .Property(pr => pr.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LedgerTransaction>()
                .Property(lt => lt.Valor)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalEntradas)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalSaidas)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.SaldoOperacional)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.ReceitaPlataforma)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalDistribuidoInvestidores)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalCobertoProtectionFund)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalEmprestado)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CashFlowSnapshot>()
                .Property(c => c.TotalRecebidoParcelas)
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

            modelBuilder.Entity<LoanRequest>()
                .Property(l => l.LimiteNoMomentoSolicitacao)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.TotalSacado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorWallet>()
                .Property(iw => iw.Rentabilidade)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorTransaction>()
                .Property(it => it.SaldoAnterior)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestorTransaction>()
                .Property(it => it.SaldoPosterior)
                .HasPrecision(18, 2);

            // Mapeamentos de LimitRequest e LimitePersonalizado
            modelBuilder.Entity<UserProfile>()
                .Property(up => up.LimitePersonalizado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LimitRequest>()
                .Property(lr => lr.LimiteSolicitado)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LimitRequest>()
                .Property(lr => lr.LimiteAtualNoMomento)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestmentProduct>()
                .Property(ip => ip.RentabilidadeMensal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<InvestmentProduct>()
                .Property(ip => ip.ValorMinimo)
                .HasPrecision(18, 2);

            modelBuilder.Entity<LimitRequest>()
                .HasOne(lr => lr.User)
                .WithMany()
                .HasForeignKey(lr => lr.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ScoreHistory>()
                .HasOne(sh => sh.User)
                .WithMany()
                .HasForeignKey(sh => sh.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Relacionamento N:1 entre LoanInstallment e LoanRequest
            modelBuilder.Entity<LoanInstallment>()
                .HasOne(li => li.LoanRequest)
                .WithMany()
                .HasForeignKey(li => li.LoanRequestId)
                .OnDelete(DeleteBehavior.Restrict);

            // Precisões decimais
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.ValorParcela)
                .HasPrecision(18, 2);
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.ValorPrincipal)
                .HasPrecision(18, 2);
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.ValorJuros)
                .HasPrecision(18, 2);
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.ValorPago)
                .HasPrecision(18, 2);
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.Multa)
                .HasPrecision(18, 2);
            modelBuilder.Entity<LoanInstallment>()
                .Property(li => li.JurosMora)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CreditConfig>()
                .Property(cc => cc.PercentualMultaAtraso)
                .HasPrecision(18, 2);
            modelBuilder.Entity<CreditConfig>()
                .Property(cc => cc.PercentualJurosDiario)
                .HasPrecision(18, 4);

            modelBuilder.Entity<CreditConfig>()
                .Property(cc => cc.ValorMinimoInvestimento)
                .HasPrecision(18, 2);

            modelBuilder.Entity<CreditConfig>()
                .Property(cc => cc.SpreadMinimoPlataforma)
                .HasPrecision(18, 2);

            // =================ÍNDICES CONFIGURADOS PARA PERFORMANCE=================
            
            // Índices em LoanInstallment (Status, DataVencimento, LoanRequestId)
            modelBuilder.Entity<LoanInstallment>()
                .HasIndex(li => li.LoanRequestId);
            modelBuilder.Entity<LoanInstallment>()
                .HasIndex(li => new { li.Status, li.DataVencimento });

            // Índices em LoanRequest (UserId, Status)
            modelBuilder.Entity<LoanRequest>()
                .HasIndex(lr => lr.UserId);
            modelBuilder.Entity<LoanRequest>()
                .HasIndex(lr => lr.Status);

            // Índices em InvestorTransaction (InvestorId, DataMovimentacao)
            modelBuilder.Entity<InvestorTransaction>()
                .HasIndex(it => new { it.InvestorId, it.DataMovimentacao });

            // Índices em Investment (InvestorId, LoanId)
            modelBuilder.Entity<Investment>()
                .HasIndex(i => i.InvestorId);
            modelBuilder.Entity<Investment>()
                .HasIndex(i => i.LoanId);

            // Índices em FinancialEventHistory (EventoExecutado, Data)
            modelBuilder.Entity<FinancialEventHistory>()
                .HasIndex(fe => new { fe.EventoExecutado, fe.Data });

            // Índices em PlatformRevenue
            modelBuilder.Entity<PlatformRevenue>()
                .HasIndex(pr => new { pr.TipoReceita, pr.DataCriacao });
            modelBuilder.Entity<PlatformRevenue>()
                .HasIndex(pr => pr.LoanId);

            // Índices em LedgerTransaction
            modelBuilder.Entity<LedgerTransaction>()
                .HasIndex(lt => lt.TransactionDate);
            modelBuilder.Entity<LedgerTransaction>()
                .HasIndex(lt => lt.ContaOrigem);
            modelBuilder.Entity<LedgerTransaction>()
                .HasIndex(lt => lt.ContaDestino);
            modelBuilder.Entity<LedgerTransaction>()
                .HasIndex(lt => lt.ReferenceId);

            // Índices em CashFlowSnapshot
            modelBuilder.Entity<CashFlowSnapshot>()
                .HasIndex(c => c.SnapshotDate);
        }
    }
}
