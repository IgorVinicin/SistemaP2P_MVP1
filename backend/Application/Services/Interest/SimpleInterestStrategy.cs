using System;

namespace backend.Application.Services.Interest
{
    /// <summary>
    /// Estratégia de Juros Simples: J = C * i * t | M = C + J
    /// Sem qualquer tipo de capitalização ou juros sobre juros.
    /// </summary>
    public class SimpleInterestStrategy : IInterestCalculationStrategy
    {
        public decimal CalculateInterest(decimal principal, decimal rate, decimal timePeriod)
        {
            if (principal <= 0 || rate <= 0 || timePeriod <= 0)
            {
                return 0m;
            }

            // J = C * (rate / 100) * t
            decimal interestRateFraction = rate / 100m;
            decimal interest = principal * interestRateFraction * timePeriod;

            return Math.Round(interest, 2, MidpointRounding.AwayFromZero);
        }

        public decimal CalculateTotalAmount(decimal principal, decimal rate, decimal timePeriod)
        {
            decimal interest = CalculateInterest(principal, rate, timePeriod);
            return principal + interest;
        }
    }
}
