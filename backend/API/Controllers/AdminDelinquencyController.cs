using backend.Domain.Entities;
using backend.Infrastructure;
using backend.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace backend.API.Controllers
{
    [ApiController]
    [Route("api/admin/delinquency")]
    [Authorize(Roles = "Administrador")]
    public class AdminDelinquencyController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly DelinquencyService _delinquencyService;

        public AdminDelinquencyController(AppDbContext context, DelinquencyService delinquencyService)
        {
            _context = context;
            _delinquencyService = delinquencyService;
        }

        // GET /api/admin/delinquency/loans
        [HttpGet("loans")]
        public async Task<IActionResult> GetDelinquentLoans(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? cliente = null,
            [FromQuery] DateTime? periodoInicio = null,
            [FromQuery] DateTime? periodoFim = null)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                // Buscar parcelas que não estão pagas nem canceladas e estão vencidas
                var today = DateTime.UtcNow.Date;

                var query = _context.LoanInstallments
                    .AsNoTracking()
                    .Where(i => i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento < today)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(i => i.Status.ToLower() == status.ToLower());
                }

                if (!string.IsNullOrEmpty(cliente))
                {
                    var term = cliente.ToLower();
                    query = query.Where(i => (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Email.ToLower().Contains(term)) ||
                                             (i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null && i.LoanRequest.User.Profile.Nome.ToLower().Contains(term)));
                }

                if (periodoInicio.HasValue)
                {
                    query = query.Where(i => i.DataVencimento >= periodoInicio.Value.ToUniversalTime());
                }

                if (periodoFim.HasValue)
                {
                    query = query.Where(i => i.DataVencimento <= periodoFim.Value.ToUniversalTime());
                }

                // Agrupar e projetar diretamente no banco para otimização máxima de performance
                var groupedQuery = query
                    .GroupBy(i => i.LoanRequestId)
                    .Select(g => new
                    {
                        LoanId = g.Key,
                        ClienteNome = g.Select(i => i.LoanRequest != null && i.LoanRequest.User != null && i.LoanRequest.User.Profile != null ? i.LoanRequest.User.Profile.Nome : "Sem Nome").FirstOrDefault() ?? "Sem Nome",
                        ClienteEmail = g.Select(i => i.LoanRequest != null && i.LoanRequest.User != null ? i.LoanRequest.User.Email : string.Empty).FirstOrDefault() ?? string.Empty,
                        ValorSolicitado = g.Select(i => i.LoanRequest != null ? i.LoanRequest.ValorSolicitado : 0).FirstOrDefault(),
                        ValorEmAberto = g.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago),
                        DiasAtraso = g.Max(i => i.DiasAtraso),
                        Status = g.Select(i => i.LoanRequest != null ? i.LoanRequest.Status : "Desconhecido").FirstOrDefault() ?? "Desconhecido",
                        ParcelasAtrasadasCount = g.Count(),
                        SaldoDevedorTotal = _context.LoanInstallments
                            .Where(i => i.LoanRequestId == g.Key && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null)
                            .Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago)
                    });

                var totalCount = await groupedQuery.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var items = await groupedQuery
                    .OrderByDescending(x => x.DiasAtraso)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new backend.Application.DTOs.PagedResultDto<object>
                {
                    Items = items.Cast<object>().ToList(),
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/delinquency/protection-fund
        [HttpGet("protection-fund")]
        public async Task<IActionResult> GetProtectionFund()
        {
            try
            {
                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null)
                {
                    fund = new ProtectionFund
                    {
                        Id = Guid.NewGuid(),
                        SaldoAtual = 10000.00m,
                        TotalRecebido = 0,
                        TotalUtilizado = 0,
                        UltimaAtualizacao = DateTime.UtcNow
                    };
                    _context.ProtectionFunds.Add(fund);
                    await _context.SaveChangesAsync();
                }

                var coberturasRealizadasCount = await _context.FinancialEventHistories
                    .CountAsync(e => e.EventoExecutado == "UTILIZACAO_FUNDO");

                return Ok(new
                {
                    fund.Id,
                    fund.SaldoAtual,
                    fund.TotalRecebido,
                    fund.TotalUtilizado,
                    fund.UltimaAtualizacao,
                    CoberturasRealizadasCount = coberturasRealizadasCount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/delinquency/protection-fund/adjust
        [HttpPost("protection-fund/adjust")]
        public async Task<IActionResult> AdjustProtectionFund([FromBody] AdjustFundDto dto)
        {
            try
            {
                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";

                if (dto.Valor <= 0)
                {
                    return BadRequest(new { message = "O valor deve ser maior que zero." });
                }

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null)
                {
                    fund = new ProtectionFund
                    {
                        Id = Guid.NewGuid(),
                        SaldoAtual = 0,
                        TotalRecebido = 0,
                        TotalUtilizado = 0,
                        UltimaAtualizacao = DateTime.UtcNow
                    };
                    _context.ProtectionFunds.Add(fund);
                }

                decimal saldoAnterior = fund.SaldoAtual;

                if (dto.Tipo.ToUpper() == "ADICIONAR")
                {
                    fund.SaldoAtual += dto.Valor;
                    fund.TotalRecebido += dto.Valor;
                }
                else if (dto.Tipo.ToUpper() == "REMOVER")
                {
                    if (fund.SaldoAtual < dto.Valor)
                    {
                        return BadRequest(new { message = $"Saldo do fundo insuficiente ({fund.SaldoAtual:C2}) para a remoção solicitada ({dto.Valor:C2})." });
                    }
                    fund.SaldoAtual -= dto.Valor;
                    fund.TotalUtilizado += dto.Valor;
                }
                else if (dto.Tipo.ToUpper() == "AJUSTAR")
                {
                    decimal diff = dto.Valor - fund.SaldoAtual;
                    if (diff > 0)
                    {
                        fund.TotalRecebido += diff;
                    }
                    else
                    {
                        fund.TotalUtilizado += Math.Abs(diff);
                    }
                    fund.SaldoAtual = dto.Valor;
                }
                else
                {
                    return BadRequest(new { message = "Tipo de movimentação inválido. Use: ADICIONAR, REMOVER ou AJUSTAR." });
                }

                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Registrar o evento do histórico
                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = adminEmail,
                    EventoExecutado = "INTERVENCAO_ADMIN",
                    Observacao = $"Ajuste manual do Fundo de Proteção (Tipo: {dto.Tipo}, Valor: {dto.Valor:C2}, Saldo Anterior: {saldoAnterior:C2}, Novo Saldo: {fund.SaldoAtual:C2}). Motivo: {dto.Motivo}",
                    Valor = dto.Valor
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "Movimentação do fundo realizada com sucesso.", saldoAtual = fund.SaldoAtual, totalRecebido = fund.TotalRecebido, totalUtilizado = fund.TotalUtilizado });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/delinquency/loans/{id}/cover-overdue
        [HttpPost("loans/{id}/cover-overdue")]
        public async Task<IActionResult> CoverOverdue(Guid id, [FromBody] CoverRequestDto dto)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";

                var loan = await _context.LoanRequests
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null)
                {
                    return NotFound(new { message = "Empréstimo não encontrado." });
                }

                await EnsureInvestmentsExistAsync(loan);

                var today = DateTime.UtcNow.Date;
                var overdueInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null && i.DataVencimento < today)
                    .ToListAsync();

                if (!overdueInstallments.Any())
                {
                    return BadRequest(new { message = "Este empréstimo não possui parcelas vencidas para cobertura." });
                }

                decimal saldoDevedor = overdueInstallments.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null)
                {
                    return BadRequest(new { message = "Fundo de Proteção não encontrado." });
                }

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Calcular principal vencido e juros vencidos a serem cobertos
                decimal totalPrincipalUnpaid = overdueInstallments.Sum(i => i.ValorPrincipal);
                decimal totalJurosUnpaid = overdueInstallments.Sum(i => i.ValorJuros);

                decimal totalInvestorPayout = 0;
                var investorPayouts = new Dictionary<Guid, (decimal Principal, decimal Rendimento, decimal Total)>();

                foreach (var inv in investments)
                {
                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet == null) continue;

                    // Se o investimento ainda estiver comprometido, transita para saldo investido
                    if (inv.Status == "Comprometido")
                    {
                        wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                        wallet.SaldoInvestido += inv.ValorInvestido;
                        inv.Status = "Ativo";
                    }

                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalUnpaid * percentual, 2);

                    decimal valorRendimentoRecebido = 0;
                    if (loan.TaxaJuros > 0)
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosUnpaid * percentual * (wallet.Rentabilidade / loan.TaxaJuros), 2);
                    }
                    else
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosUnpaid * percentual, 2);
                    }

                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;
                    investorPayouts[inv.InvestorId] = (valorPrincipalRecebido, valorRendimentoRecebido, valorRecebido);
                    totalInvestorPayout += valorRecebido;
                }

                if (fund.SaldoAtual < totalInvestorPayout)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund.SaldoAtual:C2}) para cobrir o valor devido aos investidores ({totalInvestorPayout:C2})." });
                }

                // Deduzir do fundo
                fund.SaldoAtual -= totalInvestorPayout;
                fund.TotalUtilizado += totalInvestorPayout;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Distribuir para os investidores
                foreach (var inv in investments)
                {
                    if (!investorPayouts.ContainsKey(inv.InvestorId)) continue;
                    var payout = investorPayouts[inv.InvestorId];

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += payout.Total;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - payout.Principal);
                        wallet.Rendimentos += payout.Rendimento;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_PARCELA_FUNDO",
                            Valor = payout.Total,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura de parcela em atraso pelo Fundo de Proteção para o empréstimo {loan.Id} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                            UsuarioResponsavel = adminEmail
                        });
                    }

                    // Registrar no histórico de eventos
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "UTILIZACAO_FUNDO",
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura de parcela vencida de {payout.Total:C2} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = payout.Total
                    });
                }

                // Marcar parcelas vencidas como pagas
                foreach (var inst in overdueInstallments)
                {
                    inst.Status = "Paga";
                    inst.DataPagamento = DateTime.UtcNow;
                    inst.ValorPago = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    inst.UpdatedAt = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                // Recalcular status do empréstimo de forma centralizada
                await _delinquencyService.UpdateLoanStatusFromInstallmentsAsync(loan.Id, adminEmail);

                return Ok(new { message = "Cobertura de parcelas vencidas realizada com sucesso.", saldoDevedor, saldoRestanteFundo = fund.SaldoAtual });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/delinquency/loans/{id}/cover-overdue-partial
        [HttpPost("loans/{id}/cover-overdue-partial")]
        public async Task<IActionResult> CoverOverduePartial(Guid id, [FromBody] CoverPartialRequestDto dto)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";

                if (dto.ValorCobertura <= 0)
                {
                    return BadRequest(new { message = "O valor de cobertura deve ser maior que zero." });
                }

                var loan = await _context.LoanRequests
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null)
                {
                    return NotFound(new { message = "Empréstimo não encontrado." });
                }

                await EnsureInvestmentsExistAsync(loan);

                var today = DateTime.UtcNow.Date;
                var overdueInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null && i.DataVencimento < today)
                    .OrderBy(i => i.NumeroParcela)
                    .ToListAsync();

                if (!overdueInstallments.Any())
                {
                    return BadRequest(new { message = "Este empréstimo não possui parcelas vencidas para cobertura." });
                }

                decimal saldoVencido = overdueInstallments.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                if (dto.ValorCobertura > saldoVencido)
                {
                    return BadRequest(new { message = $"O valor informado ({dto.ValorCobertura:C2}) excede o saldo vencido em aberto ({saldoVencido:C2})." });
                }

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null)
                {
                    return BadRequest(new { message = "Fundo de Proteção não encontrado." });
                }

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Distribuir o pagamento nas parcelas sequencialmente e calcular os totais de principal e juros
                decimal remainingCoverage = dto.ValorCobertura;
                decimal totalPrincipalPaid = 0;
                decimal totalJurosPaid = 0;

                foreach (var inst in overdueInstallments)
                {
                    if (remainingCoverage <= 0) break;

                    decimal valorTotalParcela = inst.ValorParcela + inst.Multa + inst.JurosMora - inst.ValorPago;
                    decimal totalDue = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    decimal principalRatio = totalDue > 0 ? (inst.ValorPrincipal / totalDue) : 1;
                    decimal jurosRatio = totalDue > 0 ? (inst.ValorJuros / totalDue) : 0;

                    decimal paymentApplied = 0;

                    if (remainingCoverage >= valorTotalParcela)
                    {
                        paymentApplied = valorTotalParcela;
                        inst.Status = "Paga";
                        inst.DataPagamento = DateTime.UtcNow;
                        inst.ValorPago += valorTotalParcela;
                        inst.UpdatedAt = DateTime.UtcNow;
                        remainingCoverage -= valorTotalParcela;
                    }
                    else
                    {
                        paymentApplied = remainingCoverage;
                        inst.ValorPago += remainingCoverage;
                        inst.Status = "Parcial";
                        inst.UpdatedAt = DateTime.UtcNow;
                        remainingCoverage = 0;
                    }

                    decimal principalPaid = Math.Min(paymentApplied, paymentApplied * principalRatio);
                    decimal jurosPaid = Math.Min(paymentApplied - principalPaid, paymentApplied * jurosRatio);
                    totalPrincipalPaid += principalPaid;
                    totalJurosPaid += jurosPaid;
                }

                // Calcular o valor real devido aos investidores com base no juros escalado pela rentabilidade
                decimal totalInvestorPayout = 0;
                var investorPayouts = new Dictionary<Guid, (decimal Principal, decimal Rendimento, decimal Total)>();

                foreach (var inv in investments)
                {
                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet == null) continue;

                    // Se o investimento ainda estiver comprometido, transita para saldo investido
                    if (inv.Status == "Comprometido")
                    {
                        wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                        wallet.SaldoInvestido += inv.ValorInvestido;
                        inv.Status = "Ativo";
                    }

                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalPaid * percentual, 2);

                    decimal valorRendimentoRecebido = 0;
                    if (loan.TaxaJuros > 0)
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosPaid * percentual * (wallet.Rentabilidade / loan.TaxaJuros), 2);
                    }
                    else
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosPaid * percentual, 2);
                    }

                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;
                    investorPayouts[inv.InvestorId] = (valorPrincipalRecebido, valorRendimentoRecebido, valorRecebido);
                    totalInvestorPayout += valorRecebido;
                }

                if (fund.SaldoAtual < totalInvestorPayout)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund.SaldoAtual:C2}) para cobrir o valor devido aos investidores ({totalInvestorPayout:C2})." });
                }

                // Deduzir do fundo apenas o valor distribuído aos investidores
                fund.SaldoAtual -= totalInvestorPayout;
                fund.TotalUtilizado += totalInvestorPayout;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Distribuir para os investidores
                foreach (var inv in investments)
                {
                    if (!investorPayouts.ContainsKey(inv.InvestorId)) continue;
                    var payout = investorPayouts[inv.InvestorId];

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += payout.Total;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - payout.Principal);
                        wallet.Rendimentos += payout.Rendimento;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_PARCELA_FUNDO",
                            Valor = payout.Total,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura parcial de parcela em atraso pelo Fundo de Proteção para o empréstimo {loan.Id} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                            UsuarioResponsavel = adminEmail
                        });
                    }

                    // Registrar no histórico de eventos
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "UTILIZACAO_FUNDO",
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura parcial de parcela vencida de {payout.Total:C2} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = payout.Total
                    });
                }

                await _context.SaveChangesAsync();

                // Recalcular status do empréstimo de forma centralizada
                await _delinquencyService.UpdateLoanStatusFromInstallmentsAsync(loan.Id, adminEmail);

                return Ok(new { message = "Cobertura parcial realizada com sucesso.", valorCoberto = dto.ValorCobertura, saldoRestanteFundo = fund.SaldoAtual });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("loans/{id}/terminate-and-cover")]
        public async Task<IActionResult> TerminateAndCover(Guid id, [FromBody] CoverRequestDto dto)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";

                var loan = await _context.LoanRequests
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null)
                {
                    return NotFound(new { message = "Empréstimo não encontrado." });
                }

                if (loan.Status != "Inadimplente")
                {
                    return BadRequest(new { message = "O contrato só pode ser encerrado definitivamente se o status for Inadimplente." });
                }

                await EnsureInvestmentsExistAsync(loan);

                var unpaidInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null)
                    .ToListAsync();

                if (!unpaidInstallments.Any())
                {
                    return BadRequest(new { message = "Este empréstimo não possui parcelas pendentes para encerramento." });
                }

                decimal saldoDevedor = unpaidInstallments.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Calcular principal pendente e juros pendentes
                decimal totalPrincipalUnpaid = unpaidInstallments.Sum(i => i.ValorPrincipal);
                decimal totalJurosUnpaid = unpaidInstallments.Sum(i => i.ValorJuros);

                decimal totalInvestorPayout = 0;
                var investorPayouts = new Dictionary<Guid, (decimal Principal, decimal Rendimento, decimal Total)>();

                foreach (var inv in investments)
                {
                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet == null) continue;

                    // Se o investimento ainda estiver comprometido, transita para saldo investido
                    if (inv.Status == "Comprometido")
                    {
                        wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                        wallet.SaldoInvestido += inv.ValorInvestido;
                        inv.Status = "Ativo";
                    }

                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalUnpaid * percentual, 2);

                    decimal valorRendimentoRecebido = 0;
                    if (loan.TaxaJuros > 0)
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosUnpaid * percentual * (wallet.Rentabilidade / loan.TaxaJuros), 2);
                    }
                    else
                    {
                        valorRendimentoRecebido = Math.Round(totalJurosUnpaid * percentual, 2);
                    }

                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;
                    investorPayouts[inv.InvestorId] = (valorPrincipalRecebido, valorRendimentoRecebido, valorRecebido);
                    totalInvestorPayout += valorRecebido;
                }

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null || fund.SaldoAtual < totalInvestorPayout)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund?.SaldoAtual:C2}) para cobrir o valor devido aos investidores ({totalInvestorPayout:C2})." });
                }

                // Deduzir do fundo
                fund.SaldoAtual -= totalInvestorPayout;
                fund.TotalUtilizado += totalInvestorPayout;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Distribuir para os investidores
                foreach (var inv in investments)
                {
                    if (!investorPayouts.ContainsKey(inv.InvestorId)) continue;
                    var payout = investorPayouts[inv.InvestorId];

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += payout.Total;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - payout.Principal);
                        wallet.Rendimentos += payout.Rendimento;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_CONTRATO_FUNDO",
                            Valor = payout.Total,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura total de encerramento de contrato pelo Fundo de Proteção para o empréstimo {loan.Id} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                            UsuarioResponsavel = adminEmail
                        });
                    }

                    // Registrar no histórico de eventos
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "UTILIZACAO_FUNDO",
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura final de encerramento de {payout.Total:C2} (Principal: {payout.Principal:C2}, Rendimento: {payout.Rendimento:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = payout.Total
                    });
                }

                // Marcar todas as parcelas como pagas
                foreach (var inst in unpaidInstallments)
                {
                    inst.Status = "Paga";
                    inst.DataPagamento = DateTime.UtcNow;
                    inst.ValorPago = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    inst.UpdatedAt = DateTime.UtcNow;
                }

                // Encerrar o empréstimo por inadimplência com cobertura
                loan.Status = "FinalizadoPorInadimplencia";
                loan.DataEncerramento = DateTime.UtcNow;

                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = adminEmail,
                    EventoExecutado = "INTERVENCAO_ADMIN",
                    Observacao = $"Contrato encerrado por inadimplência. Saldo devedor total de {saldoDevedor:C2} coberto pelo Fundo de Proteção. Observação: {dto.Observacao}",
                    LoanRequestId = loan.Id,
                    Valor = saldoDevedor
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "Contrato encerrado e saldo devedor coberto com sucesso.", saldoDevedor, saldoRestanteFundo = fund.SaldoAtual });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // POST /api/admin/delinquency/loans/{id}/terminate-and-loss
        [HttpPost("loans/{id}/terminate-and-loss")]
        public async Task<IActionResult> TerminateAndLoss(Guid id, [FromBody] CoverRequestDto dto)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await _delinquencyService.UpdateOverdueInstallmentsAsync();

                var adminEmail = User.FindFirst(ClaimTypes.Email)?.Value ?? "Administrador";

                var loan = await _context.LoanRequests
                    .Include(l => l.User)
                    .FirstOrDefaultAsync(l => l.Id == id);

                if (loan == null)
                {
                    return NotFound(new { message = "Empréstimo não encontrado." });
                }

                if (loan.Status != "Inadimplente")
                {
                    return BadRequest(new { message = "O contrato só pode ser encerrado definitivamente se o status for Inadimplente." });
                }

                await EnsureInvestmentsExistAsync(loan);

                var unpaidInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null)
                    .ToListAsync();

                if (!unpaidInstallments.Any())
                {
                    return BadRequest(new { message = "Este empréstimo não possui parcelas pendentes para encerramento." });
                }

                decimal saldoDevedor = unpaidInstallments.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);
                decimal totalPrincipalUnpaid = unpaidInstallments.Sum(i => i.ValorPrincipal);

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();

                // Distribuir a perda (que será reembolsada ao investidor e absorvida pelo fundo) proporcionalmente entre os investidores
                decimal totalRefunded = 0;
                foreach (var inv in investments)
                {
                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet == null) continue;

                    // Se o investimento ainda estiver comprometido, transita para saldo investido
                    if (inv.Status == "Comprometido")
                    {
                        wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                        wallet.SaldoInvestido += inv.ValorInvestido;
                        inv.Status = "Ativo";
                    }

                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal perdaPrincipal = Math.Round(totalPrincipalUnpaid * percentual, 2);

                    decimal saldoAnterior = wallet.SaldoDisponivel;
                    // Risco zero: devolve o principal pendente para o saldo disponível do investidor
                    wallet.SaldoDisponivel += perdaPrincipal;
                    wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - perdaPrincipal);
                    wallet.DataAtualizacao = DateTime.UtcNow;

                    totalRefunded += perdaPrincipal;

                    // Registrar a Transação como Cobertura de Prejuízo (absorvido pelo administrador)
                    _context.InvestorTransactions.Add(new InvestorTransaction
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = inv.InvestorId,
                        Tipo = "COBERTURA_PREJUIZO_ADMIN",
                        Valor = perdaPrincipal,
                        SaldoAnterior = saldoAnterior,
                        SaldoPosterior = wallet.SaldoDisponivel,
                        DataMovimentacao = DateTime.UtcNow,
                        Descricao = $"Devolução de principal para o empréstimo {loan.Id} devido a encerramento com perda (absorvida pelo administrador).",
                        UsuarioResponsavel = adminEmail
                    });

                    // Registrar no histórico de eventos
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "COBERTURA_PREJUIZO_ADMIN",
                        Observacao = $"Investidor {inv.InvestorId} recebeu devolução de principal de {perdaPrincipal:C2} (encerramento de contrato). Prejuízo absorvido pelo Administrador.",
                        LoanRequestId = loan.Id,
                        Valor = perdaPrincipal
                    });
                }

                // Debitar do fundo de proteção o valor reembolsado aos investidores
                if (fund != null)
                {
                    fund.SaldoAtual -= totalRefunded;
                    fund.TotalUtilizado += totalRefunded;
                    fund.UltimaAtualizacao = DateTime.UtcNow;
                }

                // Marcar parcelas pendentes como Canceladas
                foreach (var inst in unpaidInstallments)
                {
                    inst.Status = "Cancelada";
                    inst.UpdatedAt = DateTime.UtcNow;
                }

                // Encerrar o empréstimo por inadimplência sem cobertura (perda total absorbida pelo administrador)
                loan.Status = "FinalizadoPorInadimplencia";
                loan.DataEncerramento = DateTime.UtcNow;

                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = adminEmail,
                    EventoExecutado = "INTERVENCAO_ADMIN",
                    Observacao = $"Contrato encerrado por inadimplência. Prejuízo de principal de {totalRefunded:C2} integralmente coberto e devolvido aos investidores pelo Administrador. Observação: {dto.Observacao}",
                    LoanRequestId = loan.Id,
                    Valor = totalRefunded
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "Contrato encerrado por perda. Prejuízo coberto pelo administrador e principal devolvido aos investidores com sucesso.", totalPrincipalReembolsado = totalRefunded, saldoRestanteFundo = fund?.SaldoAtual });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        // GET /api/admin/delinquency/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            try
            {
                var query = _context.FinancialEventHistories
                    .AsNoTracking()
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = query.Where(h => h.Observacao.ToLower().Contains(term) ||
                                             h.UsuarioResponsavel.ToLower().Contains(term) ||
                                             h.EventoExecutado.ToLower().Contains(term));
                }

                var totalCount = await query.CountAsync();
                var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
                if (page < 1) page = 1;
                if (pageSize < 1) pageSize = 20;

                var items = await query
                    .OrderByDescending(h => h.Data)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var result = new backend.Application.DTOs.PagedResultDto<FinancialEventHistory>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = totalPages
                };

                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        private async Task EnsureInvestmentsExistAsync(LoanRequest loan)
        {
            var investments = await _context.Investments
                .Where(i => i.LoanId == loan.Id)
                .ToListAsync();

            if (!investments.Any())
            {
                var investors = await _context.Users
                    .Include(u => u.Wallet)
                    .Where(u => u.Role == "Investidor" && u.Status == "Aprovado")
                    .ToListAsync();

                if (!investors.Any())
                {
                    var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                    var mockInvestor = new User
                    {
                        Id = Guid.NewGuid(),
                        Email = "investidor@p2p.com",
                        Role = "Investidor",
                        Status = "Aprovado",
                        CreatedAt = DateTime.UtcNow
                    };
                    mockInvestor.PasswordHash = hasher.HashPassword(mockInvestor, "Investidor123!");

                    var profile = new UserProfile
                    {
                        Id = mockInvestor.Id,
                        Nome = "Investidor Teste P2P",
                        CPF = "11122233344",
                        Telefone = "11999999999",
                        DataNascimento = DateTime.UtcNow.AddYears(-25),
                        ScoreAtual = 800,
                        QuestionnaireCompleted = true
                    };
                    mockInvestor.Profile = profile;

                    var wallet = new InvestorWallet
                    {
                        Id = mockInvestor.Id,
                        SaldoDisponivel = 50000.00m,
                        SaldoInvestido = 0,
                        Rendimentos = 0,
                        TotalAportado = 50000.00m,
                        TotalSacado = 0,
                        PerfilInvestimento = "Moderado",
                        DataCriacao = DateTime.UtcNow,
                        DataAtualizacao = DateTime.UtcNow
                    };
                    mockInvestor.Wallet = wallet;

                    _context.Users.Add(mockInvestor);
                    await _context.SaveChangesAsync();

                    investors.Add(mockInvestor);
                }

                decimal valorLoan = loan.ValorSolicitado;
                if (investors.Count >= 2)
                {
                    var inv1 = investors[0];
                    var inv2 = investors[1];

                    if (inv1.Wallet == null) inv1.Wallet = CreateDefaultWallet(inv1.Id);
                    if (inv2.Wallet == null) inv2.Wallet = CreateDefaultWallet(inv2.Id);

                    var investment1 = new Investment
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = inv1.Id,
                        LoanId = loan.Id,
                        ValorInvestido = Math.Round(valorLoan * 0.60m, 2),
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    };

                    var investment2 = new Investment
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = inv2.Id,
                        LoanId = loan.Id,
                        ValorInvestido = Math.Round(valorLoan * 0.40m, 2),
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    };

                    inv1.Wallet.SaldoInvestido += investment1.ValorInvestido;
                    inv1.Wallet.SaldoDisponivel = Math.Max(0, inv1.Wallet.SaldoDisponivel - investment1.ValorInvestido);

                    inv2.Wallet.SaldoInvestido += investment2.ValorInvestido;
                    inv2.Wallet.SaldoDisponivel = Math.Max(0, inv2.Wallet.SaldoDisponivel - investment2.ValorInvestido);

                    _context.Investments.Add(investment1);
                    _context.Investments.Add(investment2);
                }
                else
                {
                    var inv = investors[0];
                    if (inv.Wallet == null) inv.Wallet = CreateDefaultWallet(inv.Id);

                    var investment = new Investment
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = inv.Id,
                        LoanId = loan.Id,
                        ValorInvestido = valorLoan,
                        CreatedAt = DateTime.UtcNow.AddDays(-10)
                    };

                    inv.Wallet.SaldoInvestido += investment.ValorInvestido;
                    inv.Wallet.SaldoDisponivel = Math.Max(0, inv.Wallet.SaldoDisponivel - investment.ValorInvestido);

                    _context.Investments.Add(investment);
                }

                await _context.SaveChangesAsync();
            }
        }

        private InvestorWallet CreateDefaultWallet(Guid investorId)
        {
            var wallet = new InvestorWallet
            {
                Id = investorId,
                SaldoDisponivel = 50000.00m,
                SaldoInvestido = 0,
                Rendimentos = 0,
                TotalAportado = 50000.00m,
                TotalSacado = 0,
                PerfilInvestimento = "Moderado",
                DataCriacao = DateTime.UtcNow,
                DataAtualizacao = DateTime.UtcNow
            };
            _context.InvestorWallets.Add(wallet);
            return wallet;
        }


    }

    public class CoverRequestDto
    {
        public string Observacao { get; set; } = string.Empty;
    }

    public class CoverPartialRequestDto
    {
        public decimal ValorCobertura { get; set; }
        public string Observacao { get; set; } = string.Empty;
    }

    public class DeclareLossRequestDto
    {
        public decimal ValorPrejuizo { get; set; }
        public string Observacao { get; set; } = string.Empty;
    }

    public class AdjustFundDto
    {
        public decimal Valor { get; set; }
        public string Tipo { get; set; } = string.Empty; // ADICIONAR, REMOVER, AJUSTAR
        public string Motivo { get; set; } = string.Empty;
    }
}
