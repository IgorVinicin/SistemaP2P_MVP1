using System;
using System.Collections.Generic;
using backend.Domain.Entities;

namespace backend.Application.Services
{
    public class PriceCalculationService
    {
        public List<LoanInstallment> CalculateInstallments(Guid loanRequestId, decimal valorSolicitado, int quantidadeParcelas, decimal taxaJurosMensal, string? periodicidade = "Mensal")
        {
            var installments = new List<LoanInstallment>();

            double pv = (double)valorSolicitado;
            int n = quantidadeParcelas;
            double i = (double)taxaJurosMensal / 100.0;

            double pmt;
            if (i == 0)
            {
                pmt = pv / n;
            }
            else
            {
                pmt = pv * (i * Math.Pow(1 + i, n)) / (Math.Pow(1 + i, n) - 1);
            }

            decimal valorParcela = Math.Round((decimal)pmt, 2);
            decimal remainingPrincipal = valorSolicitado;
            DateTime baseDate = DateTime.UtcNow;
            DateTime currentDueDate = baseDate;

            for (int k = 1; k <= n; k++)
            {
                decimal juros;
                decimal principal;

                if (k == n)
                {
                    // Na última parcela, ajusta para absorver qualquer erro de arredondamento
                    principal = remainingPrincipal;
                    juros = Math.Round(remainingPrincipal * (decimal)i, 2);
                    // O valor total da parcela se torna a soma exata dos juros e principal restantes
                    valorParcela = principal + juros;
                }
                else
                {
                    juros = Math.Round(remainingPrincipal * (decimal)i, 2);
                    principal = valorParcela - juros;

                    if (principal > remainingPrincipal)
                    {
                        principal = remainingPrincipal;
                        valorParcela = principal + juros;
                    }

                    remainingPrincipal -= principal;
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
                    ValorParcela = valorParcela,
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
