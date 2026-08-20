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
    }
}
