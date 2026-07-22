using System;
using System.Collections.Generic;

namespace backend.Domain.Entities
{
    public class InvestorGroup
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public string? Descricao { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ICollection<InvestorGroupMember> Members { get; set; } = new List<InvestorGroupMember>();
    }
}
