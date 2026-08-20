using System;
using System.ComponentModel.DataAnnotations;

namespace backend.Domain.Entities
{
    public class InvestorWallet
    {
        public Guid Id { get; set; } // FK para User.Id
        public decimal SaldoDisponivel { get; set; } = 0;
        public decimal SaldoBloqueado { get; set; } = 0;
        public decimal SaldoComprometido { get; set; } = 0;
        public decimal SaldoInvestido { get; set; } = 0;
        public decimal Rendimentos { get; set; } = 0;
        public decimal TotalAportado { get; set; } = 0;
        public decimal TotalSacado { get; set; } = 0;
        public string PerfilInvestimento { get; set; } = "Conservador"; // Conservador, Moderado, Agressivo
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;
        public decimal Rentabilidade { get; set; } = 0;
        public decimal RentabilidadeSaldoDisponivel { get; set; } = 1.00m; // Definida pelo Admin
        public decimal RentabilidadeSaldoComprometido { get; set; } = 0; // Baseada na opção de investimento escolhida pelo usuário
        public string ObservacaoAdministrativa { get; set; } = string.Empty;
        
        // Controle de Escolha de Produto de Investimento
        public Guid? ProdutoInvestimentoId { get; set; }
        public string? ProdutoInvestimentoNome { get; set; }
        public bool ProdutoEscolhido { get; set; } = false;
        public bool PodeAlterarProduto { get; set; } = true;
        
        [ConcurrencyCheck]
        public uint Version { get; set; } // Otimistic Concurrency Token

        public User? User { get; set; }
    }
}
