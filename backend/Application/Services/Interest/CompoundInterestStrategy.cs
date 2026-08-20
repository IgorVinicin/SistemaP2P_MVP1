using System;

namespace backend.Application.Services.Interest
{
    /// <summary>
    /// Estratégia de Juros Compostos: M = C * (1 + i)^t | J = M - C
    /// PREPARAÇÃO PARA O FUTURO: Reservado para contratos em atraso/inadimplência.
    /// Não deve ser ativado para empréstimos ativos em dia nesta versão.
    /// </summary>
    public class CompoundInterestStrategy : IInterestCalculationStrategy
    {
        public decimal CalculateInterest(decimal principal, decimal rate, decimal timePeriod)
        {
            if (principal <= 0 || rate <= 0 || timePeriod <= 0)
            {
                return 0m;
            }

            double p = (double)principal;
            double r = (double)(rate / 100m);
            double t = (double)timePeriod;

            double totalAmount = p * Math.Pow(1 + r, t);
            decimal roundedTotal = Math.Round((decimal)totalAmount, 2, MidpointRounding.AwayFromZero);

            return roundedTotal - principal;
        }

        public decimal CalculateTotalAmount(decimal principal, decimal rate, decimal timePeriod)
        {
            decimal interest = CalculateInterest(principal, rate, timePeriod);
            return principal + interest;
        }
    }
}
