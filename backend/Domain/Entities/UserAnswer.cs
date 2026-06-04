using System;

namespace backend.Domain.Entities
{
    public class UserAnswer
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid QuestionId { get; set; }
        public Guid OptionId { get; set; }

        public User? User { get; set; }
        public Question? Question { get; set; }
        public AnswerOption? Option { get; set; }
    }
}
