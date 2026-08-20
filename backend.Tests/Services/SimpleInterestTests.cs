using System;
using System.Collections.Generic;
using System.Linq;
using backend.Application.Services;
using backend.Application.Services.Interest;
using Xunit;

namespace backend.Tests.Services
{
    public class SimpleInterestTests
    {
        private readonly PriceCalculationService _priceCalcService;
        private readonly SimpleInterestStrategy _simpleInterestStrategy;

        public SimpleInterestTests()
        {
            _simpleInterestStrategy = new SimpleInterestStrategy();
            _priceCalcService = new PriceCalculationService(_simpleInterestStrategy);
        }

        [Theory]
        [InlineData(500, 5, 1, 25)]      // 500 * 5% * 1 = 25
        [InlineData(1000, 10, 1, 100)]   // 1000 * 10% * 1 = 100
        [InlineData(2000, 15, 1, 300)]   // 2000 * 15% * 1 = 300
        [InlineData(5000, 20, 1, 1000)]  // 5000 * 20% * 1 = 1000
        [InlineData(10000, 25, 1, 2500)] // 10000 * 25% * 1 = 2500
        [InlineData(10000, 30, 1, 3000)] // 10000 * 30% * 1 = 3000
        public void CalculateInterest_ShouldFollowSimpleInterestFormula(decimal capital, decimal rate, decimal time, decimal expectedInterest)
        {
            // Act
            decimal interest = _simpleInterestStrategy.CalculateInterest(capital, rate, time);
            decimal total = _simpleInterestStrategy.CalculateTotalAmount(capital, rate, time);

            // Assert
            Assert.Equal(expectedInterest, interest);
            Assert.Equal(capital + expectedInterest, total);
        }

        [Fact]
        public void MatrixTest_AllValueTermRateCombinations_MustSumExactlyToMontante()
        {
            decimal[] valores = new decimal[] { 500m, 1000m, 2000m, 5000m, 10000m };
            decimal[] taxas = new decimal[] { 5m, 10m, 15m, 20m, 25m, 30m };
            (string periodicity, int parcelas)[] prazos = new (string, int)[]
            {
                ("Diario", 7),
                ("Diario", 15),
                ("DiarioUtil", 24),
                ("Mensal", 1),  // 30 dias
                ("Mensal", 2),  // 60 dias
                ("Mensal", 3)   // 90 dias
            };

            foreach (var valor in valores)
            {
                foreach (var taxa in taxas)
                {
                    foreach (var (periodicity, numParcelas) in prazos)
                    {
                        // Act
                        var installments = _priceCalcService.CalculateInstallments(
                            Guid.NewGuid(), 
                            valor, 
                            numParcelas, 
                            taxa, 
                            periodicity
                        );

                        decimal totalJurosEsperado = Math.Round(valor * (taxa / 100m), 2, MidpointRounding.AwayFromZero);
                        decimal montanteEsperado = valor + totalJurosEsperado;

                        decimal somaParcelas = installments.Sum(x => x.ValorParcela);
                        decimal somaPrincipal = installments.Sum(x => x.ValorPrincipal);
                        decimal somaJuros = installments.Sum(x => x.ValorJuros);

                        // Assert - Tolerância ZERO a divergências de centavos
                        Assert.Equal(montanteEsperado, somaParcelas);
                        Assert.Equal(valor, somaPrincipal);
                        Assert.Equal(totalJurosEsperado, somaJuros);
                        Assert.Equal(numParcelas, installments.Count);
                    }
                }
            }
        }

        [Fact]
        public void StrategyComparison_SimpleVsCompound_DemonstratesAbsenceOfCompoundingInActiveLoans()
        {
            decimal capital = 10000m;
            decimal rate = 10m;
            decimal periods = 12m;

            var simpleStrategy = new SimpleInterestStrategy();
            var compoundStrategy = new CompoundInterestStrategy();

            decimal simpleInterest = simpleStrategy.CalculateInterest(capital, rate, periods);
            decimal compoundInterest = compoundStrategy.CalculateInterest(capital, rate, periods);

            // Juros Simples: 10000 * 0.10 * 12 = 12000
            Assert.Equal(12000m, simpleInterest);

            // Juros Compostos: 10000 * (1.10^12 - 1) ~= 21384.28
            Assert.True(compoundInterest > simpleInterest);
            Assert.Equal(21384.28m, compoundInterest);
        }
    }
}
