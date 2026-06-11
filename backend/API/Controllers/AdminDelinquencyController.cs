using backend.Domain.Entities;
using backend.Infrastructure;
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

        public AdminDelinquencyController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/admin/delinquency/loans
        [HttpGet("loans")]
        public async Task<IActionResult> GetDelinquentLoans(
            [FromQuery] string? status,
            [FromQuery] string? cliente,
            [FromQuery] DateTime? periodoInicio,
            [FromQuery] DateTime? periodoFim)
        {
            try
            {
                // Atualizar parcelas em atraso primeiro
                await UpdateOverdueInstallmentsAsync();

                // Buscar parcelas que não estão pagas nem canceladas e estão vencidas
                var today = DateTime.UtcNow.Date;

                var query = _context.LoanInstallments
                    .Include(i => i.LoanRequest)
                    .ThenInclude(l => l!.User)
                    .ThenInclude(u => u!.Profile)
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

                var installments = await query.ToListAsync();

                // Agrupar por empréstimo para exibir o resumo por empréstimo delinquente
                var list = installments
                    .GroupBy(i => i.LoanRequestId)
                    .Select(g =>
                    {
                        var first = g.First();
                        var loan = first.LoanRequest;
                        var userProfile = loan?.User?.Profile;

                        var unpaidSum = _context.LoanInstallments
                            .Where(i => i.LoanRequestId == g.Key && i.Status != "Paga" && i.Status != "Cancelada" && i.DataPagamento == null)
                            .Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago);

                        return new
                        {
                            LoanId = g.Key,
                            ClienteNome = userProfile?.Nome ?? "Sem Nome",
                            ClienteEmail = loan?.User?.Email ?? string.Empty,
                            ValorSolicitado = loan?.ValorSolicitado ?? 0,
                            ValorEmAberto = g.Sum(i => i.ValorParcela + i.Multa + i.JurosMora - i.ValorPago),
                            DiasAtraso = g.Max(i => i.DiasAtraso),
                            Status = loan?.Status ?? "Desconhecido",
                            ParcelasAtrasadasCount = g.Count(),
                            SaldoDevedorTotal = unpaidSum
                        };
                    })
                    .ToList();

                return Ok(list);
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
                await UpdateOverdueInstallmentsAsync();

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
                if (fund == null || fund.SaldoAtual < saldoDevedor)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund?.SaldoAtual:C2}) para cobrir o saldo vencido ({saldoDevedor:C2})." });
                }

                // Deduzir do fundo
                fund.SaldoAtual -= saldoDevedor;
                fund.TotalUtilizado += saldoDevedor;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Calcular amortização de principal e rendimentos nas parcelas a serem cobertas
                decimal totalPrincipalPaid = 0;
                decimal totalInterestFinesPaid = 0;

                foreach (var inst in overdueInstallments)
                {
                    decimal instPayment = inst.ValorParcela + inst.Multa + inst.JurosMora - inst.ValorPago;
                    decimal totalDue = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    if (totalDue > 0)
                    {
                        decimal principalRatio = inst.ValorPrincipal / totalDue;
                        decimal principalPaid = Math.Min(instPayment, instPayment * principalRatio);
                        totalPrincipalPaid += principalPaid;
                        totalInterestFinesPaid += (instPayment - principalPaid);
                    }
                    else
                    {
                        totalInterestFinesPaid += instPayment;
                    }
                }

                // Distribuir para os investidores proporcionalmente
                foreach (var inv in investments)
                {
                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalPaid * percentual, 2);
                    decimal valorRendimentoRecebido = Math.Round(totalInterestFinesPaid * percentual, 2);
                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += valorRecebido;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - valorPrincipalRecebido);
                        wallet.Rendimentos += valorRendimentoRecebido;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_PARCELA_FUNDO",
                            Valor = valorRecebido,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura de parcela em atraso pelo Fundo de Proteção para o empréstimo {loan.Id}.",
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
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura de parcela vencida de {valorRecebido:C2} (Principal: {valorPrincipalRecebido:C2}, Rendimento: {valorRendimentoRecebido:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = valorRecebido
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

                // Verificar e atualizar o status do empréstimo
                var allInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == loan.Id)
                    .ToListAsync();

                var hasInadimplente = allInstallments.Any(i => i.Status == "INADIMPLENTE" || i.Status == "COBRANCA_INTENSIVA");
                var hasOverdue = allInstallments.Any(i => i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "Atrasada" || i.Status == "Parcial");
                var allPaid = allInstallments.All(i => i.Status == "Paga" || i.Status == "Cancelada");

                if (allPaid)
                {
                    loan.Status = "Quitado";
                    loan.DataEncerramento = DateTime.UtcNow;
                }
                else if (!hasInadimplente && !hasOverdue && loan.Status == "Inadimplente")
                {
                    loan.Status = "EmPagamento";
                }

                await _context.SaveChangesAsync();

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
                await UpdateOverdueInstallmentsAsync();

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
                if (fund == null || fund.SaldoAtual < dto.ValorCobertura)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund?.SaldoAtual:C2}) para cobrir o valor solicitado ({dto.ValorCobertura:C2})." });
                }

                // Deduzir do fundo
                fund.SaldoAtual -= dto.ValorCobertura;
                fund.TotalUtilizado += dto.ValorCobertura;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Distribuir o pagamento nas parcelas sequencialmente e calcular os totais de principal e rendimentos
                decimal remainingCoverage = dto.ValorCobertura;
                decimal totalPrincipalPaid = 0;
                decimal totalInterestFinesPaid = 0;

                foreach (var inst in overdueInstallments)
                {
                    if (remainingCoverage <= 0) break;

                    decimal valorTotalParcela = inst.ValorParcela + inst.Multa + inst.JurosMora - inst.ValorPago;
                    decimal totalDue = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    decimal principalRatio = totalDue > 0 ? (inst.ValorPrincipal / totalDue) : 1;

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
                    totalPrincipalPaid += principalPaid;
                    totalInterestFinesPaid += (paymentApplied - principalPaid);
                }

                // Distribuir para os investidores proporcionalmente
                foreach (var inv in investments)
                {
                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalPaid * percentual, 2);
                    decimal valorRendimentoRecebido = Math.Round(totalInterestFinesPaid * percentual, 2);
                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += valorRecebido;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - valorPrincipalRecebido);
                        wallet.Rendimentos += valorRendimentoRecebido;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_PARCELA_FUNDO",
                            Valor = valorRecebido,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura parcial de parcela em atraso pelo Fundo de Proteção para o empréstimo {loan.Id}.",
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
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura parcial de parcela vencida de {valorRecebido:C2} (Principal: {valorPrincipalRecebido:C2}, Rendimento: {valorRendimentoRecebido:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = valorRecebido
                    });
                }

                await _context.SaveChangesAsync();

                // Verificar e atualizar o status do empréstimo
                var allInstallments = await _context.LoanInstallments
                    .Where(i => i.LoanRequestId == loan.Id)
                    .ToListAsync();

                var hasInadimplente = allInstallments.Any(i => i.Status == "INADIMPLENTE" || i.Status == "COBRANCA_INTENSIVA");
                var hasOverdue = allInstallments.Any(i => i.Status == "ATRASADO" || i.Status == "COBRANCA" || i.Status == "Atrasada" || i.Status == "Parcial");
                var allPaid = allInstallments.All(i => i.Status == "Paga" || i.Status == "Cancelada");

                if (allPaid)
                {
                    loan.Status = "Quitado";
                    loan.DataEncerramento = DateTime.UtcNow;
                }
                else if (!hasInadimplente && !hasOverdue && loan.Status == "Inadimplente")
                {
                    loan.Status = "EmPagamento";
                }

                await _context.SaveChangesAsync();

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
                await UpdateOverdueInstallmentsAsync();

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

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null || fund.SaldoAtual < saldoDevedor)
                {
                    return BadRequest(new { message = $"Saldo do Fundo de Proteção insuficiente ({fund?.SaldoAtual:C2}) para cobrir o saldo devedor restante ({saldoDevedor:C2})." });
                }

                // Deduzir do fundo
                fund.SaldoAtual -= saldoDevedor;
                fund.TotalUtilizado += saldoDevedor;
                fund.UltimaAtualizacao = DateTime.UtcNow;

                // Buscar investimentos associados ao Loan
                var investments = await _context.Investments
                    .Where(i => i.LoanId == id)
                    .ToListAsync();

                decimal totalInvestido = investments.Sum(i => i.ValorInvestido);

                // Calcular amortização de principal e rendimentos nas parcelas
                decimal totalPrincipalPaid = 0;
                decimal totalInterestFinesPaid = 0;

                foreach (var inst in unpaidInstallments)
                {
                    decimal instPayment = inst.ValorParcela + inst.Multa + inst.JurosMora - inst.ValorPago;
                    decimal totalDue = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    if (totalDue > 0)
                    {
                        decimal principalRatio = inst.ValorPrincipal / totalDue;
                        decimal principalPaid = Math.Min(instPayment, instPayment * principalRatio);
                        totalPrincipalPaid += principalPaid;
                        totalInterestFinesPaid += (instPayment - principalPaid);
                    }
                    else
                    {
                        totalInterestFinesPaid += instPayment;
                    }
                }

                // Distribuir para os investidores proporcionalmente
                foreach (var inv in investments)
                {
                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal valorPrincipalRecebido = Math.Round(totalPrincipalPaid * percentual, 2);
                    decimal valorRendimentoRecebido = Math.Round(totalInterestFinesPaid * percentual, 2);
                    decimal valorRecebido = valorPrincipalRecebido + valorRendimentoRecebido;

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoDisponivel += valorRecebido;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - valorPrincipalRecebido);
                        wallet.Rendimentos += valorRendimentoRecebido;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar Transação do investidor
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "COBERTURA_CONTRATO_FUNDO",
                            Valor = valorRecebido,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Cobertura total de encerramento de contrato pelo Fundo de Proteção para o empréstimo {loan.Id}.",
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
                        Observacao = $"Investidor {inv.InvestorId} recebeu cobertura final de encerramento de {valorRecebido:C2} (Principal: {valorPrincipalRecebido:C2}, Rendimento: {valorRendimentoRecebido:C2}).",
                        LoanRequestId = loan.Id,
                        Valor = valorRecebido
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
                await UpdateOverdueInstallmentsAsync();

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

                // Distribuir a perda proporcionalmente entre os investidores
                foreach (var inv in investments)
                {
                    decimal percentual = inv.ValorInvestido / totalInvestido;
                    decimal perda = Math.Round(saldoDevedor * percentual, 2);
                    decimal perdaPrincipal = Math.Round(totalPrincipalUnpaid * percentual, 2);

                    var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == inv.InvestorId);
                    if (wallet != null)
                    {
                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - perdaPrincipal);
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        // Registrar a Transação como Perda Definitiva (disponível não muda pois o dinheiro já tinha sido investido no passado)
                        _context.InvestorTransactions.Add(new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = inv.InvestorId,
                            Tipo = "PERDA_DEFINITIVA",
                            Valor = -perda,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Perda definitiva por inadimplência no encerramento do empréstimo {loan.Id}. Motivo: {dto.Observacao}",
                            UsuarioResponsavel = adminEmail
                        });
                    }

                    // Registrar no histórico de eventos
                    _context.FinancialEventHistories.Add(new FinancialEventHistory
                    {
                        Id = Guid.NewGuid(),
                        Data = DateTime.UtcNow,
                        UsuarioResponsavel = adminEmail,
                        EventoExecutado = "PERDA_INVESTIDOR",
                        Observacao = $"Investidor {inv.InvestorId} registrou perda definitiva de {perda:C2} (encerramento de contrato). Principal amortizado perdido: {perdaPrincipal:C2}.",
                        LoanRequestId = loan.Id,
                        Valor = perda
                    });
                }

                // Marcar parcelas pendentes como Canceladas
                foreach (var inst in unpaidInstallments)
                {
                    inst.Status = "Cancelada";
                    inst.UpdatedAt = DateTime.UtcNow;
                }

                // Encerrar o empréstimo por inadimplência sem cobertura (perda total)
                loan.Status = "FinalizadoPorInadimplencia";
                loan.DataEncerramento = DateTime.UtcNow;

                _context.FinancialEventHistories.Add(new FinancialEventHistory
                {
                    Id = Guid.NewGuid(),
                    Data = DateTime.UtcNow,
                    UsuarioResponsavel = adminEmail,
                    EventoExecutado = "INTERVENCAO_ADMIN",
                    Observacao = $"Contrato encerrado por inadimplência. Saldo devedor total de {saldoDevedor:C2} rateado como PERDA DEFINITIVA entre investidores. Observação: {dto.Observacao}",
                    LoanRequestId = loan.Id,
                    Valor = saldoDevedor
                });

                await _context.SaveChangesAsync();

                return Ok(new { message = "Contrato encerrado e perda definitiva distribuída com sucesso.", saldoDevedor });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/admin/delinquency/history
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory()
        {
            try
            {
                var history = await _context.FinancialEventHistories
                    .OrderByDescending(h => h.Data)
                    .ToListAsync();

                return Ok(history);
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
                if (inst.DataVencimento < today)
                {
                    int dias = (int)(today - inst.DataVencimento.Date).TotalDays;
                    decimal novaMulta = Math.Round(inst.ValorParcela * (percentualMulta / 100m), 2);
                    decimal novoJuros = Math.Round(inst.ValorParcela * (percentualJurosDiario / 100m) * dias, 2);

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
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
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
