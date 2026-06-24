using backend.Application.Services;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;


namespace backend.API.Controllers
{
    /// <summary>
    /// CONTROLADOR TEMPORÁRIO DE DIAGNÓSTICO - REMOVER APÓS VALIDAÇÃO
    /// </summary>
    [ApiController]
    [Route("api/diagnostic")]
    [AllowAnonymous]
    public class DiagnosticController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DiagnosticController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Retorna o estado atual de todos os investidores e suas carteiras.
        /// GET /api/diagnostic/investors
        /// </summary>
        [HttpGet("investors")]
        public async Task<IActionResult> GetInvestors()
        {
            var users = await _context.Users
                .Include(u => u.Wallet)
                .Where(u => u.Role == "Investidor")
                .ToListAsync();

            var result = users.Select(u => new
            {
                u.Id,
                u.Email,
                u.Status,
                u.Role,
                TemCarteira = u.Wallet != null,
                SaldoDisponivel = u.Wallet != null ? u.Wallet.SaldoDisponivel : (decimal?)null,
                SaldoComprometido = u.Wallet != null ? u.Wallet.SaldoComprometido : (decimal?)null,
                SaldoInvestido = u.Wallet != null ? u.Wallet.SaldoInvestido : (decimal?)null,
                Rentabilidade = u.Wallet != null ? u.Wallet.Rentabilidade : (decimal?)null
            }).ToList();

            return Ok(new
            {
                TotalInvestidores = result.Count,
                ElegivelParaAlocacao = result.Count(x => x.Status == "Aprovado" && x.TemCarteira),
                Investidores = result
            });
        }

        /// <summary>
        /// Retorna o estado atual dos investimentos (alocações automáticas).
        /// GET /api/diagnostic/investments
        /// </summary>
        [HttpGet("investments")]
        public async Task<IActionResult> GetInvestments()
        {
            var investments = await _context.Investments
                .Include(i => i.Investor)
                .Include(i => i.Loan)
                .OrderByDescending(i => i.CreatedAt)
                .Take(50)
                .Select(i => new
                {
                    i.Id,
                    i.InvestorId,
                    InvestorEmail = i.Investor != null ? i.Investor.Email : "N/A",
                    i.LoanId,
                    LoanValor = i.Loan != null ? i.Loan.ValorSolicitado : 0,
                    LoanStatus = i.Loan != null ? i.Loan.Status : "N/A",
                    i.ValorInvestido,
                    i.Status,
                    i.CreatedAt
                })
                .ToListAsync();

            return Ok(new
            {
                TotalAlocacoes = investments.Count,
                Investimentos = investments
            });
        }

        /// <summary>
        /// Retorna empréstimos aprovados recentes.
        /// GET /api/diagnostic/loans
        /// </summary>
        [HttpGet("loans")]
        public async Task<IActionResult> GetLoans()
        {
            var loans = await _context.LoanRequests
                .Where(l => l.Status == "Aprovado" || l.Status == "Ativo" || l.Status == "EmPagamento")
                .OrderByDescending(l => l.DataAprovacao)
                .Take(20)
                .Select(l => new
                {
                    l.Id,
                    l.ValorSolicitado,
                    l.Status,
                    l.DataCriacao,
                    l.DataAprovacao,
                    TotalInvestidores = _context.Investments.Count(i => i.LoanId == l.Id)
                })
                .ToListAsync();

            return Ok(new
            {
                TotalEmprestimosAprovados = loans.Count,
                Emprestimos = loans
            });
        }

        /// <summary>
        /// Retorna transações de alocação automática registradas.
        /// GET /api/diagnostic/allocation-transactions
        /// </summary>
        [HttpGet("allocation-transactions")]
        public async Task<IActionResult> GetAllocationTransactions()
        {
            var txs = await _context.InvestorTransactions
                .Where(t => t.Tipo == "ALOCACAO_AUTOMATICA")
                .Include(t => t.Investor)
                .OrderByDescending(t => t.DataMovimentacao)
                .Take(50)
                .Select(t => new
                {
                    t.Id,
                    t.InvestorId,
                    InvestorEmail = t.Investor != null ? t.Investor.Email : "N/A",
                    t.Tipo,
                    t.Valor,
                    t.SaldoAnterior,
                    t.SaldoPosterior,
                    t.DataMovimentacao,
                    t.Descricao
                })
                .ToListAsync();

            return Ok(new
            {
                TotalTransacoesAlocacao = txs.Count,
                Transacoes = txs
            });
        }

        /// <summary>
        /// Simula a execução do motor de alocação para um empréstimo existente e retorna o diagnóstico
        /// sem persistir alterações.
        /// GET /api/diagnostic/simulate-allocation/{loanId}
        /// </summary>
        [HttpGet("simulate-allocation/{loanId}")]
        public async Task<IActionResult> SimulateAllocation(Guid loanId)
        {
            var loan = await _context.LoanRequests
                .Include(l => l.Investments)
                .FirstOrDefaultAsync(l => l.Id == loanId);

            if (loan == null)
                return NotFound(new { message = "Empréstimo não encontrado." });

            decimal loanAmount = loan.ValorSolicitado;

            // Todos investidores
            var allInvestors = await _context.Users
                .Include(u => u.Wallet)
                .Where(u => u.Role == "Investidor")
                .ToListAsync();

            // Investidores elegíveis
            var eligible = allInvestors
                .Where(u => u.Status == "Aprovado" && u.Wallet != null)
                .ToList();

            // Simular iteração
            var pool = eligible.ToList();
            bool stable = false;
            var allocations = new System.Collections.Generic.Dictionary<Guid, decimal>();

            int iterations = 0;
            while (!stable && pool.Any() && iterations < 100)
            {
                iterations++;
                int M = pool.Count;
                decimal sum = 0;
                allocations.Clear();

                for (int i = 0; i < M; i++)
                {
                    decimal share = i < M - 1 ? Math.Round(loanAmount / M, 2) : loanAmount - sum;
                    allocations[pool[i].Id] = share;
                    sum += share;
                }

                var tooLow = pool.Where(u => u.Wallet!.SaldoDisponivel < allocations[u.Id]).ToList();
                if (!tooLow.Any())
                    stable = true;
                else
                    foreach (var x in tooLow) pool.Remove(x);
            }

            return Ok(new
            {
                LoanId = loanId,
                LoanValor = loanAmount,
                LoanStatus = loan.Status,
                TotalInvestidores = allInvestors.Count,
                TotalElegiveis = eligible.Count,
                PoolFinal = pool.Count,
                AlocacaoPossivel = stable && pool.Any(),
                Investidores_Todos = allInvestors.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Status,
                    u.Role,
                    TemCarteira = u.Wallet != null,
                    SaldoDisponivel = u.Wallet?.SaldoDisponivel
                }),
                Investidores_Elegiveis = eligible.Select(u => new
                {
                    u.Id,
                    u.Email,
                    u.Status,
                    SaldoDisponivel = u.Wallet!.SaldoDisponivel,
                    ValorQueSeriaAlocado = allocations.ContainsKey(u.Id) ? allocations[u.Id] : 0
                })
            });
        }
        /// <summary>
        /// Gera um JWT de admin para testes (SEM verificação de senha).
        /// REMOVER APÓS VALIDAÇÃO.
        /// GET /api/diagnostic/admin-token
        /// </summary>
        [HttpGet("admin-token")]
        public async Task<IActionResult> GetAdminToken([FromServices] IConfiguration config)
        {
            var admin = await _context.Users
                .FirstOrDefaultAsync(u => u.Role == "Administrador" && u.Status == "Aprovado");

            if (admin == null)
                return NotFound(new { message = "Nenhum administrador aprovado encontrado no banco." });

            var jwtKey = config["Jwt:Key"] ?? "SuperSecretKeyForP2PLendingMVP1Secret!!!";
            var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new Claim(ClaimTypes.Email, admin.Email),
                new Claim(ClaimTypes.Role, admin.Role)
            };

            var token = new JwtSecurityToken(
                issuer: config["Jwt:Issuer"] ?? "P2PLendingAPI",
                audience: config["Jwt:Audience"] ?? "P2PLendingClients",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: creds
            );

            return Ok(new
            {
                AdminId = admin.Id,
                AdminEmail = admin.Email,
                Token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }

        /// <summary>
        /// Executa a alocação automática para um empréstimo já aprovado e persiste no banco.
        /// POST /api/diagnostic/force-allocate/{loanId}
        /// </summary>
        [HttpPost("force-allocate/{loanId}")]
        public async Task<IActionResult> ForceAllocate(Guid loanId, [FromServices] AllocationEngineService allocationService)
        {
            // Capturar saldos ANTES
            var beforeWallets = await _context.InvestorWallets.ToListAsync();
            var beforeSaldos = beforeWallets.Select(w => new
            {
                w.Id,
                SaldoDisponivelAntes = w.SaldoDisponivel,
                SaldoComprometidoAntes = w.SaldoComprometido
            }).ToList();

            bool result = await allocationService.AllocateInvestorsForLoanAsync(loanId);

            if (!result)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Alocação falhou. Verifique os logs do console para detalhes.",
                    investidoresElegiveis = await _context.Users
                        .Include(u => u.Wallet)
                        .Where(u => u.Role == "Investidor" && u.Status == "Aprovado")
                        .Select(u => new { u.Email, u.Status, SaldoDisponivel = u.Wallet != null ? u.Wallet.SaldoDisponivel : 0 })
                        .ToListAsync()
                });
            }

            await _context.SaveChangesAsync();

            // Capturar saldos DEPOIS
            var afterWallets = await _context.InvestorWallets.ToListAsync();
            var allInvestors = await _context.Users.Where(u => u.Role == "Investidor").ToListAsync();

            var delta = afterWallets.Select(w =>
            {
                var before = beforeSaldos.FirstOrDefault(b => b.Id == w.Id);
                var investor = allInvestors.FirstOrDefault(u => u.Id == w.Id);
                return new
                {
                    InvestorEmail = investor?.Email ?? w.Id.ToString(),
                    SaldoDisponivelAntes = before?.SaldoDisponivelAntes ?? 0,
                    SaldoDisponivelDepois = w.SaldoDisponivel,
                    SaldoComprometidoAntes = before?.SaldoComprometidoAntes ?? 0,
                    SaldoComprometidoDepois = w.SaldoComprometido,
                    ValorAlocado = (before?.SaldoDisponivelAntes ?? 0) - w.SaldoDisponivel
                };
            }).Where(d => d.ValorAlocado != 0).ToList();

            var investments = await _context.Investments
                .Where(i => i.LoanId == loanId)
                .Select(i => new { i.InvestorId, i.ValorInvestido, i.Status, i.CreatedAt })
                .ToListAsync();

            var transactions = await _context.InvestorTransactions
                .Where(t => t.Tipo == "ALOCACAO_AUTOMATICA")
                .OrderByDescending(t => t.DataMovimentacao)
                .Take(20)
                .Select(t => new { t.InvestorId, t.Valor, t.SaldoAnterior, t.SaldoPosterior, t.Descricao, t.DataMovimentacao })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                message = $"Alocação executada e persistida com sucesso para empréstimo {loanId}",
                InvestidoresAlterados = delta,
                RegistrosDeInvestimento = investments,
                TransacoesRegistradas = transactions
            });
        }
    }
}
