using backend.Application.DTOs;
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

        public InvestorController(AppDbContext context)
        {
            _context = context;
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

            return Ok(new InvestorWalletDto
            {
                SaldoDisponivel = wallet.SaldoDisponivel,
                SaldoInvestido = wallet.SaldoInvestido,
                Rendimentos = wallet.Rendimentos,
                TotalAportado = wallet.TotalAportado,
                TotalSacado = wallet.TotalSacado,
                PerfilInvestimento = wallet.PerfilInvestimento
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

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == userId);
            if (wallet == null)
            {
                return BadRequest(new { message = "Carteira não encontrada." });
            }

            if (wallet.SaldoDisponivel < dto.Valor)
            {
                return BadRequest(new { message = "Saldo disponível insuficiente para realizar o saque solicitado." });
            }

            decimal saldoAnterior = wallet.SaldoDisponivel;
            wallet.SaldoDisponivel -= dto.Valor;
            wallet.TotalSacado += dto.Valor;
            wallet.DataAtualizacao = DateTime.UtcNow;

            var transaction = new InvestorTransaction
            {
                Id = Guid.NewGuid(),
                InvestorId = userId,
                Tipo = "SAQUE",
                Valor = dto.Valor,
                SaldoAnterior = saldoAnterior,
                SaldoPosterior = wallet.SaldoDisponivel,
                DataMovimentacao = DateTime.UtcNow,
                Descricao = $"Saque simulado no valor de {dto.Valor:C2}",
                UsuarioResponsavel = "Investidor"
            };

            _context.InvestorTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Saque realizado com sucesso.", saldoDisponivel = wallet.SaldoDisponivel });
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
