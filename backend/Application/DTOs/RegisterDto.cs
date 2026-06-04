using System;

namespace backend.Application.DTOs
{
    public class RegisterDto
    {
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Senha { get; set; } = string.Empty;
        public string Role { get; set; } = "Cliente"; // Cliente ou Investidor

        // Campos opcionais de Empresa (Etapa 2 - Empresa)
        public bool PossuiEmpresa { get; set; } = false;
        public string? RazaoSocial { get; set; }
        public string? NomeFantasia { get; set; }
        public string? CNPJ { get; set; }
        public int? TempoEmpresa { get; set; } // Em meses
        public decimal? FaturamentoMensal { get; set; }
    }
}
