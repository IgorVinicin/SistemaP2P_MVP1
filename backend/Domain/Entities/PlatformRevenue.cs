using System;

namespace backend.Domain.Entities
{
    public enum PlatformRevenueType
    {
        JurosEmprestimo,
        MultaAtraso,
        TarifaOperacional,
        ReceitaAdministrativa,
        ReceitaSaque,
        ReceitaManual,
        ReceitaDiversa
    }

    public class PlatformRevenue
    {
        public Guid Id { get; set; }
        public DateTime DataCriacao { get; set; }
        public PlatformRevenueType TipoReceita { get; set; }
        public decimal Valor { get; set; }
        
        // Relacionamentos opcionais para rastreabilidade
        public Guid? LoanId { get; set; }
        public Guid? InstallmentId { get; set; }
        public Guid? UserId { get; set; }
        
        public string? Observacao { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
