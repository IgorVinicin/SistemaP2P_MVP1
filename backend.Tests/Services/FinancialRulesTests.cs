using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.Services
{
    public class FinancialRulesTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var context = new AppDbContext(options);

            context.CreditConfigs.Add(new CreditConfig
            {
                Id = Guid.NewGuid(),
                ValorMinimoInvestimento = 1000.00m,
                TaxaRendimentoPool = 2.00m
            });
            context.SaveChanges();
            return context;
        }

        [Fact]
        public async Task SolicitacaoSaque_DeveBloquearSaldoDisponivel_SemIncrementarTotalSacado()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var treasuryService = new TreasuryService(context);

            var investorId = Guid.NewGuid();
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 10000.00m,
                SaldoBloqueado = 0m,
                TotalSacado = 0m
            };
            context.InvestorWallets.Add(wallet);
            await context.SaveChangesAsync();

            // Act: Investidor solicita saque de R$ 3.000,00
            var withdrawal = await treasuryService.RequestWithdrawalAsync(investorId, 3000.00m, "Saque para despesas", "12345678900", "CPF");

            // Assert
            var updatedWallet = await context.InvestorWallets.FindAsync(investorId);
            Assert.NotNull(withdrawal);
            Assert.Equal("Pending", withdrawal.Status);
            Assert.Equal(7000.00m, updatedWallet!.SaldoDisponivel); // Debitado do disponível
            Assert.Equal(3000.00m, updatedWallet.SaldoBloqueado);   // Reservado no bloqueado
            Assert.Equal(0m, updatedWallet.TotalSacado);             // Ainda não concluído
        }

        [Fact]
        public async Task AprovacaoSaque_DeveDeduzirSaldoBloqueado_EIncrementarTotalSacado()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var treasuryService = new TreasuryService(context);

            var investorId = Guid.NewGuid();
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 7000.00m,
                SaldoBloqueado = 3000.00m,
                TotalSacado = 0m
            };
            context.InvestorWallets.Add(wallet);

            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                InvestorId = investorId,
                Valor = 3000.00m,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            context.WithdrawalRequests.Add(withdrawal);
            await context.SaveChangesAsync();

            // Act: Admin confirma/liquida o saque
            var success = await treasuryService.ConfirmWithdrawalAsync(withdrawal.Id, "PIX-GATEWAY-123", "AdminTest");

            // Assert
            var updatedWallet = await context.InvestorWallets.FindAsync(investorId);
            var updatedReq = await context.WithdrawalRequests.FindAsync(withdrawal.Id);

            Assert.True(success);
            Assert.Equal("Completed", updatedReq!.Status);
            Assert.Equal(7000.00m, updatedWallet!.SaldoDisponivel); // Permanece o restante livre
            Assert.Equal(0m, updatedWallet.SaldoBloqueado);          // Baixado do bloqueado
            Assert.Equal(3000.00m, updatedWallet.TotalSacado);       // Total sacado incrementado
        }

        [Fact]
        public async Task RecusaSaque_DeveRestituirSaldoBloqueado_ParaSaldoDisponivel()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var treasuryService = new TreasuryService(context);

            var investorId = Guid.NewGuid();
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 5000.00m,
                SaldoBloqueado = 2000.00m,
                TotalSacado = 0m
            };
            context.InvestorWallets.Add(wallet);

            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                InvestorId = investorId,
                Valor = 2000.00m,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };
            context.WithdrawalRequests.Add(withdrawal);
            await context.SaveChangesAsync();

            // Act: Admin recusa a solicitação
            var success = await treasuryService.RejectWithdrawalAsync(withdrawal.Id, "AdminTest", "Chave PIX inválida");

            // Assert
            var updatedWallet = await context.InvestorWallets.FindAsync(investorId);
            var updatedReq = await context.WithdrawalRequests.FindAsync(withdrawal.Id);

            Assert.True(success);
            Assert.Equal("Rejected", updatedReq!.Status);
            Assert.Equal(7000.00m, updatedWallet!.SaldoDisponivel); // 5000 + 2000 estornado
            Assert.Equal(0m, updatedWallet.SaldoBloqueado);          // Zerou bloqueado
            Assert.Equal(0m, updatedWallet.TotalSacado);             // Não sacou
        }

        [Fact]
        public async Task Saque_SaldoInsuficiente_DeveLancarExcecao()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var treasuryService = new TreasuryService(context);

            var investorId = Guid.NewGuid();
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 500.00m,
                SaldoBloqueado = 0m
            };
            context.InvestorWallets.Add(wallet);
            await context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() =>
                treasuryService.RequestWithdrawalAsync(investorId, 1000.00m, "Saque maior que saldo"));

            Assert.Contains("insuficiente", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Saque_NaoPermitirAprovacaoOuRecusaDuplicada()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var treasuryService = new TreasuryService(context);

            var investorId = Guid.NewGuid();
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 1000.00m,
                SaldoBloqueado = 1000.00m
            };
            context.InvestorWallets.Add(wallet);

            var withdrawal = new WithdrawalRequest
            {
                Id = Guid.NewGuid(),
                InvestorId = investorId,
                Valor = 1000.00m,
                Status = "Completed", // Já liquidado
                CreatedAt = DateTime.UtcNow
            };
            context.WithdrawalRequests.Add(withdrawal);
            await context.SaveChangesAsync();

            // Act & Assert: Tentar recusar ou confirmar novamente deve lançar exceção
            await Assert.ThrowsAsync<Exception>(() =>
                treasuryService.ConfirmWithdrawalAsync(withdrawal.Id, "GATEWAY-DUP", "Admin"));

            await Assert.ThrowsAsync<Exception>(() =>
                treasuryService.RejectWithdrawalAsync(withdrawal.Id, "Admin", "Tentativa de recusar saque já pago"));
        }

        [Fact]
        public async Task Rentabilidade_Dual_CalculaSeparadamenteDisponivelEComprometido()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var yieldService = new InvestorYieldService(context);

            var investor = new User
            {
                Id = Guid.NewGuid(),
                Email = "investor.dual@test.com",
                PasswordHash = "hash",
                Role = "Investidor",
                Status = "Aprovado"
            };
            context.Users.Add(investor);

            var wallet = new InvestorWallet
            {
                Id = investor.Id,
                SaldoDisponivel = 10000.00m,                // Remunerado a Taxa Y (1.00%) = R$ 100,00
                SaldoComprometido = 20000.00m,              // Remunerado a Taxa X (2.50%) = R$ 500,00
                SaldoInvestido = 0m,
                SaldoBloqueado = 5000.00m,                  // Não gera rendimento (R$ 0,00)
                RentabilidadeSaldoDisponivel = 1.00m,       // Taxa Y
                RentabilidadeSaldoComprometido = 2.50m      // Taxa X
            };
            context.InvestorWallets.Add(wallet);
            await context.SaveChangesAsync();

            // Act
            var (processed, totalYield) = await yieldService.ProcessMonthlyYieldAsync("TestRunner");

            // Assert
            var updatedWallet = await context.InvestorWallets.FindAsync(investor.Id);
            Assert.Equal(1, processed);
            Assert.Equal(600.00m, totalYield); // 100 + 500 = 600
            Assert.Equal(10600.00m, updatedWallet!.SaldoDisponivel); // Creditado no disponível
            Assert.Equal(600.00m, updatedWallet.Rendimentos);
        }

        [Fact]
        public async Task RentabilidadeDiaria_CreditaExatamenteProporcionalAoSaldoDisponivel()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var yieldService = new InvestorYieldService(context);

            var investor = new User
            {
                Id = Guid.NewGuid(),
                Email = "investor.daily@test.com",
                PasswordHash = "hash",
                Role = "Investidor",
                Status = "Aprovado"
            };
            context.Users.Add(investor);

            var wallet = new InvestorWallet
            {
                Id = investor.Id,
                SaldoDisponivel = 30000.00m,           // R$ 30.000,00 a 1.00% a.m. -> R$ 300/mês -> R$ 10,00 por dia
                SaldoComprometido = 0m,
                RentabilidadeSaldoDisponivel = 1.00m
            };
            context.InvestorWallets.Add(wallet);
            await context.SaveChangesAsync();

            // Act: Processa o rendimento diário de hoje
            var (processed, totalYield) = await yieldService.ProcessDailyYieldAsync("RoboDiario");

            // Assert
            var updatedWallet = await context.InvestorWallets.FindAsync(investor.Id);
            Assert.Equal(1, processed);
            Assert.Equal(10.00m, totalYield); // 30.000 * (1% / 30) = 10,00
            Assert.Equal(30010.00m, updatedWallet!.SaldoDisponivel); // Saldo aumentou R$ 10,00 no dia
            Assert.Equal(10.00m, updatedWallet.Rendimentos);

            // Act 2: Tentar rodar novamente no mesmo dia não deve duplicar o crédito
            var (processed2, totalYield2) = await yieldService.ProcessDailyYieldAsync("RoboDiario");
            Assert.Equal(0, processed2);
            Assert.Equal(0m, totalYield2);
        }

        [Fact]
        public async Task Emprestimo_SemGrupo_NaoDescontaInvestidores()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var distService = new WalletDistributionService(context);

            var borrower = new User
            {
                Id = Guid.NewGuid(),
                Email = "tomador@test.com",
                PasswordHash = "hash",
                Role = "Cliente",
                Status = "Aprovado"
            };
            context.Users.Add(borrower);

            var loan = new LoanRequest
            {
                Id = Guid.NewGuid(),
                UserId = borrower.Id,
                ValorSolicitado = 5000.00m,
                Status = "Aprovado",
                TipoEmprestimo = "Gold",
                Periodicidade = "Mensal",
                TaxaJuros = 5.00m,
                QuantidadeParcelas = 1
            };
            context.LoanRequests.Add(loan);

            var installment = new LoanInstallment
            {
                Id = Guid.NewGuid(),
                LoanRequestId = loan.Id,
                NumeroParcela = 1,
                ValorParcela = 5250.00m,
                ValorPrincipal = 5000.00m,
                ValorJuros = 250.00m,
                ValorPago = 5250.00m,
                DataVencimento = DateTime.UtcNow,
                DataPagamento = DateTime.UtcNow,
                Status = "Paga"
            };
            context.LoanInstallments.Add(installment);

            // Investidor no sistema sem investimentos vinculados a este empréstimo
            var investor = new User
            {
                Id = Guid.NewGuid(),
                Email = "investor.unrelated@test.com",
                PasswordHash = "hash",
                Role = "Investidor",
                Status = "Aprovado"
            };
            context.Users.Add(investor);

            var investorWallet = new InvestorWallet
            {
                Id = investor.Id,
                SaldoDisponivel = 10000.00m,
                SaldoComprometido = 0m,
                Rendimentos = 0m
            };
            context.InvestorWallets.Add(investorWallet);

            await context.SaveChangesAsync();

            // Act: Distribuir pagamento do empréstimo de operação manual
            await distService.DistributeInstallmentPaymentAsync(installment.Id, "AdminTest");

            // Assert: A carteira do investidor permanece rigorosamente inalterada
            var checkWallet = await context.InvestorWallets.FindAsync(investor.Id);
            Assert.Equal(10000.00m, checkWallet!.SaldoDisponivel);
            Assert.Equal(0m, checkWallet.SaldoComprometido);
            Assert.Equal(0m, checkWallet.Rendimentos);
        }
    }
}
