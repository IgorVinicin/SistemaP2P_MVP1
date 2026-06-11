using backend.Domain.Entities;
using backend.Infrastructure;
using backend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api")]
    [Authorize]
    public class LoanInstallmentController : ControllerBase
    {
        private readonly AppDbContext _context;

        public LoanInstallmentController(AppDbContext context)
        {
            _context = context;
        }

        // POST /api/loans/installments/{id}/pay
        [HttpPost("loans/installments/{id}/pay")]
        public async Task<IActionResult> PayInstallment(Guid id)
        {
            try
            {
                var installment = await _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .FirstOrDefaultAsync(i => i.Id == id);

                if (installment == null)
                {
                    return NotFound(new { message = "Parcela não encontrada." });
                }

                if (installment.Status == "Paga")
                {
                    return BadRequest(new { message = "Esta parcela já está paga." });
                }

                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                decimal percentualMulta = config?.PercentualMultaAtraso ?? 2.00m;
                decimal percentualJurosDiario = config?.PercentualJurosDiario ?? 0.033m;

                var today = DateTime.UtcNow.Date;
                var vencimento = installment.DataVencimento.Date;

                if (vencimento < today)
                {
                    int dias = (int)(today - vencimento).TotalDays;
                    installment.DiasAtraso = dias;
                    installment.Multa = Math.Round(installment.ValorParcela * (percentualMulta / 100m), 2);
                    installment.JurosMora = Math.Round(installment.ValorParcela * (percentualJurosDiario / 100m) * dias, 2);
                    installment.ValorPago = installment.ValorParcela + installment.Multa + installment.JurosMora;
                }
                else
                {
                    installment.ValorPago = installment.ValorParcela;
                    installment.DiasAtraso = 0;
                    installment.Multa = 0;
                    installment.JurosMora = 0;
                }

                installment.DataPagamento = DateTime.UtcNow;
                installment.Status = "Paga";
                installment.UpdatedAt = DateTime.UtcNow;

                // Registrar evento de pagamento no histórico
                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = User.FindFirst(ClaimTypes.Email)?.Value ?? "Cliente",
                    EventoExecutado = "PAGAMENTO_PARCELA",
                    Observacao = $"Parcela {installment.NumeroParcela} do Empréstimo {installment.LoanRequestId} foi paga com sucesso. Valor Pago: {installment.ValorPago:C2}.",
                    LoanRequestId = installment.LoanRequestId,
                    Valor = installment.ValorPago
                });

                // Verificar se existem outras parcelas em aberto
                var allInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == installment.LoanRequestId)
                    .ToListAsync();

                var hasInadimplente = allInstallments.Any(i => i.Id != installment.Id && i.Status == "INADIMPLENTE");
                var allPaid = allInstallments.All(i => i.Id == installment.Id || i.Status == "Paga" || i.Status == "Cancelada");

                var loan = installment.LoanRequest;
                if (loan != null)
                {
                    if (allPaid)
                    {
                        loan.Status = "Quitado";
                        loan.DataEncerramento = DateTime.UtcNow;

                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = "MUDANCA_STATUS",
                            Observacao = $"Empréstimo {loan.Id} foi totalmente quitado.",
                            LoanRequestId = loan.Id,
                            Valor = loan.ValorTotal
                        });
                    }
                    else if (!hasInadimplente && loan.Status == "Inadimplente")
                    {
                        loan.Status = "EmPagamento";

                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = "MUDANCA_STATUS",
                            Observacao = $"Empréstimo {loan.Id} retornou ao status EmPagamento após o pagamento da parcela {installment.NumeroParcela}.",
                            LoanRequestId = loan.Id
                        });
                    }
                }

                // Salvar as alterações
                await _context.SaveChangesAsync();

                // Integrar preparação de carteira de investidores
                var distService = new WalletDistributionService(_context);
                await distService.DistributeInstallmentPaymentAsync(installment.Id);

                return Ok(new { message = "Pagamento da parcela simulado com sucesso.", installment });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/loans/{loanRequestId}/installments
        [HttpGet("loans/{loanRequestId}/installments")]
        public async Task<IActionResult> GetInstallments(Guid loanRequestId)
        {
            try
            {
                // Atualizar parcelas atrasadas antes de retornar
                await UpdateOverdueInstallmentsAsync();

                var installments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == loanRequestId)
                    .OrderBy(i => i.NumeroParcela)
                    .ToListAsync();

                return Ok(installments);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/loans/installments
        [HttpGet("admin/loans/installments")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> GetAdminInstallments(
            [FromQuery] string? cliente,
            [FromQuery] string? status,
            [FromQuery] DateTime? periodoInicio,
            [FromQuery] DateTime? periodoFim,
            [FromQuery] bool? somenteAtrasadas)
        {
            try
            {
                // Atualizar parcelas atrasadas antes de retornar
                await UpdateOverdueInstallmentsAsync();

                var query = _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .ThenInclude(l => l!.User)
                    .ThenInclude(u => u!.Profile)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(cliente))
                {
                    var term = cliente.ToLower();
                    query = query.Where(i => (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Email.ToLower().Contains(term)) ||
                                             (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null && i.LoanRequest.User.Profile.Nome.ToLower().Contains(term)));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(i => i.Status.ToLower() == status.ToLower());
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(i => i.DataVencimento >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(i => i.DataVencimento <= periodoFim.Value.ToUniversalTime());
                }

                if (somenteAtrasadas == true)
                {
                    query = query.Where(i => i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "COBRANCA_INTENSIVA" || i.Status == "INADIMPLENTE" || i.Status == "Atrasada");
                }

                var list = await query
                    .OrderBy(i => i.DataVencimento)
                    .Select(i => new
                    {
                        i.Id,
                        i.LoanRequestId,
                        i.NumeroParcela,
                        i.ValorParcela,
                        i.ValorPrincipal,
                        i.ValorJuros,
                        i.DataVencimento,
                        i.DataPagamento,
                        i.Status,
                        i.ValorPago,
                        i.DiasAtraso,
                        i.Multa,
                        i.JurosMora,
                        ClienteNome = i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null ? i.LoanRequest.User.Profile.Nome : "Sem Nome",
                        ClienteEmail = i.LoanRequest != null && i.LoanRequest.User != null ? i.LoanRequest.User.Email : string.Empty
                    })
                    .ToListAsync();

                return Ok(list);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private async Task UpdateOverdueInstallmentsAsync()
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
                    inst.DiasAtraso = dias;
                    inst.Multa = Math.Round(inst.ValorParcela * (percentualMulta / 100m), 2);
                    inst.JurosMora = Math.Round(inst.ValorParcela * (percentualJurosDiario / 100m) * dias, 2);
                    inst.UpdatedAt = DateTime.UtcNow;

                    string oldStatus = inst.Status;
                    string newStatus = "ATRASADO";
                    if (dias >= diasParaInadimplencia) newStatus = "INADIMPLENTE";
                    else if (dias >= diasParaCobrancaIntensiva) newStatus = "COBRANCA_INTENSIVA";
                    else if (dias >= diasParaCobranca) newStatus = "COBRANCA";

                    if (oldStatus != newStatus)
                    {
                        inst.Status = newStatus;
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

                        // Se a parcela ficou inadimplente, o empréstimo/LoanRequest muda para Inadimplente
                        if (newStatus == "INADIMPLENTE" && inst.LoanRequest != null && inst.LoanRequest.Status != "Inadimplente")
                        {
                            string oldLoanStatus = inst.LoanRequest.Status;
                            inst.LoanRequest.Status = "Inadimplente";

                            _context.FinancialEventHistories.Add(new FinancialEventHistory
                            {
                                Id = Guid.NewGuid(),
                                Data = DateTime.UtcNow,
                                UsuarioResponsavel = "Sistema",
                                EventoExecutado = "ENTRADA_INADIMPLENCIA",
                                Observacao = $"Empréstimo {inst.LoanRequestId} mudou de {oldLoanStatus} para Inadimplente devido ao atraso da parcela {inst.NumeroParcela}.",
                                LoanRequestId = inst.LoanRequestId,
                                Valor = inst.LoanRequest.ValorSolicitado
                            });
                        }
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
    }
}
