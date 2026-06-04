using System;

namespace backend.Domain.Entities
{
    public class Document
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string TipoDocumento { get; set; } = string.Empty; // Frente, Verso, Selfie, FachadaEmpresa
        public string CaminhoArquivo { get; set; } = string.Empty;
        public string Status { get; set; } = "Pendente"; // Pendente, Aprovado, Reprovado
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Propriedade de navegação
        public User? User { get; set; }
    }
}
