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
    [Route("api/investor")]
    [Authorize(Roles = "Investidor")]
    public class InvestorController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly backend.Application.Services.InvestorYieldService _yieldService;
        private readonly ITreasuryService _treasuryService;

        public InvestorController(AppDbContext context, backend.Application.Services.InvestorYieldService yieldService, ITreasuryService treasuryService)
        {
            _context = context;
            _yieldService = yieldService;
            _treasuryService = treasuryService;
        }

        // GET /api/investor/wallet
        [HttpGet("wallet")]
        public async Task<IActionResult> GetWallet()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);

            if (wallet == null)
            {
                wallet = new InvestorWallet
                {
                    Id = userId,
                    SaldoDisponivel = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    TotalAportado = 0,
                    TotalSacado = 0,
                    PerfilInvestimento = "Conservador",
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.InvestorWallets.Add(wallet);
                await _context.SaveChangesAsync();
            }

            // Buscar investimentos
            var investments = await _context.Investments
                .Where(i => i.InvestorId == userId)
                .ToListAsync();

            // Buscar IDs de empréstimos com parcelas em atraso
            var loanIdsWithOverdue = await _context.LoanInstallments
                .Where(inst => inst.Status == "ATRASADO" || inst.Status == "COBRANCA" || inst.Status == "COBRANCA_INTENSIVA" || inst.Status == "INADIMPLENTE" || inst.Status == "Atrasada")
                .Select(inst => inst.LoanRequestId)
                .Distinct()
                .ToListAsync();

            decimal valorEmRisco = investments
                .Where(i => loanIdsWithOverdue.Contains(i.LoanId))
                .Sum(i => i.ValorInvestido);

            decimal valorProtegido = wallet.SaldoInvestido - valorEmRisco;
            if (valorProtegido < 0) valorProtegido = 0;

            // Buscar transações para perdas e coberturas
            var transactions = await _context.InvestorTransactions
                .Where(t => t.InvestorId == userId)
                .ToListAsync();

            decimal valorPerdido = Math.Abs(transactions
                .Where(t => t.Tipo.Equals("PERDA", StringComparison.OrdinalIgnoreCase) || 
                            t.Tipo.Equals("PREJUIZO", StringComparison.OrdinalIgnoreCase) || 
                            t.Tipo.Equals("PERDA_DEFINITIVA", StringComparison.OrdinalIgnoreCase) || 
                            (t.Tipo.Equals("AJUSTE_ADMIN", StringComparison.OrdinalIgnoreCase) && t.Valor < 0 && 
                             (t.Descricao.Contains("Perda", StringComparison.OrdinalIgnoreCase) || 
                              t.Descricao.Contains("prejuízo", StringComparison.OrdinalIgnoreCase) || 
                              t.Descricao.Contains("inadimplência", StringComparison.OrdinalIgnoreCase))))
                .Sum(t => t.Valor));

            decimal coberturasRecebidas = transactions
                .Where(t => t.Valor > 0 && 
                            (t.Tipo.Equals("RENDIMENTO", StringComparison.OrdinalIgnoreCase) || 
                             t.Tipo.Equals("COBERTURA_PARCELA_FUNDO", StringComparison.OrdinalIgnoreCase) || 
                             t.Tipo.Equals("COBERTURA_CONTRATO_FUNDO", StringComparison.OrdinalIgnoreCase) || 
                             t.Descricao.Contains("Fundo de Proteção", StringComparison.OrdinalIgnoreCase) || 
                             t.Descricao.Contains("Fundo de protecao", StringComparison.OrdinalIgnoreCase) || 
                             t.Descricao.Contains("Proteção", StringComparison.OrdinalIgnoreCase) || 
                             t.Descricao.Contains("cobertura", StringComparison.OrdinalIgnoreCase)))
                .Sum(t => t.Valor);

            decimal perdasDefinitivas = Math.Abs(transactions
                .Where(t => t.Tipo.Equals("PERDA_DEFINITIVA", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Valor));

            decimal coberturasFundo = transactions
                .Where(t => t.Tipo.Equals("COBERTURA_PARCELA_FUNDO", StringComparison.OrdinalIgnoreCase) || 
                            t.Tipo.Equals("COBERTURA_CONTRATO_FUNDO", StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Valor);

            return Ok(new InvestorWalletDto
            {
                SaldoDisponivel = wallet.SaldoDisponivel,
                SaldoComprometido = wallet.SaldoComprometido,
                SaldoInvestido = wallet.SaldoInvestido,
                Rendimentos = wallet.Rendimentos,
                TotalAportado = wallet.TotalAportado,
                TotalSacado = wallet.TotalSacado,
                PerfilInvestimento = wallet.PerfilInvestimento,
                ValorProtegido = valorProtegido,
                ValorEmRisco = valorEmRisco,
                ValorPerdido = valorPerdido,
                CoberturasRecebidas = coberturasRecebidas,
                PerdasDefinitivas = perdasDefinitivas,
                CoberturasFundo = coberturasFundo,
                Rentabilidade = wallet.Rentabilidade,
                RentabilidadeSaldoDisponivel = wallet.RentabilidadeSaldoDisponivel,
                RentabilidadeSaldoComprometido = wallet.RentabilidadeSaldoComprometido > 0 ? wallet.RentabilidadeSaldoComprometido : wallet.Rentabilidade,
                ObservacaoAdministrativa = wallet.ObservacaoAdministrativa,
                ProdutoInvestimentoId = wallet.ProdutoInvestimentoId,
                ProdutoInvestimentoNome = wallet.ProdutoInvestimentoNome,
                ProdutoEscolhido = wallet.ProdutoEscolhido,
                PodeAlterarProduto = wallet.PodeAlterarProduto
            });
        }

        // POST /api/investor/select-product
        [HttpPost("select-product")]
        public async Task<IActionResult> SelectProduct([FromBody] SelectInvestmentProductDto dto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);
            if (wallet == null)
            {
                return BadRequest(new { message = "Carteira não encontrada." });
            }

            if (wallet.ProdutoEscolhido && !wallet.PodeAlterarProduto)
            {
                return BadRequest(new { message = "A alteração de opção de investimento está bloqueada. Solicite liberação ao administrador caso queira alterar." });
            }

            var product = await _context.InvestmentProducts.FirstOrDefaultAsync(p => p.Id == dto.ProductId && p.Ativo);
            if (product == null)
            {
                return NotFound(new { message = "Produto de investimento não encontrado ou inativo." });
            }

            wallet.ProdutoInvestimentoId = product.Id;
            wallet.ProdutoInvestimentoNome = product.Nome;
            wallet.Rentabilidade = product.RentabilidadeMensal;
            wallet.RentabilidadeSaldoComprometido = product.RentabilidadeMensal;
            wallet.ProdutoEscolhido = true;
            wallet.PodeAlterarProduto = false;
            wallet.DataAtualizacao = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new 
            { 
                message = $"Opção '{product.Nome}' contratada com sucesso! Sua rentabilidade foi definida para {product.RentabilidadeMensal:N2}% a.m.", 
                rentabilidade = wallet.Rentabilidade,
                produto = wallet.ProdutoInvestimentoNome
            });
        }

        // POST /api/investor/deposit
        [HttpPost("deposit")]
        public async Task<IActionResult> MakeDeposit([FromBody] MakeDepositDto dto)
        {
            if (dto.Valor <= 0)
            {
                return BadRequest(new { message = "O valor do aporte deve ser maior que zero." });
            }

            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            decimal minInvestimento = config?.ValorMinimoInvestimento ?? 5000.00m;
            if (dto.Valor < minInvestimento)
            {
                return BadRequest(new { message = $"O valor do aporte mínimo permitido é de R$ {minInvestimento:N2}." });
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);
            if (wallet == null)
            {
                return BadRequest(new { message = "Carteira não encontrada." });
            }

            decimal saldoAnterior = wallet.SaldoDisponivel;
            wallet.SaldoDisponivel += dto.Valor;
            wallet.TotalAportado += dto.Valor;
            wallet.DataAtualizacao = DateTime.UtcNow;

            var transaction = new InvestorTransaction
            {
                Id = Guid.NewGuid(),
                InvestorId = userId,
                Tipo = "APORTE",
                Valor = dto.Valor,
                SaldoAnterior = saldoAnterior,
                SaldoPosterior = wallet.SaldoDisponivel,
                DataMovimentacao = DateTime.UtcNow,
                Descricao = $"Aporte simulado no valor de {dto.Valor:C2}",
                UsuarioResponsavel = "Investidor"
            };

            _context.InvestorTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Aporte realizado com sucesso.", saldoDisponivel = wallet.SaldoDisponivel });
        }

        // POST /api/investor/withdraw
        // GET /api/investor/yield/preview
        [HttpGet("yield/preview")]
        public async Task<IActionResult> GetYieldPreview()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            try
            {
                var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);
                decimal preview = await _yieldService.GetDailyYieldPreviewAsync(userId);
                return Ok(new 
                { 
                    rendimentoDiarioEstimado = preview, 
                    totalRendimentosPagos = wallet?.Rendimentos ?? 0 
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("withdraw")]
        public async Task<IActionResult> MakeWithdraw([FromBody] MakeWithdrawDto dto)
        {
            if (dto.Valor <= 0)
            {
                return BadRequest(new { message = "O valor do saque deve ser maior que zero." });
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            try
            {
                var request = await _treasuryService.RequestWithdrawalAsync(userId, dto.Valor, "Saque solicitado via painel do investidor", dto.ChavePix, dto.TipoChavePix);
                return Ok(new { message = "Solicitação de saque enviada com sucesso e aguardando aprovação.", id = request.Id });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // GET /api/investor/transactions
        [HttpGet("transactions")]
        public async Task<IActionResult> GetTransactions()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            var transactions = await _context.InvestorTransactions
                .Where(t => t.InvestorId == userId)
                .OrderByDescending(t => t.DataMovimentacao)
                .Select(t => new InvestorTransactionDto
                {
                    Id = t.Id,
                    Tipo = t.Tipo,
                    Valor = t.Valor,
                    SaldoAnterior = t.SaldoAnterior,
                    SaldoPosterior = t.SaldoPosterior,
                    DataMovimentacao = t.DataMovimentacao,
                    Descricao = t.Descricao,
                    UsuarioResponsavel = t.UsuarioResponsavel
                })
                .ToListAsync();

            return Ok(transactions);
        }

        // PUT /api/investor/profile
        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateInvestmentProfileDto dto)
        {
            var validProfiles = new[] { "Conservador", "Moderado", "Agressivo" };
            if (string.IsNullOrEmpty(dto.Perfil) || !validProfiles.Contains(dto.Perfil))
            {
                return BadRequest(new { message = "Perfil de investimento inválido. Escolha Conservador, Moderado ou Agressivo." });
            }

            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out var userId))
            {
                return Unauthorized(new { message = "Investidor não autenticado." });
            }

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);
            if (wallet == null)
            {
                return BadRequest(new { message = "Carteira não encontrada." });
            }

            wallet.PerfilInvestimento = dto.Perfil;
            wallet.DataAtualizacao = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Perfil de investimento atualizado com sucesso.", perfil = wallet.PerfilInvestimento });
        }
    }
}
