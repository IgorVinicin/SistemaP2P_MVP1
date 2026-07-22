using System;

namespace backend.Domain.Entities
{
    public class InvestorGroupMember
    {
        public Guid GroupId { get; set; }
        public InvestorGroup Group { get; set; }

        public Guid InvestorId { get; set; }
        public User Investor { get; set; }
    }
}
