using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace backend.Tests.Services
{
    public class WalletDistributionServiceTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task DistributeInstallmentPaymentAsync_ShouldCalculateFractionsAndReturnPrincipalCorrectly()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();

            // 1. Setup Tomador
            var borrower = new User { Id = Guid.NewGuid(), Email = "tomador@test.com", Role = "Cliente" };
            dbContext.Users.Add(borrower);

            // 2. Setup Empréstimo (1000 reais, taxa 2%)
            var loan = new LoanRequest
            {
                Id = Guid.NewGuid(),
                UserId = borrower.Id,
                ValorSolicitado = 1000.00m,
                TaxaJuros = 2.00m,
                Status = "EmPagamento"
            };
            dbContext.LoanRequests.Add(loan);

            // 3. Setup Parcela de 100 reais (Principal: 80, Juros: 20)
            var installment = new LoanInstallment
            {
                Id = Guid.NewGuid(),
                LoanRequestId = loan.Id,
                NumeroParcela = 1,
                ValorPrincipal = 80.00m,
                ValorJuros = 20.00m,
                ValorParcela = 100.00m,
                ValorPago = 100.00m,
                Status = "Paga"
            };
            dbContext.LoanInstallments.Add(installment);

            // 4. Setup 3 Investidores
            var inv1 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 500, Rentabilidade = 2.00m };
            var inv2 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 200, Rentabilidade = 2.00m };
            var inv3 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 300, Rentabilidade = 1.00m };

            dbContext.InvestorWallets.AddRange(inv1, inv2, inv3);

            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv1.Id, ValorInvestido = 500.00m, SaldoPrincipalPendente = 500.00m, Status = "Ativo" });
            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv2.Id, ValorInvestido = 200.00m, SaldoPrincipalPendente = 200.00m, Status = "Ativo" });
            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv3.Id, ValorInvestido = 300.00m, SaldoPrincipalPendente = 300.00m, Status = "Ativo" });

            await dbContext.SaveChangesAsync();

            var service = new WalletDistributionService(dbContext);

            // Act
            await service.DistributeInstallmentPaymentAsync(installment.Id, "TesteUnitario");

            // Assert
            var wallet1 = await dbContext.InvestorWallets.FindAsync(inv1.Id);
            var wallet2 = await dbContext.InvestorWallets.FindAsync(inv2.Id);
            var wallet3 = await dbContext.InvestorWallets.FindAsync(inv3.Id);

            // Retorno proporcional de principal (80.00m total):
            // Inv 1 (50%): 40.00m
            // Inv 2 (20%): 16.00m
            // Inv 3 (30%): 24.00m
            Assert.Equal(40.00m, wallet1!.SaldoDisponivel);
            Assert.Equal(16.00m, wallet2!.SaldoDisponivel);
            Assert.Equal(24.00m, wallet3!.SaldoDisponivel);
        }

        [Theory]
        [InlineData(40, 32, 8)]
        [InlineData(100, 80, 20)]
        public async Task ActualReceivedMoneyBoundsDistributionAndReplay(decimal paid, decimal principal, decimal revenue)
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            installment.ValorPago = paid;
            await db.SaveChangesAsync();
            var service = new WalletDistributionService(db);
            await service.DistributeInstallmentPaymentAsync(installment.Id);
            await service.DistributeInstallmentPaymentAsync(installment.Id);
            Assert.Equal(principal, wallet.SaldoDisponivel);
            Assert.Equal(revenue, db.PlatformRevenues.Sum(r => r.Valor));
            Assert.Equal(paid, db.LedgerTransactions.Where(t => t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment).Sum(t => t.Valor));
            Assert.Equal(paid, db.LedgerTransactions.Where(t => t.ContaOrigem == LedgerAccount.PlatformCash).Sum(t => t.Valor));
        }

        [Fact]
        public async Task SuccessivePartialPaymentsPreserveDebtHistoryAndDistributeOnlyDelta()
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            var service = new InstallmentPaymentService(db);
            await service.ApplyPaymentAsync(installment.Id, 40);
            Assert.Equal("Parcial", installment.Status);
            Assert.Equal(40, installment.ValorPago);
            Assert.Equal(100, installment.ValorParcela);
            Assert.Equal(80, installment.ValorPrincipal);
            Assert.Equal(32, wallet.SaldoDisponivel);
            await service.ApplyPaymentAsync(installment.Id, 60);
            Assert.Equal("Paga", installment.Status);
            Assert.Equal(100, installment.ValorPago);
            Assert.Equal(80, wallet.SaldoDisponivel);
            Assert.Equal(20, db.PlatformRevenues.Sum(r => r.Valor));
            Assert.Equal(100, db.LedgerTransactions.Where(t => t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment).Sum(t => t.Valor));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ApplyPaymentAsync(installment.Id, 1));
        }

        [Fact]
        public async Task OverpaymentIsRejectedWithoutCreditingWallet()
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => new InstallmentPaymentService(db).ApplyPaymentAsync(installment.Id, 101));
            Assert.Equal(0, wallet.SaldoDisponivel);
            Assert.Empty(db.LedgerTransactions);
        }

        [Fact]
        public void InstallmentComponentsAlwaysMatchRoundedContract()
        {
            var installments = new PriceCalculationService().CalculateInstallments(Guid.NewGuid(), 100, 3, 10);
            Assert.All(installments, i => Assert.Equal(i.ValorPrincipal + i.ValorJuros, i.ValorParcela));
            Assert.Equal(100, installments.Sum(i => i.ValorPrincipal));
            Assert.Equal(10, installments.Sum(i => i.ValorJuros));
            Assert.Equal(110, installments.Sum(i => i.ValorParcela));
        }

        [Fact]
        public async Task PartialPaymentDoesNotHideContinuingDelinquency()
        {
            using var db = GetInMemoryDbContext();
            var (installment, _) = await SeedPayment(db);
            installment.DataVencimento = DateTime.UtcNow.AddDays(-70);
            await new InstallmentPaymentService(db).ApplyPaymentAsync(installment.Id, 40);
            await new DelinquencyService(db).UpdateOverdueInstallmentsAsync();
            Assert.Equal("Parcial", installment.Status);
            Assert.True(installment.DiasAtraso >= 69);
            Assert.Equal("Inadimplente", installment.LoanRequest!.Status);
        }

        [Fact]
        public async Task FundCoverageHasExactlyOneIncomingLedgerEntryAndReplaysSafely()
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            await new InstallmentPaymentService(db).ApplyPaymentAsync(installment.Id, 100, "Fundo", LedgerAccount.ProtectionFund);
            await new WalletDistributionService(db).DistributeInstallmentPaymentAsync(installment.Id);
            Assert.Single(db.LedgerTransactions.Where(t => t.ContaDestino == LedgerAccount.PlatformCash));
            Assert.Equal(100, db.LedgerTransactions.Where(t => t.ContaOrigem == LedgerAccount.ProtectionFund).Sum(t => t.Valor));
            Assert.Empty(db.LedgerTransactions.Where(t => t.ContaOrigem == LedgerAccount.BorrowerAccount));
            Assert.Equal(80, wallet.SaldoDisponivel);
        }

        [Fact]
        public async Task RolloverPreservesSeparatePrincipalInterestAndPaymentHistory()
        {
            using var db = GetInMemoryDbContext();
            var (first, wallet) = await SeedPayment(db);
            first.LoanRequest!.ValorSolicitado = 160;
            wallet.SaldoInvestido = 160;
            var position = await db.Investments.SingleAsync();
            position.ValorInvestido = position.SaldoPrincipalPendente = 160;
            var next = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = first.LoanRequestId, NumeroParcela = 2, ValorPrincipal = 80, ValorJuros = 20, ValorParcela = 100, DataVencimento = DateTime.UtcNow.AddDays(2) };
            db.LoanInstallments.Add(next);
            await db.SaveChangesAsync();
            var service = new InstallmentPaymentService(db);
            await service.ApplyPaymentAsync(first.Id, 40);
            Assert.Equal("Paga", first.Status);
            Assert.Equal(40, first.ValorPago);
            Assert.Equal(32, first.ValorPrincipal);
            Assert.Equal(8, first.ValorJuros);
            Assert.Equal(128, next.ValorPrincipal);
            Assert.Equal(32, next.ValorJuros);
            Assert.Equal(160, next.ValorParcela);
            var rollover = await db.DebtRollovers.SingleAsync();
            Assert.Equal(48, rollover.Principal);
            Assert.Equal(12, rollover.Interest);
            await service.ApplyPaymentAsync(next.Id, 160);
            Assert.Equal(160, wallet.SaldoDisponivel);
            Assert.Equal(0, position.SaldoPrincipalPendente);
            Assert.Equal(40, db.PlatformRevenues.Sum(r => r.Valor));
        }

        [Fact]
        public async Task ActiveNextChargeMustBeCancelledBeforeRollover()
        {
            using var db = GetInMemoryDbContext();
            var (first, _) = await SeedPayment(db);
            var next = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = first.LoanRequestId, NumeroParcela = 2, ValorPrincipal = 80, ValorJuros = 20, ValorParcela = 100 };
            db.LoanInstallments.Add(next);
            db.GatewayCharges.Add(new GatewayCharge { ReferenceId = next.Id, Kind = "Installment", IsActive = true, Value = 100 });
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => new InstallmentPaymentService(db).ApplyPaymentAsync(first.Id, 40));
            Assert.Equal(0, first.ValorPago);
            Assert.Empty(db.LedgerTransactions);
        }

        [Fact]
        public async Task ThreeWayRateioReturnsEveryCentOfFundedPrincipal()
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            installment.LoanRequest!.ValorSolicitado = 100;
            installment.ValorPrincipal = installment.ValorParcela = 100;
            installment.ValorJuros = 0;
            wallet.SaldoInvestido = 33.33m;
            var firstPosition = await db.Investments.SingleAsync();
            firstPosition.ValorInvestido = firstPosition.SaldoPrincipalPendente = 33.33m;
            foreach (var value in new[] { 33.33m, 33.34m })
            {
                var other = new InvestorWallet { Id = Guid.NewGuid(), SaldoInvestido = value };
                db.InvestorWallets.Add(other);
                db.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = installment.LoanRequestId, InvestorId = other.Id, ValorInvestido = value, SaldoPrincipalPendente = value, Status = "Ativo" });
            }
            await db.SaveChangesAsync();
            var service = new InstallmentPaymentService(db);
            await service.ApplyPaymentAsync(installment.Id, 0.01m);
            Assert.Equal(0.01m, db.InvestorWallets.Sum(w => w.SaldoDisponivel));
            await service.ApplyPaymentAsync(installment.Id, 99.99m);
            Assert.Equal(100m, db.InvestorWallets.Sum(w => w.SaldoDisponivel));
            Assert.Equal(0, db.Investments.Sum(i => i.SaldoPrincipalPendente));
            Assert.Empty(db.PlatformRevenues);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(30, 8, 30)]
        [InlineData(32, 8, 0)]
        public async Task HistoricalPartialPaymentCannotBeGuessedOrReinterpreted(decimal principal, decimal interest, decimal distributed)
        {
            using var db = GetInMemoryDbContext();
            var (installment, wallet) = await SeedPayment(db);
            installment.ValorPago = 40;
            installment.Status = "Parcial";
            installment.PrincipalPago = principal;
            installment.JurosPagos = interest;
            installment.PrincipalDistribuido = distributed;
            await db.SaveChangesAsync();
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new InstallmentPaymentService(db).ApplyPaymentAsync(installment.Id, 10));
            Assert.Contains("conciliação histórica", error.Message);
            Assert.Equal(40, installment.ValorPago);
            Assert.Equal(principal, installment.PrincipalPago);
            Assert.Equal(interest, installment.JurosPagos);
            Assert.Equal(distributed, installment.PrincipalDistribuido);
            Assert.Equal(0, wallet.SaldoDisponivel);
            Assert.Empty(db.LedgerTransactions);
            Assert.Empty(db.FinancialEventHistories);
            Assert.Empty(db.DebtRollovers);
        }

        [Fact]
        public async Task WalletVersionAdvancesAndRejectsConcurrentWriter()
        {
            var database = Guid.NewGuid().ToString();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(database).Options;
            using var first = new AppDbContext(options);
            var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 100 };
            first.InvestorWallets.Add(wallet);
            await first.SaveChangesAsync();
            using var second = new AppDbContext(options);
            var stale = await second.InvestorWallets.FindAsync(wallet.Id);
            wallet.SaldoDisponivel -= 80;
            await first.SaveChangesAsync();
            Assert.Equal(1u, wallet.Version);
            stale!.SaldoDisponivel -= 80;
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }

        private async Task<(LoanInstallment, InvestorWallet)> SeedPayment(AppDbContext db)
        {
            var borrower = new User { Id = Guid.NewGuid(), Email = "borrower@test.com", Role = "Cliente" };
            var loan = new LoanRequest { Id = Guid.NewGuid(), UserId = borrower.Id, ValorSolicitado = 100, Status = "EmPagamento" };
            var installment = new LoanInstallment { Id = Guid.NewGuid(), LoanRequestId = loan.Id, NumeroParcela = 1, ValorPrincipal = 80, ValorJuros = 20, ValorParcela = 100, DataVencimento = DateTime.UtcNow.AddDays(1) };
            var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoInvestido = 100 };
            db.Users.Add(borrower);
            db.LoanRequests.Add(loan);
            db.LoanInstallments.Add(installment);
            db.InvestorWallets.Add(wallet);
            db.Investments.Add(new Investment { Id = Guid.NewGuid(), InvestorId = wallet.Id, LoanId = loan.Id, ValorInvestido = 100, SaldoPrincipalPendente = 100, Status = "Ativo" });
            await db.SaveChangesAsync();
            return (installment, wallet);
        }
    }
}
