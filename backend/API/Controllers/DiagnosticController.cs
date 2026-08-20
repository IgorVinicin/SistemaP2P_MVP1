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
    [Authorize(Roles = "Administrador")]
    public class DiagnosticController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DiagnosticController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("jose")]
        public async Task<IActionResult> GetJose()
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Profile != null && (u.Profile.Nome.Contains("José") || u.Profile.Nome.Contains("Jose")));
            
            var rules = await _context.ScoreCreditRules.ToListAsync();
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();

            if (user == null)
            {
                return Ok(new { message = "José não encontrado", rules, config });
            }

            var loans = await _context.LoanRequests
                .Where(l => l.UserId == user.Id)
                .ToListAsync();

            var installments = await _context.LoanInstallments
                .Where(i => i.LoanRequest!.UserId == user.Id)
                .ToListAsync();

            return Ok(new
            {
                user = new { user.Id, user.Email, user.Status, ProfileNome = user.Profile?.Nome, ProfileScore = user.Profile?.ScoreAtual },
                loans = loans.Select(l => new { l.Id, l.ValorSolicitado, l.Status, l.DataCriacao }),
                installments = installments.Select(i => new { i.Id, i.LoanRequestId, i.NumeroParcela, i.ValorParcela, i.ValorPago, i.Status, i.ValorPrincipal }),
                rules,
                config
            });
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
        [HttpGet("debug-last-loan")]
        public async Task<IActionResult> DebugLastLoan()
        {
            var lastRevenue = await _context.PlatformRevenues
                .OrderByDescending(r => r.DataCriacao)
                .FirstOrDefaultAsync();

            if (lastRevenue == null) return Ok("No revenues found.");

            var installment = await _context.LoanInstallments
                .Include(i => i.LoanRequest)
                .FirstOrDefaultAsync(i => i.Id == lastRevenue.InstallmentId);

            var installmentIdStr = lastRevenue.InstallmentId?.ToString() ?? "";
            var investorTxs = await _context.InvestorTransactions
                .Where(t => t.Descricao.Contains(installmentIdStr) || t.DataMovimentacao >= lastRevenue.DataCriacao.AddMinutes(-1))
                .OrderByDescending(t => t.DataMovimentacao)
                .Take(5)
                .ToListAsync();

            var investments = await _context.Investments
                .Where(inv => inv.LoanId == lastRevenue.LoanId)
                .ToListAsync();

            return Ok(new
            {
                LastRevenue = lastRevenue,
                Installment = installment,
                Investments = investments,
                InvestorTransactions = investorTxs
            });
        }

        [HttpGet("ping")]
        public async Task<IActionResult> GetLoans()
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == "investidor1@p2p.com");
            var admin = await _context.Users.FirstOrDefaultAsync(u => u.Email == "admin@p2p.com");

            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<backend.Domain.Entities.User>();
            var verifyUser = user != null ? hasher.VerifyHashedPassword(user, user.PasswordHash, "Senha123!") : Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
            var verifyAdmin = admin != null ? hasher.VerifyHashedPassword(admin, admin.PasswordHash, "Senha123!") : Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;

            return Ok(new
            {
                userEmail = user?.Email,
                userHash = user?.PasswordHash,
                userVerify = verifyUser.ToString(),
                adminEmail = admin?.Email,
                adminHash = admin?.PasswordHash,
                adminVerify = verifyAdmin.ToString()
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

            var (result, _) = await allocationService.AllocateInvestorsForLoanAsync(loanId);

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

        /// <summary>
        /// Cria um cenário limpo de teste para validação obrigatória.
        /// POST /api/diagnostic/clean-scenario
        /// </summary>
        [HttpPost("clean-scenario")]
        public async Task<IActionResult> CreateCleanScenario([FromServices] AllocationEngineService allocationService)
        {
            using var dbTransaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Limpar usuários antigos com o mesmo email para evitar duplicados
                var emailsToClean = new[] { "clean_tomador@p2p.com", "clean_inv1@p2p.com", "clean_inv2@p2p.com" };
                var oldUsers = await _context.Users
                    .Include(u => u.Profile)
                    .Include(u => u.Wallet)
                    .Include(u => u.LoanRequests)
                    .Include(u => u.Investments)
                    .Where(u => emailsToClean.Contains(u.Email))
                    .ToListAsync();

                foreach (var u in oldUsers)
                {
                    // Remover parcelas e investimentos relacionados
                    var loanIds = u.LoanRequests.Select(l => l.Id).ToList();
                    var insts = await _context.LoanInstallments.Where(i => loanIds.Contains(i.LoanRequestId)).ToListAsync();
                    _context.LoanInstallments.RemoveRange(insts);
                    
                    var hist = await _context.FinancialEventHistories.Where(h => loanIds.Contains(h.LoanRequestId ?? Guid.Empty)).ToListAsync();
                    _context.FinancialEventHistories.RemoveRange(hist);

                    _context.Investments.RemoveRange(u.Investments);
                    
                    var otherInvestments = await _context.Investments.Where(i => loanIds.Contains(i.LoanId)).ToListAsync();
                    _context.Investments.RemoveRange(otherInvestments);

                    _context.LoanRequests.RemoveRange(u.LoanRequests);

                    if (u.Profile != null) _context.UserProfiles.Remove(u.Profile);
                    if (u.Wallet != null) _context.InvestorWallets.Remove(u.Wallet);
                    _context.Users.Remove(u);
                }
                await _context.SaveChangesAsync();

                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<backend.Domain.Entities.User>();

                // 1. Novo Tomador
                var tomador = new backend.Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    Email = "clean_tomador@p2p.com",
                    Role = "Cliente",
                    Status = "Aprovado"
                };
                tomador.PasswordHash = hasher.HashPassword(tomador, "Senha123!");
                tomador.Profile = new backend.Domain.Entities.UserProfile
                {
                    Id = tomador.Id,
                    Nome = "Tomador Limpo",
                    CPF = "77777777777",
                    Telefone = "11977777777",
                    DataNascimento = DateTime.UtcNow.AddYears(-25),
                    ScoreAtual = 800,
                    QuestionnaireCompleted = true
                };
                _context.Users.Add(tomador);

                // 2. Novos Investidores
                var inv1 = new backend.Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    Email = "clean_inv1@p2p.com",
                    Role = "Investidor",
                    Status = "Aprovado"
                };
                inv1.PasswordHash = hasher.HashPassword(inv1, "Senha123!");
                inv1.Profile = new backend.Domain.Entities.UserProfile
                {
                    Id = inv1.Id,
                    Nome = "Investidor Limpo Um",
                    CPF = "88888888888",
                    Telefone = "11988888888",
                    DataNascimento = DateTime.UtcNow.AddYears(-40),
                    ScoreAtual = 900,
                    QuestionnaireCompleted = true
                };
                inv1.Wallet = new backend.Domain.Entities.InvestorWallet
                {
                    Id = inv1.Id,
                    SaldoDisponivel = 10000.00m,
                    SaldoComprometido = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    TotalAportado = 10000.00m,
                    PerfilInvestimento = "Moderado",
                    Rentabilidade = 5.00m,
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.Users.Add(inv1);

                var inv2 = new backend.Domain.Entities.User
                {
                    Id = Guid.NewGuid(),
                    Email = "clean_inv2@p2p.com",
                    Role = "Investidor",
                    Status = "Aprovado"
                };
                inv2.PasswordHash = hasher.HashPassword(inv2, "Senha123!");
                inv2.Profile = new backend.Domain.Entities.UserProfile
                {
                    Id = inv2.Id,
                    Nome = "Investidor Limpo Dois",
                    CPF = "99999999999",
                    Telefone = "11999999999",
                    DataNascimento = DateTime.UtcNow.AddYears(-42),
                    ScoreAtual = 950,
                    QuestionnaireCompleted = true
                };
                inv2.Wallet = new backend.Domain.Entities.InvestorWallet
                {
                    Id = inv2.Id,
                    SaldoDisponivel = 10000.00m,
                    SaldoComprometido = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    TotalAportado = 10000.00m,
                    PerfilInvestimento = "Moderado",
                    Rentabilidade = 6.00m,
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow
                };
                _context.Users.Add(inv2);

                await _context.SaveChangesAsync();

                // 3. Novo Empréstimo (Pendente)
                var loan = new backend.Domain.Entities.LoanRequest
                {
                    Id = Guid.NewGuid(),
                    UserId = tomador.Id,
                    ValorSolicitado = 5000.00m,
                    Parcelas = 5,
                    QuantidadeParcelas = 5,
                    Status = "Pendente",
                    ScoreNaSolicitacao = 800,
                    ScoreNoMomento = 800,
                    Finalidade = "Cenário de Teste Limpo",
                    Observacoes = "Criado para validar a quitação integral do empréstimo.",
                    TaxaJurosAplicada = 2.00m,
                    TaxaJuros = 2.00m,
                    ValorTotal = 0, // Será calculado na aprovação
                    NivelRisco = "Baixo",
                    CreatedAt = DateTime.UtcNow,
                    DataCriacao = DateTime.UtcNow,
                    DataSolicitacao = DateTime.UtcNow
                };
                _context.LoanRequests.Add(loan);
                await _context.SaveChangesAsync();

                await dbTransaction.CommitAsync();

                return Ok(new
                {
                    message = "Cenário limpo criado com sucesso. Agora você pode aprovar este empréstimo.",
                    loanId = loan.Id,
                    tomadorId = tomador.Id,
                    inv1Id = inv1.Id,
                    inv2Id = inv2.Id
                });
            }
            catch (Exception ex)
            {
                await dbTransaction.RollbackAsync();
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("run-audit-tests")]
        public async Task<IActionResult> RunAuditTests()
        {
            var logs = new List<string>();
            void Log(string message)
            {
                logs.Add(message);
            }

            Log("==================================================");
            Log("INICIANDO BATERIA COMPLETA DE AUDITORIA E TESTES");
            Log("==================================================");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Ensure Config & ProtectionFund
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                if (config == null)
                {
                    config = new backend.Domain.Entities.CreditConfig
                    {
                        Id = Guid.NewGuid(),
                        ScoreMinimoParaSolicitacao = 150,
                        ValorMinimoInvestimento = 5000.00m,
                        MinimoParcelasPagasNovaSolicitacao = 4,
                        MaxInvestidoresPorEmprestimo = 10,
                        PercentualMultaAtraso = 2.00m,
                        PercentualJurosDiario = 0.033m,
                        SpreadMinimoPlataforma = 1.00m
                    };
                    _context.CreditConfigs.Add(config);
                    await _context.SaveChangesAsync();
                }

                var fund = await _context.ProtectionFunds.FirstOrDefaultAsync();
                if (fund == null)
                {
                    fund = new backend.Domain.Entities.ProtectionFund
                    {
                        Id = Guid.NewGuid(),
                        SaldoAtual = 100000.00m,
                        TotalRecebido = 0,
                        TotalUtilizado = 0,
                        UltimaAtualizacao = DateTime.UtcNow
                    };
                    _context.ProtectionFunds.Add(fund);
                    await _context.SaveChangesAsync();
                }

                Log($"[DB-STATE] Configuração de Crédito Global: Score Mínimo={config.ScoreMinimoParaSolicitacao}, Limite Investidores={config.MaxInvestidoresPorEmprestimo}");
                Log($"[DB-STATE] Fundo de Proteção Inicial: Saldo={fund.SaldoAtual:C2}");

                // --------------------------------------------------
                // TESTE 1: ALOCAÇÃO AUTOMÁTICA
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 1: ALOCAÇÃO AUTOMÁTICA");
                Log("==================================================");

                var clientes = await _context.Users
                    .Where(u => u.Role == "Cliente" && u.Status == "Aprovado")
                    .Take(5)
                    .ToListAsync();

                if (clientes.Count < 5)
                {
                    Log("[TEST 1] FALHA: Clientes insuficientes para o teste (requer pelo menos 5).");
                    return Ok(logs);
                }

                var loanAmounts = new[] { 5000.00m, 10000.00m, 15000.00m, 20000.00m, 25000.00m };
                var approvedLoans = new List<backend.Domain.Entities.LoanRequest>();
                var priceCalc = new PriceCalculationService();
                var allocationEngine = new AllocationEngineService(_context);

                for (int i = 0; i < clientes.Count; i++)
                {
                    var cliente = clientes[i];
                    var loan = new backend.Domain.Entities.LoanRequest
                    {
                        Id = Guid.NewGuid(),
                        UserId = cliente.Id,
                        ValorSolicitado = loanAmounts[i],
                        QuantidadeParcelas = 12,
                        Parcelas = 12,
                        TaxaJuros = 2.50m,
                        TaxaJurosAplicada = 2.50m,
                        Status = "Pendente",
                        ScoreNoMomento = 500,
                        ScoreNaSolicitacao = 500,
                        NivelRisco = "Médio",
                        TipoEmprestimo = "Plus",
                        Periodicidade = "Mensal",
                        CreatedAt = DateTime.UtcNow,
                        DataCriacao = DateTime.UtcNow,
                        DataSolicitacao = DateTime.UtcNow
                    };

                    _context.LoanRequests.Add(loan);
                    await _context.SaveChangesAsync();

                    var installments = priceCalc.CalculateInstallments(loan.Id, loan.ValorSolicitado, loan.QuantidadeParcelas, loan.TaxaJuros, loan.Periodicidade);
                    loan.ValorTotal = installments.Sum(inst => inst.ValorParcela);
                    loan.Status = "Aprovado";
                    loan.DataAnalise = DateTime.UtcNow;
                    loan.DataAprovacao = DateTime.UtcNow;

                    _context.LoanInstallments.AddRange(installments);
                    await _context.SaveChangesAsync();

                    Log($"[TEST 1] Empréstimo criado para {cliente.Email} | Valor: {loan.ValorSolicitado:C2} | Valor Total com Juros: {loan.ValorTotal:C2}");

                    var (allocated, _) = await allocationEngine.AllocateInvestorsForLoanAsync(loan.Id);
                    if (!allocated)
                    {
                        Log($"[TEST 1] FALHA ao alocar investidores para o empréstimo {loan.Id}");
                        continue;
                    }

                    await _context.SaveChangesAsync();
                    approvedLoans.Add(loan);
                }

                foreach (var loan in approvedLoans)
                {
                    var investments = await _context.Investments.Where(inv => inv.LoanId == loan.Id).ToListAsync();
                    decimal totalInvestido = investments.Sum(inv => inv.ValorInvestido);
                    Log($"Empréstimo {loan.Id} (Solicitado: {loan.ValorSolicitado:C2}) | Total Investido Alocado: {totalInvestido:C2} | Qtd Investidores: {investments.Count}");

                    int transactionsCreated = await _context.InvestorTransactions.CountAsync(t => t.Tipo == "ALOCACAO_AUTOMATICA" && t.Descricao.Contains(loan.Id.ToString()));
                    int historiesCreated = await _context.FinancialEventHistories.CountAsync(h => h.EventoExecutado == "ALOCACAO_COMPROMETIDO" && h.LoanRequestId == loan.Id);
                    Log($"  Registros associados: Transações criadas={transactionsCreated}, Históricos criados={historiesCreated}");
                }

                // Medir Concentração
                var allPortfolioInvestments = await _context.Investments
                    .Where(inv => approvedLoans.Select(l => l.Id).Contains(inv.LoanId))
                    .ToListAsync();

                var groupedByInvestor = allPortfolioInvestments
                    .GroupBy(inv => inv.InvestorId)
                    .Select(g => new
                    {
                        InvestorId = g.Key,
                        TotalAlocado = g.Sum(inv => inv.ValorInvestido),
                        QtdParticipacoes = g.Count()
                    })
                    .OrderByDescending(x => x.TotalAlocado)
                    .ToList();

                decimal totalGeralAlocado = allPortfolioInvestments.Sum(inv => inv.ValorInvestido);
                Log($"Total Alocado na Carteira de Testes: {totalGeralAlocado:C2} distribuído entre {groupedByInvestor.Count} investidores.");
                Log("Top 5 Investidores mais concentrados:");
                for (int j = 0; j < Math.Min(5, groupedByInvestor.Count); j++)
                {
                    var item = groupedByInvestor[j];
                    var invUser = await _context.Users.FindAsync(item.InvestorId);
                    decimal pct = (item.TotalAlocado / totalGeralAlocado) * 100m;
                    Log($"  {j + 1}. {invUser?.Email} | Alocado: {item.TotalAlocado:C2} ({pct:F2}%) | Participações: {item.QtdParticipacoes}");
                }

                var values = groupedByInvestor.Select(x => (double)x.TotalAlocado).ToList();
                double avg = values.Average();
                double variance = values.Sum(v => Math.Pow(v - avg, 2)) / values.Count;
                double stdDev = Math.Sqrt(variance);
                Log($"Estatísticas do Pool: Média de Alocação={avg:C2} | Desvio Padrão={stdDev:C2} | Mín={values.Min():C2} | Máx={values.Max():C2}");

                // --------------------------------------------------
                // TESTE 2: PAGAMENTO DE PARCELAS
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 2: PAGAMENTO DE PARCELAS");
                Log("==================================================");

                if (approvedLoans.Any())
                {
                    var loan = approvedLoans.First();
                    var installment = await _context.LoanInstallments
                        .Where(i => i.LoanRequestId == loan.Id && i.NumeroParcela == 1)
                        .FirstOrDefaultAsync();

                    if (installment != null)
                    {
                        Log($"Selecionado Empréstimo {loan.Id} | Parcela {installment.NumeroParcela} | Valor: {installment.ValorParcela:C2}");
                        var walletDistService = new WalletDistributionService(_context);

                        var investments = await _context.Investments.Where(inv => inv.LoanId == loan.Id).ToListAsync();
                        var preWalletState = new Dictionary<Guid, (decimal Disp, decimal Comp, decimal Inv)>();
                        foreach (var inv in investments)
                        {
                            var w = await _context.InvestorWallets.FindAsync(inv.InvestorId);
                            if (w != null) preWalletState[inv.InvestorId] = (w.SaldoDisponivel, w.SaldoComprometido, w.SaldoInvestido);
                        }

                        var fundBefore = fund.SaldoAtual;

                        installment.Status = "Paga";
                        installment.DataPagamento = DateTime.UtcNow;
                        installment.ValorPago = installment.ValorParcela;
                        installment.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        await walletDistService.DistributeInstallmentPaymentAsync(installment.Id, "Sistema_Teste");

                        var fundAfter = fund.SaldoAtual;
                        Log($"Fundo de Proteção: {fundBefore:C2} → {fundAfter:C2} (Diferença: {fundAfter - fundBefore:C2})");

                        foreach (var inv in investments)
                        {
                            var w = await _context.InvestorWallets.FindAsync(inv.InvestorId);
                            var user = await _context.Users.FindAsync(inv.InvestorId);
                            if (w == null || user == null) continue;

                            var pre = preWalletState[inv.InvestorId];
                            decimal fraction = inv.ValorInvestido / loan.ValorSolicitado;
                            decimal principalEsperado = Math.Round(installment.ValorPrincipal * fraction, 2);
                            decimal jurosEsperado = loan.TaxaJuros > 0 ? Math.Round(installment.ValorJuros * fraction * (inv.Rentabilidade / loan.TaxaJuros), 2) : Math.Round(installment.ValorJuros * fraction, 2);
                            decimal totalEsperado = principalEsperado + jurosEsperado;

                            decimal diffDisp = w.SaldoDisponivel - pre.Disp;
                            decimal diffComp = w.SaldoComprometido - pre.Comp;
                            decimal diffInv = w.SaldoInvestido - pre.Inv;

                            Log($"Investidor {user.Email}:");
                            Log($"  Saldo Disponível:  {pre.Disp:C2} → {w.SaldoDisponivel:C2} (Var: {diffDisp:C2} | Esperado: +{totalEsperado:C2})");
                            Log($"  Saldo Comprometido:{pre.Comp:C2} → {w.SaldoComprometido:C2} (Var: {diffComp:C2} | Esperado: -{inv.ValorInvestido:C2})");
                            Log($"  Saldo Investido:   {pre.Inv:C2} → {w.SaldoInvestido:C2} (Var: {diffInv:C2} | Esperado: +{inv.ValorInvestido - principalEsperado:C2})");
                        }
                    }
                }

                // --------------------------------------------------
                // TESTE 3: QUITAÇÃO PELO ADMINISTRADOR
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 3: QUITAÇÃO PELO ADMINISTRADOR");
                Log("==================================================");

                if (approvedLoans.Count > 1)
                {
                    var loan = approvedLoans[1];
                    Log($"Selecionado Empréstimo {loan.Id} para Quitação Integral (Valor: {loan.ValorSolicitado:C2})");

                    var installments = await _context.LoanInstallments
                        .Where(i => i.LoanRequestId == loan.Id && i.Status != "Paga" && i.Status != "Cancelada")
                        .ToListAsync();

                    var walletDistService = new WalletDistributionService(_context);
                    foreach (var inst in installments)
                    {
                        inst.Status = "Paga";
                        inst.DataPagamento = DateTime.UtcNow;
                        inst.ValorPago = inst.ValorParcela;
                        inst.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();

                        await walletDistService.DistributeInstallmentPaymentAsync(inst.Id, "Admin_Teste");
                    }

                    loan.Status = "Quitado";
                    loan.DataEncerramento = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    Log($"Empréstimo {loan.Id} quitado integralmente. Parcelas quitadas: {installments.Count}");
                }

                // --------------------------------------------------
                // TESTE 4: INADIMPLÊNCIA
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 4: INADIMPLÊNCIA");
                Log("==================================================");

                if (approvedLoans.Count > 3)
                {
                    var delinquencyService = new DelinquencyService(_context);

                    // Cenário A: Cobertura Total
                    var loanA = approvedLoans[2];
                    Log($"[CENÁRIO A] Simulando Cobertura Total no Empréstimo {loanA.Id}");

                    var installmentsA = await _context.LoanInstallments.Where(i => i.LoanRequestId == loanA.Id).ToListAsync();
                    foreach (var inst in installmentsA.Take(3))
                    {
                        inst.DataVencimento = DateTime.UtcNow.AddDays(-70);
                        inst.Status = "Atrasada";
                    }
                    await _context.SaveChangesAsync();

                    await delinquencyService.UpdateOverdueInstallmentsAsync();

                    var overdueInstallments = await _context.LoanInstallments
                        .Where(i => i.LoanRequestId == loanA.Id && i.Status == "INADIMPLENTE")
                        .ToListAsync();

                    decimal totalOverdue = overdueInstallments.Sum(i => i.ValorParcela + i.Multa + i.JurosMora);
                    Log($"Overdue Installments Count: {overdueInstallments.Count} | Total Devido: {totalOverdue:C2}");

                    var investmentsA = await _context.Investments.Where(i => i.LoanId == loanA.Id).ToListAsync();
                    decimal totalInvestidoA = investmentsA.Sum(i => i.ValorInvestido);
                    decimal totalPrincipalUnpaid = overdueInstallments.Sum(i => i.ValorPrincipal);
                    decimal totalJurosUnpaid = overdueInstallments.Sum(i => i.ValorJuros);

                    decimal totalPayout = 0;
                    foreach (var inv in investmentsA)
                    {
                        var wallet = await _context.InvestorWallets.FindAsync(inv.InvestorId);
                        if (wallet == null) continue;

                        if (inv.Status == "Comprometido")
                        {
                            wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                            wallet.SaldoInvestido += inv.ValorInvestido;
                            inv.Status = "Ativo";
                        }

                        decimal percentual = inv.ValorInvestido / totalInvestidoA;
                        decimal principalRec = Math.Round(totalPrincipalUnpaid * percentual, 2);
                        decimal rentabilidadeFator = loanA.TaxaJuros > 0 ? Math.Min(1.0m, inv.Rentabilidade / loanA.TaxaJuros) : 1.0m;
                        decimal jurosRec = Math.Round(totalJurosUnpaid * percentual * rentabilidadeFator, 2);
                        decimal rec = principalRec + jurosRec;

                        wallet.SaldoDisponivel += rec;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - principalRec);
                        inv.SaldoPrincipalPendente = Math.Max(0, inv.SaldoPrincipalPendente - principalRec);
                        wallet.Rendimentos += jurosRec;
                        totalPayout += rec;
                    }

                    fund.SaldoAtual -= totalPayout;
                    fund.TotalUtilizado += totalPayout;

                    foreach (var inst in overdueInstallments)
                    {
                        inst.Status = "Paga";
                        inst.DataPagamento = DateTime.UtcNow;
                        inst.ValorPago = inst.ValorParcela + inst.Multa + inst.JurosMora;
                    }
                    await _context.SaveChangesAsync();
                    await delinquencyService.UpdateLoanStatusFromInstallmentsAsync(loanA.Id, "Admin_Teste");

                    Log($"[CENÁRIO A] Cobertura Concluída. Fundo de Proteção Saldo: {fund.SaldoAtual:C2}");

                    // Cenário B: Encerramento com Perda
                    var loanB = approvedLoans[3];
                    Log($"\n[CENÁRIO B] Simulando Encerramento com Perda no Empréstimo {loanB.Id}");

                    var installmentsB = await _context.LoanInstallments.Where(i => i.LoanRequestId == loanB.Id).ToListAsync();
                    foreach (var inst in installmentsB.Take(3))
                    {
                        inst.DataVencimento = DateTime.UtcNow.AddDays(-70);
                        inst.Status = "Atrasada";
                    }
                    await _context.SaveChangesAsync();

                    await delinquencyService.UpdateOverdueInstallmentsAsync();

                    var unpaidInstallmentsB = await _context.LoanInstallments
                        .Where(i => i.LoanRequestId == loanB.Id && i.Status != "Paga" && i.Status != "Cancelada")
                        .ToListAsync();

                    decimal totalPrincipalUnpaidB = unpaidInstallmentsB.Sum(i => i.ValorPrincipal);
                    var investmentsB = await _context.Investments.Where(i => i.LoanId == loanB.Id).ToListAsync();
                    decimal totalInvestidoB = investmentsB.Sum(i => i.ValorInvestido);

                    decimal totalRefunded = 0;
                    foreach (var inv in investmentsB)
                    {
                        var wallet = await _context.InvestorWallets.FindAsync(inv.InvestorId);
                        if (wallet == null) continue;

                        if (inv.Status == "Comprometido")
                        {
                            wallet.SaldoComprometido = Math.Max(0, wallet.SaldoComprometido - inv.ValorInvestido);
                            wallet.SaldoInvestido += inv.ValorInvestido;
                            inv.Status = "Ativo";
                        }

                        decimal percentual = inv.ValorInvestido / totalInvestidoB;
                        decimal refund = Math.Round(totalPrincipalUnpaidB * percentual, 2);

                        wallet.SaldoDisponivel += refund;
                        wallet.SaldoInvestido = Math.Max(0, wallet.SaldoInvestido - refund);
                        inv.SaldoPrincipalPendente = Math.Max(0, inv.SaldoPrincipalPendente - refund);
                        totalRefunded += refund;
                    }

                    fund.SaldoAtual -= totalRefunded;
                    fund.TotalUtilizado += totalRefunded;

                    foreach (var inst in unpaidInstallmentsB)
                    {
                        inst.Status = "Cancelada";
                    }

                    var loanBUpdated = await _context.LoanRequests.FindAsync(loanB.Id);
                    if (loanBUpdated != null)
                    {
                        loanBUpdated.Status = "FinalizadoPorInadimplencia";
                        loanBUpdated.DataEncerramento = DateTime.UtcNow;
                    }

                    await _context.SaveChangesAsync();

                    Log($"[CENÁRIO B] Encerramento com Perda Concluído. Fundo de Proteção Reembolsou: {totalRefunded:C2}. Novo Saldo Fund: {fund.SaldoAtual:C2}");
                }

                // --------------------------------------------------
                // TESTE 5: CONSISTÊNCIA FINANCEIRA
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 5: CONSISTÊNCIA FINANCEIRA");
                Log("==================================================");

                var wallets = await _context.InvestorWallets.Include(w => w.User).ToListAsync();
                int inconsistent = 0;
                decimal diffSum = 0;

                foreach (var w in wallets)
                {
                    decimal totalPatrimonio = w.SaldoDisponivel + w.SaldoComprometido + w.SaldoInvestido;
                    decimal totalOrigem = w.TotalAportado - w.TotalSacado + w.Rendimentos;

                    decimal diff = totalPatrimonio - totalOrigem;
                    if (Math.Abs(diff) > 0.01m)
                    {
                        inconsistent++;
                        diffSum += Math.Abs(diff);
                        Log($"Carteira Inconsistente {w.User?.Email}: Patrimônio={totalPatrimonio:C2} | Origem={totalOrigem:C2} | Diff={diff:C2}");
                    }
                }

                Log($"Total Inconsistentes: {inconsistent} / {wallets.Count} | Divergência Total: {diffSum:C2}");

                // Lastro
                decimal walletsInvestido = wallets.Sum(w => w.SaldoInvestido);
                decimal activeInvestments = await _context.Investments.Where(inv => inv.Status == "Ativo").SumAsync(inv => inv.SaldoPrincipalPendente);
                Log($"SaldoInvestido Wallets: {walletsInvestido:C2} | Investments Ativos (SaldoPrincipalPendente): {activeInvestments:C2} | Diff: {walletsInvestido - activeInvestments:C2}");

                decimal walletsComprometido = wallets.Sum(w => w.SaldoComprometido);
                decimal compInvestments = await _context.Investments.Where(inv => inv.Status == "Comprometido").SumAsync(inv => inv.ValorInvestido);
                Log($"SaldoComprometido Wallets: {walletsComprometido:C2} | Investments Comprometidos: {compInvestments:C2} | Diff: {walletsComprometido - compInvestments:C2}");

                // --------------------------------------------------
                // TESTE 6: PERFORMANCE
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 6: PERFORMANCE");
                Log("==================================================");

                var sw = new System.Diagnostics.Stopwatch();

                // Dashboard admin simulation
                sw.Start();
                for (int idx = 0; idx < 50; idx++)
                {
                    var count = await _context.LoanRequests.CountAsync(l => l.Status == "EmPagamento" || l.Status == "Inadimplente");
                    var vol = await _context.LoanRequests.Where(l => l.Status == "EmPagamento").SumAsync(l => l.ValorSolicitado);
                }
                sw.Stop();
                Log($"Métricas Dashboard Admin (50 runs): {sw.Elapsed.TotalMilliseconds / 50.0:F2} ms");

                // Users list
                sw.Restart();
                for (int idx = 0; idx < 30; idx++)
                {
                    var list = await _context.Users.Include(u => u.Profile).Include(u => u.Wallet).AsNoTracking().ToListAsync();
                }
                sw.Stop();
                Log($"Listagem de Usuários (30 runs): {sw.Elapsed.TotalMilliseconds / 30.0:F2} ms");

                // Loans list
                sw.Restart();
                for (int idx = 0; idx < 50; idx++)
                {
                    var list = await _context.LoanRequests.Include(l => l.User).ThenInclude(u => u!.Profile).OrderByDescending(l => l.DataCriacao).AsNoTracking().ToListAsync();
                }
                sw.Stop();
                Log($"Listagem de Empréstimos (50 runs): {sw.Elapsed.TotalMilliseconds / 50.0:F2} ms");

                // --------------------------------------------------
                // TESTE 7: BUSCAS E FILTROS
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 7: BUSCAS E FILTROS");
                Log("==================================================");

                sw.Restart();
                for (int idx = 0; idx < 50; idx++)
                {
                    var res = await _context.Users.Include(u => u.Profile).Where(u => u.Profile != null && u.Profile.Nome.ToLower().Contains("invest")).ToListAsync();
                }
                sw.Stop();
                Log($"Busca por NOME 'invest' (50 runs): {sw.Elapsed.TotalMilliseconds / 50.0:F2} ms");

                sw.Restart();
                for (int idx = 0; idx < 50; idx++)
                {
                    var res = await _context.Users.Include(u => u.Profile).Where(u => u.Profile != null && u.Profile.CPF.Contains("123")).ToListAsync();
                }
                sw.Stop();
                Log($"Busca por CPF '123' (50 runs): {sw.Elapsed.TotalMilliseconds / 50.0:F2} ms");

                // --------------------------------------------------
                // TESTE 8: VALIDAÇÃO DE ESCALABILIDADE (ÍNDICES)
                // --------------------------------------------------
                Log("\n==================================================");
                Log("TESTE 8: VALIDAÇÃO DE ESCALABILIDADE (ÍNDICES)");
                Log("==================================================");

                var conn = _context.Database.GetDbConnection();
                bool wasClosed = conn.State == System.Data.ConnectionState.Closed;
                if (wasClosed)
                {
                    await conn.OpenAsync();
                }
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT
                        tablename,
                        indexname,
                        indexdef
                    FROM
                        pg_indexes
                    WHERE
                        schemaname = 'public';";

                var physicalIndexes = new List<(string Table, string Index, string Def)>();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        physicalIndexes.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
                    }
                }
                if (wasClosed)
                {
                    await conn.CloseAsync();
                }

                Log($"Total de índices físicos no PostgreSQL: {physicalIndexes.Count}");
                var tables = new[] { "LoanInstallments", "Investments", "InvestorTransactions", "LoanRequests", "FinancialEventHistories" };
                foreach (var t in tables)
                {
                    var idxs = physicalIndexes.Where(x => x.Table.Equals(t, StringComparison.OrdinalIgnoreCase)).ToList();
                    Log($"Tabela: {t}");
                    if (idxs.Any())
                    {
                        foreach (var index in idxs)
                        {
                            Log($"  - {index.Index} | {index.Def}");
                        }
                    }
                    else
                    {
                        Log("  [AVISO] Nenhum índice físico localizado!");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"ERRO DURANTE OS TESTES: {ex}");
            }
            finally
            {
                await transaction.RollbackAsync();
                Log("\n==================================================");
                Log("ROLLBACK DA TRANSAÇÃO EXECUTADO. BANCO PRESERVADO.");
                Log("==================================================");
            }

            return Ok(logs);
        }
    }
}
