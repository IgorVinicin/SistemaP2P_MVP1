using System;

namespace backend.Domain.Entities
{
    public class LimitRequest
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public decimal LimiteSolicitado { get; set; }
        public decimal LimiteAtualNoMomento { get; set; }
        public string Status { get; set; } = "Pendente"; // Pendente, Aprovado, Rejeitado
        public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
        public DateTime? DataAnalise { get; set; }
        public string? ObservacaoAdmin { get; set; }

        // Propriedade de navegação
        public User? User { get; set; }
    }
}
