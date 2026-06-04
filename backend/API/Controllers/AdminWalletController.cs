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
    [Route("api/admin")]
    [Authorize(Roles = "Administrador")]
    public class AdminWalletController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminWalletController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/admin/wallets
        [HttpGet("wallets")]
        public async Task<IActionResult> GetWallets()
        {
            var wallets = await _context.InvestorWallets
                .Include(w => w.User)
                .ThenInclude(u => u.Profile)
                .Select(w => new
                {
                    InvestorId = w.Id,
                    Nome = w.User != null && w.User.Profile != null ? w.User.Profile.Nome : string.Empty,
                    Email = w.User != null ? w.User.Email : string.Empty,
                    SaldoDisponivel = w.SaldoDisponivel,
                    SaldoInvestido = w.SaldoInvestido,
                    Rendimentos = w.Rendimentos,
                    TotalAportado = w.TotalAportado,
                    TotalSacado = w.TotalSacado,
                    PerfilInvestimento = w.PerfilInvestimento,
                    DataCriacao = w.DataCriacao,
                    DataAtualizacao = w.DataAtualizacao
                })
                .ToListAsync();

            return Ok(wallets);
        }

        // GET /api/admin/wallets/{investorId}/transactions
        [HttpGet("wallets/{investorId}/transactions")]
        public async Task<IActionResult> GetTransactions(Guid investorId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == investorId);
            if (user == null)
            {
                return NotFound(new { message = "Investidor não encontrado." });
            }

            var transactions = await _context.InvestorTransactions
                .Where(t => t.InvestorId == investorId)
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

        // POST /api/admin/wallet-adjustment
        [HttpPost("wallet-adjustment")]
        public async Task<IActionResult> AdjustWallet([FromBody] AdminWalletAdjustmentDto dto)
        {
            if (dto.Valor == 0)
            {
                return BadRequest(new { message = "O valor do ajuste não pode ser zero." });
            }

            var adminIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string adminEmail = "Administrador";
            if (!string.IsNullOrEmpty(adminIdString) && Guid.TryParse(adminIdString, out var adminId))
            {
                var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == adminId);
                if (adminUser != null)
                {
                    adminEmail = adminUser.Email;
                }
            }

            var wallet = await _context.InvestorWallets.FirstOrDefaultAsync(w => w.Id == dto.InvestorId);
            if (wallet == null)
            {
                // Se não existir carteira para o investidor aprovado, cria uma
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.InvestorId);
                if (user == null)
                {
                    return NotFound(new { message = "Investidor não encontrado." });
                }

                wallet = new InvestorWallet
                {
                    Id = dto.InvestorId,
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

            decimal saldoAnterior = wallet.SaldoDisponivel;
            decimal novoSaldo = wallet.SaldoDisponivel + dto.Valor;

            if (novoSaldo < 0)
            {
                return BadRequest(new { message = "Saldo disponível insuficiente após o ajuste solicitado." });
            }

            wallet.SaldoDisponivel = novoSaldo;
            wallet.DataAtualizacao = DateTime.UtcNow;

            var transaction = new InvestorTransaction
            {
                Id = Guid.NewGuid(),
                InvestorId = dto.InvestorId,
                Tipo = "AJUSTE_ADMIN",
                Valor = dto.Valor,
                SaldoAnterior = saldoAnterior,
                SaldoPosterior = wallet.SaldoDisponivel,
                DataMovimentacao = DateTime.UtcNow,
                Descricao = string.IsNullOrEmpty(dto.Descricao) ? "Ajuste administrativo manual" : dto.Descricao,
                UsuarioResponsavel = adminEmail
            };

            _context.InvestorTransactions.Add(transaction);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ajuste de carteira realizado com sucesso.", saldoDisponivel = wallet.SaldoDisponivel });
        }
    }
}
