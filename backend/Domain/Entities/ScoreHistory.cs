using System;

namespace backend.Domain.Entities
{
    public class ScoreHistory
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int ScoreAnterior { get; set; }
        public int ScoreNovo { get; set; }
        public string Motivo { get; set; } = string.Empty;
        public DateTime DataAlteracao { get; set; } = DateTime.UtcNow;
        public Guid AdminId { get; set; }

        public User? User { get; set; }
    }
}
