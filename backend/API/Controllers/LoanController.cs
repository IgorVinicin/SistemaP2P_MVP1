using backend.Application.DTOs;
using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
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
    [Route("api/loans")]
    [Authorize]
    public class LoanController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CreditValidationService _creditService;

        public LoanController(AppDbContext context, CreditValidationService creditService)
        {
            _context = context;
            _creditService = creditService;
        }

        // GET /api/loans/my
        [HttpGet("my")]
        public async Task<IActionResult> GetMyLoans()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var loans = await _context.LoanRequests
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.DataCriacao)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    l.Status,
                    TaxaJuros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                    l.ValorTotal,
                    Risco = l.NivelRisco,
                    l.Finalidade,
                    l.Observacoes,
                    l.DataCriacao,
                    l.DataSolicitacao,
                    l.DataAprovacao,
                    l.ObservacaoAdmin,
                    l.TipoEmprestimo,
                    l.Periodicidade,
                    TemParcelaAtrasada = _context.LoanInstallments.Any(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date),
                    DiasAtrasoMax = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).Select(i => (int?)(DateTime.UtcNow.Date - i.DataVencimento.Date).TotalDays).Max() ?? 0,
                    ParcelaAtrasadaFormatada = _context.LoanInstallments.Where(i => i.LoanRequestId == l.Id && i.Status != "Paga" && i.Status != "Cancelada" && i.DataVencimento.Date < DateTime.UtcNow.Date).OrderBy(i => i.NumeroParcela).Select(i => i.NumeroParcela + "/" + (l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas)).FirstOrDefault()
                })
                .ToListAsync();

            return Ok(loans);
        }

        // GET /api/loans/history
        [HttpGet("history")]
        public async Task<IActionResult> GetLoanHistory()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var loans = await _context.LoanRequests
                .Where(l => l.UserId == userId)
                .OrderByDescending(l => l.DataCriacao)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    QuantidadeParcelas = l.QuantidadeParcelas > 0 ? l.QuantidadeParcelas : l.Parcelas,
                    l.Status,
                    TaxaJuros = l.TaxaJuros > 0 ? l.TaxaJuros : l.TaxaJurosAplicada,
                    l.ValorTotal,
                    Risco = l.NivelRisco,
                    l.Finalidade,
                    l.Observacoes,
                    l.DataCriacao,
                    l.DataSolicitacao,
                    l.DataAprovacao,
                    l.DataEncerramento,
                    l.ObservacaoAdmin,
                    l.TipoEmprestimo,
                    l.Periodicidade
                })
                .ToListAsync();

            return Ok(loans);
        }

        // GET /api/loans/eligibility
        [HttpGet("eligibility")]
        public async Task<IActionResult> GetEligibility()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Usuário não autenticado." });
            }

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.Profile == null)
            {
                return NotFound(new { message = "Perfil de usuário não encontrado." });
            }

            // Regra 1: Apenas usuários Aprovados
            if (user.Status != "Aprovado")
            {
                return Ok(new LoanEligibilityDto
                {
                    PodeSolicitar = false,
                    MensagemBlock = "Seu cadastro ainda não está aprovado para solicitar empréstimos."
                });
            }

            // Regra 1.5: Impedir se o usuário possuir status Inadimplente
            var hasInadimplencia = await _context.LoanRequests
                .AnyAsync(l => l.UserId == userId && l.Status == "Inadimplente");
            if (hasInadimplencia)
            {
                return Ok(new LoanEligibilityDto
                {
                    PodeSolicitar = false,
                    MensagemBlock = "Você possui pendências financeiras e precisa regularizar sua situação para utilizar novamente a plataforma."
                });
            }

            var renovacaoVal = await _creditService.VerificarElegibilidadeRenovacao(userId);
            
            var statusAtivos = new[] { "pendente", "emanalise", "aprovado", "ativo", "empagamento", "inadimplente" };
            var temEmprestimoAtivo = await _context.LoanRequests
                .AnyAsync(l => l.UserId == userId && statusAtivos.Contains(l.Status.ToLower()));

            if (!renovacaoVal.PodeSolicitar)
            {
                return Ok(new LoanEligibilityDto
                {
                    PodeSolicitar = false,
                    MensagemBlock = renovacaoVal.MensagemBlock,
                    ParcelasPagas = renovacaoVal.ParcelasPagas,
                    MinimoExigido = renovacaoVal.MinimoExigido,
                    TemEmprestimoAtivo = temEmprestimoAtivo
                });
            }

            var validation = await _creditService.ValidarCreditoDoUsuario(user.Profile.ScoreAtual, userId);

            if (!validation.Aprovado)
            {
                // Ajustar a mensagem conforme solicitado se for por causa do score mínimo
                string msg = validation.Mensagem;
                if (msg.Contains("score") || msg.Contains("score atual"))
                {
                    msg = "Seu score atual não permite solicitar empréstimos.";
                }

                return Ok(new LoanEligibilityDto
                {
                    PodeSolicitar = false,
                    MensagemBlock = msg,
                    ParcelasPagas = renovacaoVal.ParcelasPagas,
                    MinimoExigido = renovacaoVal.MinimoExigido,
                    TemEmprestimoAtivo = temEmprestimoAtivo
                });
            }

            return Ok(new LoanEligibilityDto
            {
                Risco = validation.NivelRisco,
                TaxaJuros = validation.TaxaJuros,
                LimiteDisponivel = validation.LimiteDisponivel,
                PodeSolicitar = true,
                ParcelasPagas = renovacaoVal.ParcelasPagas,
                MinimoExigido = renovacaoVal.MinimoExigido,
                TemEmprestimoAtivo = temEmprestimoAtivo
            });
        }

        // POST /api/loans/request
        [HttpPost("request")]
        [Authorize(Roles = "Cliente")]
        public async Task<IActionResult> RequestLoan([FromBody] RequestLoanDto dto)
        {
            if (dto.ValorSolicitado <= 0 || decimal.Round(dto.ValorSolicitado, 2) != dto.ValorSolicitado)
                return BadRequest(new { message = "Informe um valor positivo com no máximo duas casas decimais." });
            try
            {
                var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
                {
                    return Unauthorized(new { message = "Usuário não autenticado." });
                }

                var user = await _context.Users
                    .Include(u => u.Profile)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null || user.Profile == null)
                {
                    return NotFound(new { message = "Perfil de usuário não encontrado." });
                }

                // Regra 1: Apenas usuários Aprovados
                if (user.Status != "Aprovado")
                {
                    return BadRequest(new { message = "Seu cadastro ainda não está aprovado para solicitar empréstimos." });
                }

                // Regra 1.5: Impedir se o usuário possuir status Inadimplente
                var hasInadimplencia = await _context.LoanRequests
                    .AnyAsync(l => l.UserId == userId && l.Status == "Inadimplente");
                if (hasInadimplencia)
                {
                    return BadRequest(new { message = "Você possui pendências financeiras e precisa regularizar sua situação para utilizar novamente a plataforma." });
                }

                // Regra: Impedir múltiplos empréstimos ativos concorrentes
                var renovacaoVal = await _creditService.VerificarElegibilidadeRenovacao(userId);
                if (!renovacaoVal.PodeSolicitar)
                {
                    return BadRequest(new { message = renovacaoVal.MensagemBlock });
                }

                // Regra 2: Validação de Score
                var validation = await _creditService.ValidarCreditoDoUsuario(user.Profile.ScoreAtual, userId);
                if (!validation.Aprovado)
                {
                    string msg = validation.Mensagem;
                    if (msg.Contains("score") || msg.Contains("score atual"))
                    {
                        msg = "Seu score atual não permite solicitar empréstimos.";
                    }
                    return BadRequest(new { message = msg });
                }

                // Regra 3: Validação de Limite
                if (!dto.SolicitaAumentoLimite && dto.ValorSolicitado > validation.LimiteDisponivel)
                {
                    return BadRequest(new { message = "Valor solicitado excede o limite disponível para seu perfil. Marque a opção de solicitar aumento de limite se desejar prosseguir." });
                }

                if (dto.QuantidadeParcelas <= 0)
                {
                    return BadRequest(new { message = "Quantidade de parcelas deve ser maior que zero." });
                }

                // Calcular ValorTotal automaticamente utilizando a taxa de juros e parcelas (Tabela Price)
                var priceCalc = new PriceCalculationService();
                var simulatedInstallments = priceCalc.CalculateInstallments(Guid.Empty, dto.ValorSolicitado, dto.QuantidadeParcelas, validation.TaxaJuros, "Mensal");
                decimal valorTotalCalculado = simulatedInstallments.Sum(inst => inst.ValorParcela);

                // Obter as taxas globais no momento da criação para congelá-las no contrato
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                decimal multaAtraso = config?.PercentualMultaAtraso ?? 2.00m;
                decimal jurosDiario = config?.PercentualJurosDiario ?? 0.033m;

                // Criar a solicitação
                var loan = new LoanRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    ValorSolicitado = dto.ValorSolicitado,
                    Parcelas = dto.QuantidadeParcelas,
                    QuantidadeParcelas = dto.QuantidadeParcelas,
                    Status = "Pendente",
                    ScoreNaSolicitacao = user.Profile.ScoreAtual,
                    ScoreNoMomento = user.Profile.ScoreAtual,
                    Finalidade = dto.Finalidade,
                    Observacoes = dto.Observacoes,
                    TaxaJurosAplicada = validation.TaxaJuros,
                    TaxaJuros = validation.TaxaJuros,
                    ValorTotal = valorTotalCalculado,
                    NivelRisco = validation.NivelRisco,
                    PercentualMultaAtrasoContrato = multaAtraso,
                    PercentualJurosDiarioContrato = jurosDiario,
                    SolicitaAumentoLimite = dto.SolicitaAumentoLimite,
                    LimiteNoMomentoSolicitacao = validation.LimiteDisponivel,
                    CreatedAt = DateTime.UtcNow,
                    DataCriacao = DateTime.UtcNow,
                    DataSolicitacao = DateTime.UtcNow
                };

                _context.LoanRequests.Add(loan);
                await _context.SaveChangesAsync();

                return Ok(new
                {
                    loan.Id,
                    loan.UserId,
                    loan.ValorSolicitado,
                    loan.QuantidadeParcelas,
                    TaxaJuros = loan.TaxaJuros,
                    loan.ValorTotal,
                    loan.Status,
                    loan.DataSolicitacao,
                    loan.ObservacaoAdmin
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}
