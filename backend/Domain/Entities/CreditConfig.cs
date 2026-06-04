using System;

namespace backend.Domain.Entities
{
    public class CreditConfig
    {
        public Guid Id { get; set; }
        public int ScoreMinimoParaSolicitacao { get; set; } = 150; // Default
    }
}
