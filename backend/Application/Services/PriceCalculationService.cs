using System;
using System.Collections.Generic;
using backend.Domain.Entities;

namespace backend.Application.Services
{
    public class PriceCalculationService
    {
        /// <summary>
        /// Calcula as parcelas de um empréstimo usando JUROS SIMPLES.
        /// Fórmula: Juros = ValorSolicitado × (Taxa / 100)
        ///          Total = ValorSolicitado + Juros
        ///          Parcela = Total / NúmeroParcelas
        /// A taxa é aplicada UMA VEZ sobre o valor total solicitado, independente da periodicidade.
        /// </summary>
        public List<LoanInstallment> CalculateInstallments(Guid loanRequestId, decimal valorSolicitado, int quantidadeParcelas, decimal taxaJuros, string? periodicidade = "Mensal")
        {
            var installments = new List<LoanInstallment>();

            int n = quantidadeParcelas;

            // === JUROS SIMPLES ===
            // A taxa é aplicada sobre o valor total solicitado, sobre o período total
            decimal jurosTotal = Math.Round(valorSolicitado * (taxaJuros / 100m), 2);
            decimal valorTotal = valorSolicitado + jurosTotal;

            // Cada parcela tem valor igual (principal + juros distribuídos igualmente)
            decimal valorParcelaPadrao = Math.Round(valorTotal / n, 2);
            decimal principalPorParcela = Math.Round(valorSolicitado / n, 2);
            decimal jurosPorParcela = Math.Round(jurosTotal / n, 2);

            DateTime baseDate = DateTime.UtcNow;
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
                    // Na última parcela, absorve qualquer diferença de arredondamento
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

                // Cálculo do vencimento com base na periodicidade
                string p = (periodicidade ?? "Mensal").Trim().ToLower();
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

