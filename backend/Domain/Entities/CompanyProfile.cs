using System;

namespace backend.Domain.Entities
{
    public class CompanyProfile
    {
        public Guid Id { get; set; } // FK para User.Id
        public string RazaoSocial { get; set; } = string.Empty;
        public string NomeFantasia { get; set; } = string.Empty;
        public string CNPJ { get; set; } = string.Empty;
        public int TempoEmpresa { get; set; } // Em meses
        public decimal FaturamentoMensal { get; set; }

        // Propriedade de navegação
        public User? User { get; set; }
    }
}
