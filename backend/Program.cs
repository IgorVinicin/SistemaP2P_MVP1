using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<CreditValidationService>();
builder.Services.AddScoped<DelinquencyService>();
builder.Services.AddScoped<AllocationEngineService>();

// Configuração JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "SuperSecretKeyForP2PLendingMVP1Secret!!!";
var key = Encoding.ASCII.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
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
        policy.WithOrigins("http://localhost:4200") // Origem padrão do Angular
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Seed Database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();

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
                MinimoParcelasPagasNovaSolicitacao = 4
            });
        }

        // 1. Seed Administrador
        if (!context.Users.Any(u => u.Email == "admin@p2p.com"))
        {
            var hasher = new PasswordHasher<User>();
            var admin = new User
            {
                Id = Guid.NewGuid(),
                Email = "admin@p2p.com",
                Role = "Administrador",
                Status = "Aprovado"
            };
            admin.PasswordHash = hasher.HashPassword(admin, "Admin123!");

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

        // 4. Seed Test Investors
        var testInvestors = new[]
        {
            new { Email = "investidor1@p2p.com", Nome = "Investidor Alpha", CPF = "11111111111", Saldo = 10000.00m, Rentabilidade = 5.00m },
            new { Email = "investidor2@p2p.com", Nome = "Investidor Beta", CPF = "22222222222", Saldo = 15000.00m, Rentabilidade = 6.00m },
            new { Email = "investidor3@p2p.com", Nome = "Investidor Gamma", CPF = "33333333333", Saldo = 12000.00m, Rentabilidade = 4.00m },
            new { Email = "investidor4@p2p.com", Nome = "Investidor LowBalance", CPF = "44444444444", Saldo = 1000.00m, Rentabilidade = 5.00m }
        };

        var testPasswordHasher = new PasswordHasher<User>();

        foreach (var invData in testInvestors)
        {
            if (!context.Users.Any(u => u.Email == invData.Email))
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = invData.Email,
                    Role = "Investidor",
                    Status = "Aprovado"
                };
                user.PasswordHash = testPasswordHasher.HashPassword(user, "Senha123!");

                var profile = new UserProfile
                {
                    Id = user.Id,
                    Nome = invData.Nome,
                    CPF = invData.CPF,
                    Telefone = "11999999999",
                    DataNascimento = DateTime.UtcNow.AddYears(-35),
                    ScoreAtual = 800,
                    QuestionnaireCompleted = true
                };

                var wallet = new InvestorWallet
                {
                    Id = user.Id,
                    SaldoDisponivel = invData.Saldo,
                    SaldoComprometido = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    TotalAportado = invData.Saldo,
                    TotalSacado = 0,
                    PerfilInvestimento = "Moderado",
                    Rentabilidade = invData.Rentabilidade,
                    DataCriacao = DateTime.UtcNow,
                    DataAtualizacao = DateTime.UtcNow
                };

                user.Profile = profile;
                user.Wallet = wallet;

                context.Users.Add(user);
            }
        }

        // 5. Seed Test Borrowers
        var testBorrowers = new[]
        {
            new { Email = "tomador1@p2p.com", Nome = "Tomador Um", CPF = "55555555555", Score = 800 },
            new { Email = "tomador2@p2p.com", Nome = "Tomador Dois", CPF = "66666666666", Score = 250 }
        };

        foreach (var bData in testBorrowers)
        {
            if (!context.Users.Any(u => u.Email == bData.Email))
            {
                var user = new User
                {
                    Id = Guid.NewGuid(),
                    Email = bData.Email,
                    Role = "Cliente",
                    Status = "Aprovado"
                };
                user.PasswordHash = testPasswordHasher.HashPassword(user, "Senha123!");

                var profile = new UserProfile
                {
                    Id = user.Id,
                    Nome = bData.Nome,
                    CPF = bData.CPF,
                    Telefone = "11988888888",
                    DataNascimento = DateTime.UtcNow.AddYears(-28),
                    ScoreAtual = bData.Score,
                    QuestionnaireCompleted = true
                };

                user.Profile = profile;
                context.Users.Add(user);
            }
        }

        // 6. Seed Test Loan Request for Tomador 1
        var tomadorUser = context.Users.Include(u => u.LoanRequests).FirstOrDefault(u => u.Email == "tomador1@p2p.com");
        if (tomadorUser != null && !tomadorUser.LoanRequests.Any())
        {
            var loan = new LoanRequest
            {
                Id = Guid.NewGuid(),
                UserId = tomadorUser.Id,
                ValorSolicitado = 6000.00m,
                Parcelas = 12,
                QuantidadeParcelas = 12,
                Status = "Pendente",
                ScoreNaSolicitacao = 800,
                ScoreNoMomento = 800,
                Finalidade = "Simulação e Teste de Alocação P2P",
                Observacoes = "Criado automaticamente pelo seed de teste.",
                TaxaJurosAplicada = 2.00m,
                TaxaJuros = 2.00m,
                ValorTotal = 7440.00m,
                NivelRisco = "Baixo",
                CreatedAt = DateTime.UtcNow,
                DataCriacao = DateTime.UtcNow,
                DataSolicitacao = DateTime.UtcNow
            };
            context.LoanRequests.Add(loan);
        }

        context.SaveChanges();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Erro ao rodar seed de banco de dados: {ex.Message}");
    }
}

app.Run();
