using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ICpfConsultationService _cpfConsultationService;
        private readonly ILoginAttemptService _loginAttemptService;
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthService(
            AppDbContext context, 
            IConfiguration configuration,
            ICpfConsultationService cpfConsultationService,
            ILoginAttemptService loginAttemptService)
        {
            _context = context;
            _configuration = configuration;
            _cpfConsultationService = cpfConsultationService;
            _loginAttemptService = loginAttemptService;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Senha) || dto.Senha.Length < 4)
            {
                throw new Exception("A senha deve possuir pelo menos 4 caracteres.");
            }

            var cleanCpf = (dto.CPF ?? string.Empty).Replace(".", "").Replace("-", "").Trim();

            // Validação de CPF de pessoa real (Algoritmo Módulo 11 + Consulta à base de dados da Receita Federal)
            var cpfResult = await _cpfConsultationService.ConsultarCpfAsync(cleanCpf, dto.DataNascimento, dto.Nome);
            if (!cpfResult.IsValid)
            {
                throw new Exception(cpfResult.ErrorMessage ?? "CPF inválido. Por favor, informe o CPF de uma pessoa real cadastrada.");
            }

            // Validação de Maioridade (idade >= 18 anos)
            if (!ValidationHelper.IsAdult(dto.DataNascimento))
            {
                throw new Exception("É necessário ser maior de 18 anos para se cadastrar.");
            }

            bool isInvestidor = string.Equals(dto.Role, "Investidor", StringComparison.OrdinalIgnoreCase);

            // Validação de E-mail
            if (!isInvestidor)
            {
                if (string.IsNullOrWhiteSpace(dto.Email) || !ValidationHelper.IsValidEmail(dto.Email))
                {
                    throw new Exception("Por favor, informe um endereço de e-mail válido.");
                }
            }
            else
            {
                // Se for investidor e o e-mail não for informado ou for inválido, usa o próprio CPF como identificador
                if (string.IsNullOrWhiteSpace(dto.Email) || !ValidationHelper.IsValidEmail(dto.Email))
                {
                    dto.Email = cleanCpf;
                }
            }

            // Validar CPF único
            if (await _context.UserProfiles.AnyAsync(up => up.CPF == cleanCpf || up.CPF == dto.CPF))
            {
                throw new Exception("CPF já cadastrado.");
            }

            // Validar e-mail único
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            {
                throw new Exception("E-mail ou CPF já cadastrado.");
            }

            // Sanitizar Role para evitar auto-atribuição de Administrador
            string requestedRole = (dto.Role ?? "Cliente").Trim();
            string finalRole = isInvestidor ? "Investidor" : "Cliente";

            // Criar Usuário
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                Role = finalRole,
                Status = "Pendente" // Status inicial
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Senha);

            // Perfil do Usuário
            var profile = new UserProfile
            {
                Id = user.Id,
                Nome = dto.Nome,
                CPF = cleanCpf,
                Telefone = dto.Telefone,
                DataNascimento = dto.DataNascimento.ToUniversalTime(),
                PossuiEmpresa = !isInvestidor && dto.PossuiEmpresa,
                ScoreAtual = 0
            };

            user.Profile = profile;

            // Se possui empresa (apenas para tomador)
            if (!isInvestidor && dto.PossuiEmpresa)
            {
                if (string.IsNullOrEmpty(dto.RazaoSocial) || string.IsNullOrEmpty(dto.CNPJ))
                {
                    throw new Exception("Dados da empresa são obrigatórios se 'PossuiEmpresa' for verdadeiro.");
                }

                var cleanCnpj = dto.CNPJ.Replace(".", "").Replace("-", "").Replace("/", "").Trim();
                if (!ValidationHelper.IsValidCnpj(cleanCnpj))
                {
                    throw new Exception("CNPJ inválido. Por favor, informe um CNPJ real com dígitos verificadores válidos.");
                }

                // Validar CNPJ único
                if (await _context.CompanyProfiles.AnyAsync(cp => cp.CNPJ == cleanCnpj || cp.CNPJ == dto.CNPJ))
                {
                    throw new Exception("CNPJ já está cadastrado.");
                }

                var company = new CompanyProfile
                {
                    Id = user.Id,
                    RazaoSocial = dto.RazaoSocial,
                    NomeFantasia = dto.NomeFantasia ?? string.Empty,
                    CNPJ = cleanCnpj,
                    TempoEmpresa = dto.TempoEmpresa ?? 0,
                    FaturamentoMensal = dto.FaturamentoMensal ?? 0
                };
                user.Company = company;
            }

            // Se for investidor, inicializa a carteira dele
            if (isInvestidor)
            {
                var config = await _context.CreditConfigs.FirstOrDefaultAsync();
                decimal minInvestimento = config?.ValorMinimoInvestimento ?? 5000.00m;
                decimal baseRentabilidade = config?.TaxaRendimentoPool ?? 2.00m;

                // Buscar primeiro produto de investimento ativo para referência de rentabilidade base se existir
                var defaultProduct = await _context.InvestmentProducts
                    .Where(p => p.Ativo)
                    .OrderBy(p => p.ValorMinimo)
                    .FirstOrDefaultAsync();

                if (defaultProduct != null)
                {
                    baseRentabilidade = defaultProduct.RentabilidadeMensal;
                    if (defaultProduct.ValorMinimo > 0)
                    {
                        minInvestimento = defaultProduct.ValorMinimo;
                    }
                }

                // Se informou intenção de aporte inicial, valida rigorosamente
                if (dto.ValorInicialInvestimento.HasValue)
                {
                    decimal valor = dto.ValorInicialInvestimento.Value;

                    if (valor <= 0)
                    {
                        throw new Exception("O valor pretendido para investimento deve ser positivo e maior que zero.");
                    }

                    if (decimal.Round(valor, 2) != valor)
                    {
                        throw new Exception("O valor de investimento deve possuir no máximo 2 casas decimais.");
                    }

                    if (valor < minInvestimento)
                    {
                        throw new Exception($"O valor mínimo para investimento é de R$ {minInvestimento:N2}.");
                    }
                }

                var wallet = new InvestorWallet
                {
                    Id = user.Id,
                    SaldoDisponivel = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0,
                    Rentabilidade = baseRentabilidade,
                    PerfilInvestimento = "Conservador"
                };
                user.Wallet = wallet;

                if (dto.ValorInicialInvestimento.HasValue && dto.ValorInicialInvestimento.Value > 0)
                {
                    _context.DepositRequests.Add(new DepositRequest
                    {
                        Id = Guid.NewGuid(),
                        InvestorId = user.Id,
                        Valor = dto.ValorInicialInvestimento.Value,
                        Status = "Pending",
                        Observacao = $"Aporte inicial informado durante o cadastro: {dto.ValorInicialInvestimento.Value:C2}",
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Nome = profile.Nome,
                Role = user.Role,
                Status = user.Status,
                CPF = profile.CPF,
                Token = token
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto, string? ipAddress = null)
        {
            var input = (dto.Email ?? string.Empty).Trim();
            var cleanCpfInput = input.Replace(".", "").Replace("-", "").Replace("/", "").Trim();

            // 1. Verificar bloqueio temporário anti-força bruta por identificador ou IP
            if (_loginAttemptService.IsLockedOut(cleanCpfInput, ipAddress, out var remaining))
            {
                int minutos = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
                throw new Exception($"Muitas tentativas incorretas. Por motivos de segurança, tente novamente em {minutos} minuto(s).");
            }

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => 
                    u.Email.ToLower() == input.ToLower() ||
                    u.Email == cleanCpfInput ||
                    u.Email == $"{cleanCpfInput}@investor.local" ||
                    (u.Profile != null && (u.Profile.CPF == cleanCpfInput || u.Profile.CPF == input))
                );

            if (user == null)
            {
                _loginAttemptService.RegisterFailedAttempt(cleanCpfInput, ipAddress);
                throw new Exception("E-mail, CPF ou senha incorretos.");
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Senha);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                _loginAttemptService.RegisterFailedAttempt(cleanCpfInput, ipAddress);
                throw new Exception("E-mail, CPF ou senha incorretos.");
            }

            // Login bem-sucedido: limpar histórico de tentativas incorretas
            _loginAttemptService.ResetAttempts(cleanCpfInput, ipAddress);

            var token = GenerateJwtToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Nome = user.Profile?.Nome ?? string.Empty,
                Role = user.Role,
                Status = user.Status,
                CPF = user.Profile?.CPF ?? string.Empty,
                Token = token
            };
        }

        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = Environment.GetEnvironmentVariable("JWT_KEY") ?? _configuration["Jwt:Key"]
                ?? throw new InvalidOperationException("Chave JWT não configurada.");
            var key = Encoding.ASCII.GetBytes(jwtKey);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("Status", user.Status)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(7),
                Issuer = _configuration["Jwt:Issuer"] ?? "P2PLendingAPI",
                Audience = _configuration["Jwt:Audience"] ?? "P2PLendingClients",
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
