namespace backend.Application.Services.Interest
{
    public interface IInterestCalculationStrategy
    {
        /// <summary>
        /// Calcula o valor total de juros para um capital, taxa e período de tempo.
        /// </summary>
        /// <param name="principal">Capital inicial (C)</param>
        /// <param name="rate">Taxa de juros em percentual (i, ex: 10 para 10%)</param>
        /// <param name="timePeriod">Tempo/Período (t)</param>
        /// <returns>Valor total dos juros (J)</returns>
        decimal CalculateInterest(decimal principal, decimal rate, decimal timePeriod);

        /// <summary>
        /// Calcula o montante total (M = C + J).
        /// </summary>
        decimal CalculateTotalAmount(decimal principal, decimal rate, decimal timePeriod);
    }
}
