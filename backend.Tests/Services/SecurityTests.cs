using backend.Application.DTOs;
using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.Services
{
    public class DummyCpfConsultationService : ICpfConsultationService
    {
        public Task<CpfValidationResult> ConsultarCpfAsync(string cpf, DateTime? dataNascimento = null, string? nome = null)
        {
            return Task.FromResult(new CpfValidationResult { IsValid = true, Cpf = cpf, SituacaoCadastral = "REGULAR" });
        }
    }

    public class SecurityTests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new AppDbContext(options);
        }

        private IConfiguration GetMockConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"Jwt:Key", "SuperSecretKeyForSecurityTestingOnly123456!"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"},
                {"Security:MaxLoginAttempts", "3"},
                {"Security:LockoutMinutes", "15"},
                {"CpfConsultation:Enabled", "false"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Fact]
        public async Task Login_WithCorrectCredentials_ShouldSucceed()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();

            var authService = new AuthService(context, config, dummyCpf, attemptService);
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "usuario@teste.com",
                Role = "Cliente",
                Status = "Aprovado"
            };
            user.PasswordHash = hasher.HashPassword(user, "SenhaCorreta123");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act
            var result = await authService.LoginAsync(new LoginDto
            {
                Email = "usuario@teste.com",
                Senha = "SenhaCorreta123"
            }, "127.0.0.1");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(user.Id, result.UserId);
            Assert.NotEmpty(result.Token);
            Assert.Equal(0, attemptService.GetFailedAttempts("usuario@teste.com"));
        }

        [Fact]
        public async Task Login_WithIncorrectPassword_ShouldThrowAndRegisterAttempt()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();

            var authService = new AuthService(context, config, dummyCpf, attemptService);
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "usuario@teste.com",
                Role = "Cliente"
            };
            user.PasswordHash = hasher.HashPassword(user, "SenhaCorreta123");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.LoginAsync(new LoginDto
            {
                Email = "usuario@teste.com",
                Senha = "SenhaErrada"
            }, "127.0.0.1"));

            Assert.Equal("E-mail, CPF ou senha incorretos.", ex.Message);
            Assert.Equal(1, attemptService.GetFailedAttempts("usuario@teste.com"));
        }

        [Fact]
        public async Task Login_NonExistentUser_ShouldReturnGenericMessage_PreventingEnumeration()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();

            var authService = new AuthService(context, config, dummyCpf, attemptService);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.LoginAsync(new LoginDto
            {
                Email = "inexistente@teste.com",
                Senha = "QualquerSenha"
            }, "127.0.0.1"));

            // Mensagem idêntica à de senha incorreta para não revelar se o usuário existe
            Assert.Equal("E-mail, CPF ou senha incorretos.", ex.Message);
            Assert.Equal(1, attemptService.GetFailedAttempts("inexistente@teste.com"));
        }

        [Fact]
        public async Task Login_ExceedingMaxAttempts_ShouldTriggerTemporaryLockout()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration(); // Max attempts = 3
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();

            var authService = new AuthService(context, config, dummyCpf, attemptService);
            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "alvo@teste.com",
                Role = "Cliente"
            };
            user.PasswordHash = hasher.HashPassword(user, "SenhaCorreta123");
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var loginDto = new LoginDto
            {
                Email = "alvo@teste.com",
                Senha = "SenhaIncorreta"
            };

            // 3 tentativas falhas
            for (int i = 0; i < 3; i++)
            {
                await Assert.ThrowsAsync<Exception>(() => authService.LoginAsync(loginDto, "192.168.1.50"));
            }

            // 4ª tentativa: deve ser bloqueada imediatamente antes mesmo de validar a senha
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.LoginAsync(new LoginDto
            {
                Email = "alvo@teste.com",
                Senha = "SenhaCorreta123" // Mesmo com a senha correta agora, está bloqueado
            }, "192.168.1.50"));

            Assert.Contains("Muitas tentativas incorretas", ex.Message);
            Assert.True(attemptService.IsLockedOut("alvo@teste.com", "192.168.1.50", out var remaining));
            Assert.True(remaining.TotalMinutes > 0);
        }

        [Fact]
        public void PasswordStorage_MustUseSecureHash_NeverPlainText()
        {
            var user = new User { Id = Guid.NewGuid(), Email = "teste@p2p.com" };
            var hasher = new PasswordHasher<User>();
            var plainPassword = "MinhaSenhaForte@2026";

            var hash = hasher.HashPassword(user, plainPassword);

            // Não deve conter a senha em texto puro
            Assert.DoesNotContain(plainPassword, hash);
            // Deve ser um hash compatível com ASP.NET Identity (PBKDF2 com Salt)
            Assert.True(hash.Length > 30);

            var verification = hasher.VerifyHashedPassword(user, hash, plainPassword);
            Assert.Equal(PasswordVerificationResult.Success, verification);

            var wrongVerification = hasher.VerifyHashedPassword(user, hash, "SenhaErrada");
            Assert.Equal(PasswordVerificationResult.Failed, wrongVerification);
        }
    }
}
