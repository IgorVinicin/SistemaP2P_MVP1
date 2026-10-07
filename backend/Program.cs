using backend.API.Middleware;
using backend.Application.Services;
using backend.Application.Services.Asaas;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

// Tratamento Global de Exceções (RFC 7807)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Configuração de Rate Limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(policyName: "AuthRateLimit", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
    options.AddSlidingWindowLimiter(policyName: "GeneralRateLimit", limiterOptions =>
    {
        limiterOptions.PermitLimit = 120;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 4;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 0;
    });
});

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));
});

builder.Services.AddHttpClient();

builder.Services.AddSingleton<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<ICpfConsultationService, CpfConsultationService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CreditValidationService>();
builder.Services.AddScoped<DelinquencyService>();
builder.Services.AddScoped<AllocationEngineService>();
builder.Services.AddScoped<PlatformRevenueService>();
builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<CashFlowService>();
builder.Services.AddScoped<WalletDistributionService>();
builder.Services.AddScoped<InvestorYieldService>();
builder.Services.AddScoped<ITreasuryService, TreasuryService>();
builder.Services.AddScoped<IAsaasService, AsaasService>();

// Configuração JWT (prioriza Variável de Ambiente JWT_KEY para produção segura)
var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForP2PLendingMVP1Secret!!!";
if (!builder.Environment.IsDevelopment() &&
    (Encoding.UTF8.GetByteCount(jwtKey) < 32 || jwtKey == "SuperSecretKeyForP2PLendingMVP1Secret!!!"))
    throw new InvalidOperationException("Configure JWT_KEY com uma chave exclusiva de pelo menos 32 bytes antes de iniciar fora de Development.");
builder.Configuration["Jwt:Key"] = jwtKey;
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "P2PLendingAPI",
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"] ?? "P2PLendingClients",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Adicionar suporte para Controllers com serialização JSON em camelCase
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// Adicionar suporte CORS para permitir o frontend Angular acessar os recursos
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? (builder.Environment.IsDevelopment() ? new[] { "http://localhost:4200" } : Array.Empty<string>());
        if (origins.Length > 0) policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseExceptionHandler();

if (!app.Environment.IsDevelopment()) app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<IdempotencyMiddleware>();

app.MapControllers();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
        await backend.Infrastructure.AdminBootstrap.InitializeAsync(context, builder.Configuration);

        // Dados de demonstração só são criados mediante opt-in explícito em Development.
        if (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Seed:DemoData"))
        {

        // Correção de dados legados (Investidores sem carteira ou com rentabilidade zerada, e investimentos com status nulo/vazio)
        try
        {
            var investidores = context.Users.Where(u => u.Role == "Investidor").ToList();
            foreach (var inv in investidores)
            {
                var wallet = context.InvestorWallets.FirstOrDefault(w => w.Id == inv.Id);
                if (wallet == null)
                {
                    Console.WriteLine($"[MIGRACAO] Criando InvestorWallet para investidor legado: {inv.Email}");
                    context.InvestorWallets.Add(new InvestorWallet
                    {
                        Id = inv.Id,
                        SaldoDisponivel = 0,
                        SaldoComprometido = 0,
                        SaldoInvestido = 0,
                        Rendimentos = 0,
                        TotalAportado = 0,
                        TotalSacado = 0,
                        PerfilInvestimento = "Conservador",
                        Rentabilidade = 5.00m,
                        DataCriacao = DateTime.UtcNow,
                        DataAtualizacao = DateTime.UtcNow,
                        ObservacaoAdministrativa = "Criado automaticamente pela rotina de migração."
                    });
                }
                else if (wallet.Rentabilidade == 0.00m)
                {
                    Console.WriteLine($"[MIGRACAO] Corrigindo rentabilidade de 0.00% para 5.00% do investidor: {inv.Email}");
                    wallet.Rentabilidade = 5.00m;
                    context.InvestorWallets.Update(wallet);
                }
            }

            var investmentsSemStatus = context.Investments.Where(i => string.IsNullOrEmpty(i.Status)).ToList();
            foreach (var investment in investmentsSemStatus)
            {
                var loan = context.LoanRequests.FirstOrDefault(l => l.Id == investment.LoanId);
                string novoStatus = "Ativo";
                if (loan != null && (loan.Status == "Pendente" || loan.Status == "Aprovado"))
                {
                    novoStatus = "Comprometido";
                }
                Console.WriteLine($"[MIGRACAO] Corrigindo status do Investment {investment.Id} para '{novoStatus}' (LoanStatus: {loan?.Status})");
                investment.Status = novoStatus;
                context.Investments.Update(investment);
            }
            context.SaveChanges();
        }
        catch (Exception migEx)
        {
            Console.WriteLine($"[MIGRACAO-ERRO] Erro ao executar migração de dados legados: {migEx.Message}");
        }

        // Seed das Regras de Score
        if (!context.ScoreCreditRules.Any())
        {
            context.ScoreCreditRules.AddRange(
                new ScoreCreditRule
                {
                    Id = Guid.NewGuid(),
                    ScoreMin = 0,
                    ScoreMax = 100,
                    LimiteEmprestimo = 500.00m,
                    TaxaJuros = 8.00m,
                    NivelRisco = "Alto",
                    Ativo = true
                },
                new ScoreCreditRule
                {
                    Id = Guid.NewGuid(),
                    ScoreMin = 101,
                    ScoreMax = 300,
                    LimiteEmprestimo = 2000.00m,
                    TaxaJuros = 5.00m,
                    NivelRisco = "Médio",
                    Ativo = true
                },
                new ScoreCreditRule
                {
                    Id = Guid.NewGuid(),
                    ScoreMin = 301,
                    ScoreMax = 1000,
                    LimiteEmprestimo = 10000.00m,
                    TaxaJuros = 2.00m,
                    NivelRisco = "Baixo",
                    Ativo = true
                }
            );
        }

        // Seed da Configuração de Crédito Global
        if (!context.CreditConfigs.Any())
        {
            context.CreditConfigs.Add(new CreditConfig
            {
                Id = Guid.NewGuid(),
                ScoreMinimoParaSolicitacao = 150,
                ValorMinimoInvestimento = 5000.00m,
                MinimoParcelasPagasNovaSolicitacao = 4,
                TaxaRendimentoPool = 5.00m
            });
        }
        else
        {
            // Corrige taxa de rendimento se estiver zerada
            var cfg = context.CreditConfigs.First();
            if (cfg.TaxaRendimentoPool <= 0)
            {
                cfg.TaxaRendimentoPool = 5.00m;
            }
        }

        // 1. Seed Administrador
        {
            var hasher = new PasswordHasher<User>();
            var admin = context.Users.Include(u => u.Profile).FirstOrDefault(u => u.Email == "admin@p2p.com");
            if (admin == null)
            {
                admin = new User
                {
                    Id = Guid.NewGuid(),
                    Email = "admin@p2p.com",
                    Role = "Administrador",
                    Status = "Aprovado"
                };
                admin.PasswordHash = hasher.HashPassword(admin, "Senha123!");

                var profile = new UserProfile
                {
                    Id = admin.Id,
                    Nome = "Administrador do Sistema",
                    CPF = "00000000000",
                    Telefone = "00000000000",
                    DataNascimento = DateTime.UtcNow.AddYears(-30),
                    ScoreAtual = 1000
                };
                admin.Profile = profile;
                context.Users.Add(admin);
            }
            else
            {
                // Reiniciar a API n?o redefine credenciais de usu?rios existentes.
            }
        }

        // Seed do novo administrador solicitado
        if (!context.Users.Any(u => u.Email == "iaraujopecedossantos@gmail.com"))
        {
            var hasher = new PasswordHasher<User>();
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "iaraujopecedossantos@gmail.com",
                Role = "Administrador",
                Status = "Aprovado"
            };
            admin.PasswordHash = hasher.HashPassword(admin, "1234");

            var profile = new UserProfile
            {
                Id = admin.Id,
                Nome = "Administrador Iaraujo",
                CPF = "99999999999",
                Telefone = "99999999999",
                DataNascimento = DateTime.UtcNow.AddYears(-30),
                ScoreAtual = 1000
            };
            admin.Profile = profile;

            context.Users.Add(admin);
        }

        // Seed cliente1@p2p.com
        {
            var hasher = new PasswordHasher<User>();
            var cliente = context.Users.Include(u => u.Profile).FirstOrDefault(u => u.Email == "cliente1@p2p.com");
            if (cliente == null)
            {
                cliente = new User
                {
                    Id = Guid.NewGuid(),
                    Email = "cliente1@p2p.com",
                    Role = "Cliente",
                    Status = "Aprovado"
                };
                cliente.PasswordHash = hasher.HashPassword(cliente, "Senha123!");

                var profile = new UserProfile
                {
                    Id = cliente.Id,
                    Nome = "Cliente Um",
                    CPF = "11111111111",
                    Telefone = "11911111111",
                    DataNascimento = DateTime.UtcNow.AddYears(-25),
                    ScoreAtual = 750,
                    QuestionnaireCompleted = true
                };
                cliente.Profile = profile;
                context.Users.Add(cliente);
            }
            else
            {
                // Preserva o cadastro e a senha existentes.
            }
        }

        // Seed investidor1@p2p.com
        {
            var hasher = new PasswordHasher<User>();
            var investidor = context.Users.Include(u => u.Profile).Include(u => u.Wallet).FirstOrDefault(u => u.Email == "investidor1@p2p.com");
            if (investidor == null)
            {
                investidor = new User
                {
                    Id = Guid.NewGuid(),
                    Email = "investidor1@p2p.com",
                    Role = "Investidor",
                    Status = "Aprovado"
                };
                investidor.PasswordHash = hasher.HashPassword(investidor, "Senha123!");

                var profile = new UserProfile
                {
                    Id = investidor.Id,
                    Nome = "Investidor Um",
                    CPF = "22222222222",
                    Telefone = "11922222222",
                    DataNascimento = DateTime.UtcNow.AddYears(-35),
                    ScoreAtual = 900,
                    QuestionnaireCompleted = true
                };
                investidor.Profile = profile;

                var wallet = new InvestorWallet
                {
                    Id = investidor.Id,
                    SaldoDisponivel = 50000.00m,
                    SaldoComprometido = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    TotalAportado = 50000.00m,
                    PerfilInvestimento = "Moderado",
                    Rentabilidade = 10.00m,
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow
                };
                investidor.Wallet = wallet;
                context.Users.Add(investidor);
            }
            else
            {
                // Preserva o cadastro, a senha e os saldos existentes.
                if (investidor.Wallet == null)
                {
                    investidor.Wallet = new InvestorWallet
                    {
                        Id = investidor.Id,
                        SaldoDisponivel = 50000.00m,
                        SaldoComprometido = 0,
                        SaldoInvestido = 0,
                        Rendimentos = 0,
                        TotalAportado = 50000.00m,
                        PerfilInvestimento = "Moderado",
                        Rentabilidade = 10.00m,
                        DataCriacao = DateTime.UtcNow,
                        DataAtualizacao = DateTime.UtcNow
                    };
                }
                else
                {
                    // Nenhuma restaura??o de saldo de demonstra??o em reinicializa??es.
                }
            }
        }

        // 2. Seed Perguntas de Score
        if (!context.Questions.Any())
        {
            var q1 = new Question
            {
                Id = Guid.NewGuid(),
                Titulo = "Há quanto tempo trabalha?",
                Ativa = true
            };
            q1.Options = new List<AnswerOption>
            {
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q1.Id, Texto = "Menos de 6 meses", Pontuacao = 10 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q1.Id, Texto = "1 ano", Pontuacao = 30 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q1.Id, Texto = "2 anos", Pontuacao = 60 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q1.Id, Texto = "Mais de 3 anos", Pontuacao = 100 }
            };

            var q2 = new Question
            {
                Id = Guid.NewGuid(),
                Titulo = "Qual é o faturamento mensal aproximado?",
                Ativa = true
            };
            q2.Options = new List<AnswerOption>
            {
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q2.Id, Texto = "Menos de R$ 5.000", Pontuacao = 50 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q2.Id, Texto = "R$ 5.000 a R$ 15.000", Pontuacao = 150 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q2.Id, Texto = "Mais de R$ 15.000", Pontuacao = 300 }
            };

            var q3 = new Question
            {
                Id = Guid.NewGuid(),
                Titulo = "Você possui restrições de crédito ativas (SPC/Serasa)?",
                Ativa = true
            };
            q3.Options = new List<AnswerOption>
            {
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q3.Id, Texto = "Sim", Pontuacao = 100 },
                new AnswerOption { Id = Guid.NewGuid(), QuestionId = q3.Id, Texto = "Não", Pontuacao = 600 }
            };

            context.Questions.AddRange(q1, q2, q3);
        }

        // 3. Seed Fundo de Proteção Inicial
        if (!context.ProtectionFunds.Any())
        {
            context.ProtectionFunds.Add(new ProtectionFund
            {
                Id = Guid.NewGuid(),
                SaldoAtual = 10000.00m,
                UltimaAtualizacao = DateTime.UtcNow
            });
        }

        // 4. Seed de Solicitação de Saque de Demonstração
        {
            var investidor = context.Users.FirstOrDefault(u => u.Email == "investidor1@p2p.com");
            if (investidor != null && !context.WithdrawalRequests.Any(w => w.InvestorId == investidor.Id))
            {
                context.WithdrawalRequests.Add(new backend.Domain.Entities.WithdrawalRequest
                {
                    Id = Guid.NewGuid(),
                    InvestorId = investidor.Id,
                    Valor = 1000.00m,
                    Status = "Pending",
                    ChavePix = "investidor1@p2p.com",
                    TipoChavePix = "Email",
                    Observacao = "Saque de demonstração — aguardando aprovação do administrador.",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // 5. Seed de Produtos de Investimento (Prateleira Bancária)
        if (!context.InvestmentProducts.Any())
        {
            context.InvestmentProducts.AddRange(
                new InvestmentProduct
                {
                    Id = Guid.NewGuid(),
                    Nome = "Pool de Liquidez Conservador",
                    RentabilidadeMensal = 1.80m,
                    ValorMinimo = 5000.00m,
                    Descricao = "Investimento diversificado em tomadores com score elevado (A/B), com garantia e mitigação de risco.",
                    Condicoes = "Liquidez imediata para resgate com aviso de 30 dias. Cobertura proporcional pelo Fundo de Proteção.",
                    Risco = "Baixo",
                    PrazoMinimo = "Livre",
                    Ativo = true,
                    CreatedAt = DateTime.UtcNow
                },
                new InvestmentProduct
                {
                    Id = Guid.NewGuid(),
                    Nome = "Crédito P2P Moderado",
                    RentabilidadeMensal = 2.50m,
                    ValorMinimo = 10000.00m,
                    Descricao = "Alocação em contratos com rentabilidade atrativa e score intermediário (B/C).",
                    Condicoes = "Amortizações mensais creditadas diretamente no saldo com rendimentos reinvestíveis.",
                    Risco = "Moderado",
                    PrazoMinimo = "90 dias",
                    Ativo = true,
                    CreatedAt = DateTime.UtcNow
                },
                new InvestmentProduct
                {
                    Id = Guid.NewGuid(),
                    Nome = "Crédito Estruturado Alta Rentabilidade",
                    RentabilidadeMensal = 3.20m,
                    ValorMinimo = 25000.00m,
                    Descricao = "Voltado para investidores arrojados buscando maximização de rendimentos em operações de curto/médio prazo.",
                    Condicoes = "Maior taxa de retorno, com garantias contratuais e esteira de cobrança intensiva.",
                    Risco = "Alto",
                    PrazoMinimo = "180 dias",
                    Ativo = true,
                    CreatedAt = DateTime.UtcNow
                }
            );
        }

        context.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Falha na inicialização do banco de dados.");
        throw;
    }
}

app.Run();
