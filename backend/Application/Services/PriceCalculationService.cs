using System;
using System.Collections.Generic;
using backend.Domain.Entities;
using backend.Application.Services.Interest;

namespace backend.Application.Services
{
    public class PriceCalculationService
    {
        private readonly IInterestCalculationStrategy _interestStrategy;

        public PriceCalculationService(IInterestCalculationStrategy? interestStrategy = null)
        {
            // Padrão do sistema: Juros Simples para todos os empréstimos ativos
            _interestStrategy = interestStrategy ?? new SimpleInterestStrategy();
        }

        /// <summary>
        /// Calcula as parcelas de um empréstimo usando a Estratégia de Juros configurada (Juros Simples por padrão).
        /// Fórmula: Juros = C × i × t
        ///          Montante Total = C + J
        ///          Parcela = Montante / NúmeroParcelas
        /// Garante zero discrepância de centavos ajustando a diferença de arredondamento na última parcela.
        /// </summary>
        public List<LoanInstallment> CalculateInstallments(
            Guid loanRequestId, 
            decimal valorSolicitado, 
            int quantidadeParcelas, 
            decimal taxaJuros, 
            string? periodicidade = "Mensal",
            decimal timePeriod = 1m,
            DateTime? dataPrimeiraParcela = null)
        {
            var installments = new List<LoanInstallment>();
            int n = quantidadeParcelas;

            if (n <= 0 || valorSolicitado <= 0)
            {
                return installments;
            }

            // === CÁLCULO VIA STRATEGY (JUROS SIMPLES: J = C * i * t) ===
            decimal jurosTotal = _interestStrategy.CalculateInterest(valorSolicitado, taxaJuros, timePeriod);
            decimal valorTotal = valorSolicitado + jurosTotal;

            // Cada parcela tem valor padrão aproximado (distribuído igualmente)
            decimal valorParcelaPadrao = Math.Round(valorTotal / n, 2, MidpointRounding.AwayFromZero);
            decimal principalPorParcela = Math.Round(valorSolicitado / n, 2, MidpointRounding.AwayFromZero);
            decimal jurosPorParcela = Math.Round(jurosTotal / n, 2, MidpointRounding.AwayFromZero);

            DateTime baseDate = dataPrimeiraParcela.HasValue 
                ? new DateTime(dataPrimeiraParcela.Value.Year, dataPrimeiraParcela.Value.Month, dataPrimeiraParcela.Value.Day, 0, 0, 0, DateTimeKind.Utc) 
                : DateTime.UtcNow.Date;
            DateTime currentDueDate = baseDate;

            decimal somaParcelasAnteriores = 0;
            decimal somaPrincipalAnterior = 0;
            decimal somaJurosAnterior = 0;

            for (int k = 1; k <= n; k++)
            {
                decimal parcela;
                decimal principal;
                decimal juros;

                if (k == n)
                {
                    // Na última parcela, absorve exatamente qualquer diferença de arredondamento
                    principal = valorSolicitado - somaPrincipalAnterior;
                    juros = jurosTotal - somaJurosAnterior;
                    parcela = valorTotal - somaParcelasAnteriores;
                }
                else
                {
                    principal = principalPorParcela;
                    juros = jurosPorParcela;
                    parcela = valorParcelaPadrao;

                    somaPrincipalAnterior += principal;
                    somaJurosAnterior += juros;
                    somaParcelasAnteriores += parcela;
                }

                // Cálculo do vencimento com base na periodicidade e data inicial
                string p = (periodicidade ?? "Mensal").Trim().ToLower();

                if (dataPrimeiraParcela.HasValue)
                {
                    if (k == 1)
                    {
                        currentDueDate = baseDate;
                    }
                    else
                    {
                        if (p == "diario")
                        {
                            currentDueDate = baseDate.AddDays(k - 1);
                        }
                        else if (p == "diarioutil")
                        {
                            currentDueDate = GetNextBusinessDay(currentDueDate);
                        }
                        else if (p == "semanal")
                        {
                            currentDueDate = baseDate.AddDays((k - 1) * 7);
                        }
                        else if (p == "quinzenal")
                        {
                            currentDueDate = baseDate.AddDays((k - 1) * 15);
                        }
                        else // Mensal (padrão)
                        {
                            currentDueDate = baseDate.AddMonths(k - 1);
                        }
                    }
                }
                else
                {
                    if (p == "diario")
                    {
                        currentDueDate = baseDate.AddDays(k);
                    }
                    else if (p == "diarioutil")
                    {
                        currentDueDate = GetNextBusinessDay(currentDueDate);
                    }
                    else if (p == "semanal")
                    {
                        currentDueDate = baseDate.AddDays(k * 7);
                    }
                    else if (p == "quinzenal")
                    {
                        currentDueDate = baseDate.AddDays(k * 15);
                    }
                    else // Mensal (padrão)
                    {
                        currentDueDate = baseDate.AddMonths(k);
                    }
                }

                installments.Add(new LoanInstallment
                {
                    Id = Guid.NewGuid(),
                    LoanRequestId = loanRequestId,
                    NumeroParcela = k,
                    ValorParcela = parcela,
                    ValorPrincipal = principal,
                    ValorJuros = juros,
                    DataVencimento = currentDueDate,
                    Status = "Pendente",
                    ValorPago = 0,
                    DiasAtraso = 0,
                    Multa = 0,
                    JurosMora = 0,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            return installments;
        }

        private DateTime GetNextBusinessDay(DateTime date)
        {
            do
            {
                date = date.AddDays(1);
            } while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday);
            return date;
        }
    }
}
