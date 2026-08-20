using backend.API.Controllers;
using backend.Application.DTOs;
using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace backend.Tests.Services
{
    public class InvestorSprint6Tests
    {
        private AppDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            var context = new AppDbContext(options);
            
            // Seed CreditConfig
            context.CreditConfigs.Add(new CreditConfig
            {
                Id = Guid.NewGuid(),
                ValorMinimoInvestimento = 5000.00m,
                TaxaRendimentoPool = 2.50m
            });
            context.SaveChanges();
            return context;
        }

        private IConfiguration GetMockConfiguration()
        {
            var inMemorySettings = new Dictionary<string, string?>
            {
                {"Jwt:Key", "SuperSecretKeyForInvestorTestingSprint6Key123!"},
                {"Jwt:Issuer", "TestIssuer"},
                {"Jwt:Audience", "TestAudience"},
                {"Security:MaxLoginAttempts", "5"},
                {"Security:LockoutMinutes", "15"},
                {"CpfConsultation:Enabled", "false"}
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(inMemorySettings)
                .Build();
        }

        [Fact]
        public async Task Register_InvestorWithValidAmount_ShouldCreateDepositRequestAndStoreCpf()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();
            var authService = new AuthService(context, config, dummyCpf, attemptService);

            // CPF válido (11144477735)
            var dto = new RegisterDto
            {
                Nome = "Investidor Sucesso",
                CPF = "111.444.777-35",
                Telefone = "11988887777",
                DataNascimento = DateTime.UtcNow.AddYears(-30),
                Senha = "SenhaForte123!",
                Role = "Investidor",
                ValorInicialInvestimento = 10000.00m,
                PossuiEmpresa = false
            };

            // Act
            var result = await authService.RegisterAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Investidor", result.Role);
            Assert.Equal("11144477735", result.Email); // Sem e-mail artificial, usa CPF limpo

            var savedUser = await context.Users
                .Include(u => u.Profile)
                .Include(u => u.Wallet)
                .FirstOrDefaultAsync(u => u.Id == result.UserId);

            Assert.NotNull(savedUser);
            Assert.NotNull(savedUser.Wallet);
            Assert.Equal(2.50m, savedUser.Wallet.Rentabilidade); // Padrão baseado nas regras/produtos

            var deposit = await context.DepositRequests.FirstOrDefaultAsync(d => d.InvestorId == savedUser.Id);
            Assert.NotNull(deposit);
            Assert.Equal(10000.00m, deposit.Valor);
            Assert.Equal("Pending", deposit.Status);
        }

        [Fact]
        public async Task Register_InvestorWithNegativeAmount_ShouldThrowException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();
            var authService = new AuthService(context, config, dummyCpf, attemptService);

            var dto = new RegisterDto
            {
                Nome = "Investidor Negativo",
                CPF = "111.444.777-35",
                Telefone = "11988887777",
                DataNascimento = DateTime.UtcNow.AddYears(-25),
                Senha = "SenhaForte123!",
                Role = "Investidor",
                ValorInicialInvestimento = -500.00m
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.RegisterAsync(dto));
            Assert.Contains("positivo e maior que zero", ex.Message);
        }

        [Fact]
        public async Task Register_InvestorWithAmountBelowMinimum_ShouldThrowException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();
            var authService = new AuthService(context, config, dummyCpf, attemptService);

            var dto = new RegisterDto
            {
                Nome = "Investidor Baixo",
                CPF = "111.444.777-35",
                Telefone = "11988887777",
                DataNascimento = DateTime.UtcNow.AddYears(-25),
                Senha = "SenhaForte123!",
                Role = "Investidor",
                ValorInicialInvestimento = 1000.00m // Mínimo é 5000.00
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.RegisterAsync(dto));
            Assert.Contains("mínimo", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task Register_InvestorWithInvalidPrecision_ShouldThrowException()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var config = GetMockConfiguration();
            var attemptService = new LoginAttemptService(config, NullLogger<LoginAttemptService>.Instance);
            var dummyCpf = new DummyCpfConsultationService();
            var authService = new AuthService(context, config, dummyCpf, attemptService);

            var dto = new RegisterDto
            {
                Nome = "Investidor Quebrado",
                CPF = "111.444.777-35",
                Telefone = "11988887777",
                DataNascimento = DateTime.UtcNow.AddYears(-25),
                Senha = "SenhaForte123!",
                Role = "Investidor",
                ValorInicialInvestimento = 5000.555m // 3 casas decimais
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<Exception>(() => authService.RegisterAsync(dto));
            Assert.Contains("casas decimais", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task AdminInvestmentProducts_CRUD_ShouldWorkCorrectly()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var adminController = new AdminInvestmentProductController(context);
            var publicController = new InvestmentProductController(context);

            // 1. Create Product
            var createDto = new CreateInvestmentProductDto
            {
                Nome = "CDB P2P Liquidez Diária",
                RentabilidadeMensal = 2.10m,
                ValorMinimo = 5000.00m,
                Descricao = "Investimento seguro com liquidez diária",
                Condicoes = "Garantia Fundo de Proteção",
                Risco = "Baixo",
                PrazoMinimo = "Livre",
                Ativo = true
            };

            var createResult = await adminController.CreateProduct(createDto);
            var createdObj = Assert.IsType<CreatedAtActionResult>(createResult);
            var productDto = Assert.IsType<InvestmentProductDto>(createdObj.Value);
            Assert.Equal("CDB P2P Liquidez Diária", productDto.Nome);
            Assert.Equal(2.10m, productDto.RentabilidadeMensal);

            // 2. Query Active Products from Investor Endpoint
            var activeResult = await publicController.GetActiveProducts();
            var okActive = Assert.IsType<OkObjectResult>(activeResult);
            var activeList = Assert.IsAssignableFrom<IEnumerable<InvestmentProductDto>>(okActive.Value);
            Assert.Single(activeList);

            // 3. Update Product
            var updateDto = new UpdateInvestmentProductDto
            {
                Nome = "CDB P2P Liquidez Diária Atualizado",
                RentabilidadeMensal = 2.30m,
                ValorMinimo = 6000.00m,
                Descricao = "Atualizado com maior taxa",
                Condicoes = "Garantia Total",
                Risco = "Baixo",
                PrazoMinimo = "30 dias",
                Ativo = true
            };

            var updateResult = await adminController.UpdateProduct(productDto.Id, updateDto);
            var okUpdated = Assert.IsType<OkObjectResult>(updateResult);
            var updatedProd = Assert.IsType<InvestmentProductDto>(okUpdated.Value);
            Assert.Equal("CDB P2P Liquidez Diária Atualizado", updatedProd.Nome);
            Assert.Equal(2.30m, updatedProd.RentabilidadeMensal);

            // 4. Delete Product
            var deleteResult = await adminController.DeleteProduct(productDto.Id);
            Assert.IsType<OkObjectResult>(deleteResult);

            var remaining = await context.InvestmentProducts.ToListAsync();
            Assert.Empty(remaining);
        }

        [Fact]
        public async Task AdminUserApproval_Investor_ShouldInitializeWalletWithoutArbitraryManualRentability()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            
            // Add custom product to serve as default
            context.InvestmentProducts.Add(new InvestmentProduct
            {
                Id = Guid.NewGuid(),
                Nome = "Pool Base P2P",
                RentabilidadeMensal = 2.80m,
                ValorMinimo = 5000.00m,
                Ativo = true
            });
            await context.SaveChangesAsync();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = "11144477735",
                Role = "Investidor",
                Status = "Pendente",
                Profile = new UserProfile
                {
                    Nome = "Investidor Teste",
                    CPF = "11144477735",
                    Telefone = "11999998888",
                    DataNascimento = DateTime.UtcNow.AddYears(-30)
                }
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();

            var adminController = new AdminUserController(context);

            // Act - Approve without manual rentabilidade
            var approveDto = new ApproveUserDto
            {
                ObservacaoAdministrativa = "Perfil aprovado conforme documentação."
            };
            var result = await adminController.ApproveUser(user.Id, approveDto);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var updatedUser = await context.Users.Include(u => u.Wallet).FirstOrDefaultAsync(u => u.Id == user.Id);
            Assert.NotNull(updatedUser);
            Assert.Equal("Aprovado", updatedUser.Status);
            Assert.NotNull(updatedUser.Wallet);
            Assert.Equal(2.80m, updatedUser.Wallet.Rentabilidade); // Herdado da taxa do produto ativo
            Assert.Equal("Perfil aprovado conforme documentação.", updatedUser.Wallet.ObservacaoAdministrativa);
        }
    }
}
