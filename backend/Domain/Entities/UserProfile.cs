using System;

namespace backend.Domain.Entities
{
    public class UserProfile
    {
        public Guid Id { get; set; } // FK para User.Id
        public string Nome { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Telefone { get; set; } = string.Empty;
        public DateTime DataNascimento { get; set; }
        public int ScoreAtual { get; set; } = 0;
        public bool QuestionnaireCompleted { get; set; } = false;
        public bool PossuiEmpresa { get; set; } = false;
        public decimal? LimitePersonalizado { get; set; }

        // Propriedade de navegação
        public User? User { get; set; }
    }
}
