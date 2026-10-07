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
        public async Task<IActionResult> GetWallets(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            var query = _context.InvestorWallets
                .AsNoTracking()
                .Include(w => w.User)
                .ThenInclude(u => u!.Profile)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(w => (w.User != null && w.User.Email.ToLower().Contains(term)) ||
                                         (w.User != null && w.User.Profile != null && w.User.Profile.Nome.ToLower().Contains(term)));
            }

            var totalCount = await query.CountAsync();
            page = Math.Max(1, page);
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderBy(w => w.User != null && w.User.Profile != null ? w.User.Profile.Nome : string.Empty)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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

            var result = new PagedResultDto<object>
            {
                Items = items.Cast<object>().ToList(),
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };

            return Ok(result);
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
            if (dto.Valor == 0 || decimal.Round(dto.Valor, 2) != dto.Valor || string.IsNullOrWhiteSpace(dto.Descricao))
            {
                return BadRequest(new { message = "Informe um ajuste com até duas casas decimais e uma justificativa." });
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
                if (user == null || user.Role != "Investidor")
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
            _context.LedgerTransactions.Add(new LedgerTransaction
            {
                Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, TransactionDate = DateTime.UtcNow,
                ContaOrigem = dto.Valor > 0 ? LedgerAccount.AdministrativeAdjustment : LedgerAccount.InvestorWallet,
                ContaDestino = dto.Valor > 0 ? LedgerAccount.InvestorWallet : LedgerAccount.AdministrativeAdjustment,
                Valor = Math.Abs(dto.Valor), TipoMovimentacao = LedgerTransactionType.AdministrativeAdjustment,
                ReferenceType = "InvestorTransaction", ReferenceId = transaction.Id, UserId = dto.InvestorId,
                Observacao = dto.Descricao.Trim(), CreatedBy = adminEmail
            });
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ajuste de carteira realizado com sucesso.", saldoDisponivel = wallet.SaldoDisponivel });
        }
    }
}
