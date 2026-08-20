using backend.Application.Services;
using System;
using Xunit;

namespace backend.Tests.Services
{
    public class ValidationHelperTests
    {
        [Theory]
        [InlineData("52998224725", true)] // CPF válido gerado
        [InlineData("11144477735", true)] // CPF válido
        [InlineData("11111111111", false)] // Dígitos iguais
        [InlineData("12345678900", false)] // CPF com dígito inválido
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Test_IsValidCpf(string? cpf, bool expected)
        {
            var result = ValidationHelper.IsValidCpf(cpf);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("11222333000181", true)] // CNPJ válido
        [InlineData("00000000000000", false)] // Dígitos iguais
        [InlineData("11222333000199", false)] // CNPJ inválido
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Test_IsValidCnpj(string? cnpj, bool expected)
        {
            var result = ValidationHelper.IsValidCnpj(cnpj);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void Test_IsAdult()
        {
            var today = DateTime.UtcNow.Date;
            Assert.True(ValidationHelper.IsAdult(today.AddYears(-18)));
            Assert.True(ValidationHelper.IsAdult(today.AddYears(-25)));
            Assert.False(ValidationHelper.IsAdult(today.AddYears(-17)));
            Assert.False(ValidationHelper.IsAdult(today.AddYears(-18).AddDays(1))); // Quase 18
        }

        [Theory]
        [InlineData("teste@exemplo.com", true)]
        [InlineData("usuario.p2p@dominio.com.br", true)]
        [InlineData("emailinvalido", false)]
        [InlineData("sem@dominio", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void Test_IsValidEmail(string? email, bool expected)
        {
            var result = ValidationHelper.IsValidEmail(email);
            Assert.Equal(expected, result);
        }
    }
}
