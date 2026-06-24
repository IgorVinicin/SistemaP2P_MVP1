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
        private readonly PasswordHasher<User> _passwordHasher;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _passwordHasher = new PasswordHasher<User>();
        }

        public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto)
        {
            // Validar email único
            if (await _context.Users.AnyAsync(u => u.Email.ToLower() == dto.Email.ToLower()))
            {
                throw new Exception("E-mail já cadastrado.");
            }

            // Validar CPF único
            if (await _context.UserProfiles.AnyAsync(up => up.CPF == dto.CPF))
            {
                throw new Exception("CPF já cadastrado.");
            }

            // Criar Usuário
            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = dto.Email,
                Role = dto.Role,
                Status = "Pendente" // Status inicial
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Senha);

            // Perfil do Usuário
            var profile = new UserProfile
            {
                Id = user.Id,
                Nome = dto.Nome,
                CPF = dto.CPF,
                Telefone = dto.Telefone,
                DataNascimento = dto.DataNascimento.ToUniversalTime(),
                PossuiEmpresa = dto.PossuiEmpresa,
                ScoreAtual = 0 // Inicialmente zero, será recalculado no questionário
            };

            user.Profile = profile;

            // Se possui empresa, cadastrar perfil da empresa (Etapa 2)
            if (dto.PossuiEmpresa)
            {
                if (string.IsNullOrEmpty(dto.RazaoSocial) || string.IsNullOrEmpty(dto.CNPJ))
                {
                    throw new Exception("Dados da empresa são obrigatórios se 'PossuiEmpresa' for verdadeiro.");
                }

                // Validar CNPJ único
                if (await _context.CompanyProfiles.AnyAsync(cp => cp.CNPJ == dto.CNPJ))
                {
                    throw new Exception("CNPJ já está cadastrado.");
                }

                var company = new CompanyProfile
                {
                    Id = user.Id,
                    RazaoSocial = dto.RazaoSocial,
                    NomeFantasia = dto.NomeFantasia ?? string.Empty,
                    CNPJ = dto.CNPJ,
                    TempoEmpresa = dto.TempoEmpresa ?? 0,
                    FaturamentoMensal = dto.FaturamentoMensal ?? 0
                };
                user.Company = company;
            }

            // Se for investidor, inicializa a carteira dele
            if (dto.Role.Equals("Investidor", StringComparison.OrdinalIgnoreCase))
            {
                var wallet = new InvestorWallet
                {
                    Id = user.Id,
                    SaldoDisponivel = 0,
                    SaldoInvestido = 0,
                    Rendimentos = 0
                };
                user.Wallet = wallet;
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var token = GenerateJwtToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Token = token
            };
        }

        public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
        {
            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == dto.Email.ToLower());

            if (user == null)
            {
                throw new Exception("Credenciais inválidas.");
            }

            var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, dto.Senha);
            if (verificationResult == PasswordVerificationResult.Failed)
            {
                throw new Exception("Credenciais inválidas.");
            }

            var token = GenerateJwtToken(user);

            return new AuthResponseDto
            {
                UserId = user.Id,
                Email = user.Email,
                Role = user.Role,
                Status = user.Status,
                Token = token
            };
        }

        public string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var jwtKey = _configuration["Jwt:Key"] ?? "SuperSecretKeyForP2PLendingMVP1Secret!!!";
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
