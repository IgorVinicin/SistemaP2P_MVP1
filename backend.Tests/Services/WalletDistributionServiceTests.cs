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
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Garante um DB limpo por teste
                .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AppDbContext(options);
        }

        [Fact]
        public async Task DistributeInstallmentPaymentAsync_ShouldCalculateFractionsAndSpreadCorrectly()
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
                ValorSolicitado = 1000.00m, // Empréstimo redondo
                TaxaJuros = 2.00m, // 2% 
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
                ValorPago = 100.00m, // Parcela 100% Paga
                Status = "Paga"
            };
            dbContext.LoanInstallments.Add(installment);

            // 4. Setup 3 Investidores (Frações desiguais)
            // Inv 1 aportou 500 (50%) -> Tem rentabilidade igual ao tomador (2%)
            // Inv 2 aportou 200 (20%) -> Tem rentabilidade igual ao tomador (2%)
            // Inv 3 aportou 300 (30%) -> Tem rentabilidade MENOR (1%), o que gera Spread (lucro) pra plataforma.
            
            var inv1 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 500, Rentabilidade = 2.00m };
            var inv2 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 200, Rentabilidade = 2.00m };
            var inv3 = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 0, SaldoInvestido = 300, Rentabilidade = 1.00m };

            dbContext.InvestorWallets.AddRange(inv1, inv2, inv3);

            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv1.Id, ValorInvestido = 500.00m, Status = "Ativo" });
            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv2.Id, ValorInvestido = 200.00m, Status = "Ativo" });
            dbContext.Investments.Add(new Investment { Id = Guid.NewGuid(), LoanId = loan.Id, InvestorId = inv3.Id, ValorInvestido = 300.00m, Status = "Ativo" });

            await dbContext.SaveChangesAsync();

            var service = new WalletDistributionService(dbContext);

            // Act - A Mágica de Distribuição
            await service.DistributeInstallmentPaymentAsync(installment.Id, "TesteUnitario");

            // Assert
            var wallet1 = await dbContext.InvestorWallets.FindAsync(inv1.Id);
            var wallet2 = await dbContext.InvestorWallets.FindAsync(inv2.Id);
            var wallet3 = await dbContext.InvestorWallets.FindAsync(inv3.Id);

            // Fraction Inv1 (50%): Principal (80 * 0.5 = 40), Juros (20 * 0.5 * (2/2) = 10). Total = 50.
            Assert.Equal(50.00m, wallet1.SaldoDisponivel);
            Assert.Equal(10.00m, wallet1.Rendimentos);

            // Fraction Inv2 (20%): Principal (80 * 0.2 = 16), Juros (20 * 0.2 * (2/2) = 4). Total = 20.
            Assert.Equal(20.00m, wallet2.SaldoDisponivel);
            Assert.Equal(4.00m, wallet2.Rendimentos);

            // Fraction Inv3 (30%): Principal (80 * 0.3 = 24), Juros (20 * 0.3 * (1/2) = 3). Total = 27.
            // Spread esperado gerado pelo Inv 3: (Juros Total Pago pelo tomador que caberia a ele era 6 (20 * 0.3). Ele recebeu 3. Plataforma fica com 3.
            Assert.Equal(27.00m, wallet3.SaldoDisponivel);
            Assert.Equal(3.00m, wallet3.Rendimentos);

            // Validar o Spread do Sistema!
            var platformRevenue = await dbContext.PlatformRevenues.Where(r => r.TipoReceita == PlatformRevenueType.TarifaOperacional).ToListAsync();
            decimal totalSpread = platformRevenue.Sum(r => r.Valor);

            // 100 reais pagos - 50 (Inv1) - 20 (Inv2) - 27 (Inv3) = 3 reais de sobra!
            Assert.Equal(3.00m, totalSpread);
            
            // Validar Partida Dobrada Contábil: Total que saiu das regras (Investidores + Sistema) = Exatamente o valor pago da Parcela.
            decimal totalRecebidoInvestidores = wallet1.SaldoDisponivel + wallet2.SaldoDisponivel + wallet3.SaldoDisponivel;
            Assert.Equal(installment.ValorPago, totalRecebidoInvestidores + totalSpread);
        }
    }
}
