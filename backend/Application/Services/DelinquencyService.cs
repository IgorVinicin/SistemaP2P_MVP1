using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class DelinquencyService
    {
        private readonly AppDbContext _context;

        public DelinquencyService(AppDbContext context)
        {
            _context = context;
        }

        public async Task UpdateOverdueInstallmentsAsync()
        {
            var today = DateTime.UtcNow.Date;
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal percentualMulta = config?.PercentualMultaAtraso ?? 2.00m;
            decimal percentualJurosDiario = config?.PercentualJurosDiario ?? 0.033m;
            int diasParaCobranca = config?.DiasParaCobranca ?? 16;
            int diasParaCobrancaIntensiva = config?.DiasParaCobrancaIntensiva ?? 31;
            int diasParaInadimplencia = config?.DiasParaInadimplencia ?? 61;

            var unpaidInstallments = await _context.LoanInstallments
                .Include(i => i.LoanRequest)
                .Where(i => i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null)
                .ToListAsync();

            bool changed = false;

            foreach (var inst in unpaidInstallments)
            {
                if (inst.DataVencimento.Date < today)
                {
                    int dias = (int)(today - inst.DataVencimento.Date).TotalDays;
                    
                    // Fallback para empréstimos antigos que não tem taxa congelada
                    decimal multa = inst.LoanRequest.PercentualMultaAtrasoContrato > 0 ? inst.LoanRequest.PercentualMultaAtrasoContrato : percentualMulta;
                    decimal juros = inst.LoanRequest.PercentualJurosDiarioContrato > 0 ? inst.LoanRequest.PercentualJurosDiarioContrato : percentualJurosDiario;

                    decimal novaMulta = Math.Round(inst.ValorParcela * (multa / 100m), 2);
                    decimal novoJuros = Math.Round(inst.ValorParcela * (juros / 100m) * dias, 2);

                    if (inst.DiasAtraso != dias || inst.Multa != novaMulta || inst.JurosMora != novoJuros)
                    {
                        inst.DiasAtraso = dias;
                        inst.Multa = novaMulta;
                        inst.JurosMora = novoJuros;
                        inst.UpdatedAt = DateTime.UtcNow;
                        changed = true;
                    }

                    string oldStatus = inst.Status;
                    string newStatus = "ATRASADO";
                    if (dias >= diasParaInadimplencia) newStatus = "INADIMPLENTE";
                    else if (dias >= diasParaCobrancaIntensiva) newStatus = "COBRANCA_INTENSIVA";
                    else if (dias >= diasParaCobranca) newStatus = "COBRANCA";

                    // Se a parcela já foi parcialmente paga, preservamos o status "Parcial"
                    if (oldStatus == "Parcial" || inst.ValorPago > 0)
                    {
                        newStatus = "Parcial";
                    }

                    if (oldStatus != newStatus)
                    {
                        inst.Status = newStatus;
                        inst.UpdatedAt = DateTime.UtcNow;
                        changed = true;

                        // Log event
                        string eventType = "MUDANCA_STATUS";
                        if (newStatus == "COBRANCA") eventType = "ENTRADA_COBRANCA";
                        else if (newStatus == "INADIMPLENTE") eventType = "ENTRADA_INADIMPLENCIA";

                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = eventType,
                            Observacao = $"Parcela {inst.NumeroParcela} do Empréstimo {inst.LoanRequestId} mudou de {oldStatus} para {newStatus} (dias de atraso: {dias}).",
                            LoanRequestId = inst.LoanRequestId,
                            Valor = inst.ValorParcela
                        });
                    }

                    // Se a parcela (mesmo Parcial) passou do limite de dias para inadimplência,
                    // garantimos que o LoanRequest mude para Inadimplente
                    if (dias >= diasParaInadimplencia && inst.LoanRequest != null && inst.LoanRequest.Status != "Inadimplente")
                    {
                        string oldLoanStatus = inst.LoanRequest.Status;
                        inst.LoanRequest.Status = "Inadimplente";
                        changed = true;

                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = "ENTRADA_INADIMPLENCIA",
                            Observacao = $"Empréstimo {inst.LoanRequestId} mudou de {oldLoanStatus} para Inadimplente devido ao atraso da parcela {inst.NumeroParcela} (dias de atraso: {dias}).",
                            LoanRequestId = inst.LoanRequestId,
                            Valor = inst.LoanRequest.ValorSolicitado
                        });
                    }
                }
                else
                {
                    // Se não está vencida e o status anterior era algum de atraso, volta para Pendente
                    if (inst.Status == "ATRASADO" || inst.Status == "COBRANCA" || inst.Status == "COBRANCA_INTENSIVA" || inst.Status == "INADIMPLENTE" || inst.Status == "Atrasada")
                    {
                        string oldStatus = inst.Status;
                        inst.Status = "Pendente";
                        inst.DiasAtraso = 0;
                        inst.Multa = 0;
                        inst.JurosMora = 0;
                        inst.UpdatedAt = DateTime.UtcNow;
                        changed = true;

                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = "MUDANCA_STATUS",
                            Observacao = $"Parcela {inst.NumeroParcela} do Empréstimo {inst.LoanRequestId} retornou para Pendente.",
                            LoanRequestId = inst.LoanRequestId
                        });
                    }
                }
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateLoanStatusFromInstallmentsAsync(Guid loanRequestId, string userWhoUpdated)
        {
            var loan = await _context.LoanRequests
                .FirstOrDefaultAsync(l => l.Id == loanRequestId);

            if (loan == null) return;

            var allInstallments = await _context.LoanInstallments
                .Where(i => i.LoanRequestId == loanRequestId)
                .ToListAsync();

            var hasInadimplente = allInstallments.Any(i => i.Status == "INADIMPLENTE" || i.Status == "COBRANCA_INTENSIVA");
            var hasOverdue = allInstallments.Any(i => i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "Atrasada" || i.Status == "Parcial");
            var allPaid = allInstallments.All(i => i.Status == "Paga" || i.Status == "Cancelada");

            if (allPaid)
            {
                if (loan.Status != "Quitado")
                {
                    loan.Status = "Quitado";
                    loan.DataEncerramento = DateTime.UtcNow;

                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = userWhoUpdated,
                        EventoExecutado = "MUDANCA_STATUS",
                        Observacao = $"Empréstimo {loan.Id} foi totalmente quitado.",
                        LoanRequestId = loan.Id,
                        Valor = loan.ValorTotal
                    });

                    await _context.SaveChangesAsync();
                }
            }
            else if (!hasInadimplente && !hasOverdue && loan.Status == "Inadimplente")
            {
                loan.Status = "EmPagamento";

                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = userWhoUpdated,
                    EventoExecutado = "MUDANCA_STATUS",
                    Observacao = $"Empréstimo {loan.Id} retornou ao status EmPagamento após regularização de parcelas.",
                    LoanRequestId = loan.Id,
                    Valor = loan.ValorSolicitado
                });

                await _context.SaveChangesAsync();
            }
        }
    }
}
