using System;

namespace backend.Domain.Entities
{
    public class AnswerOption
    {
        public Guid Id { get; set; }
        public Guid QuestionId { get; set; }
        public string Texto { get; set; } = string.Empty;
        public int Pontuacao { get; set; }

        public Question? Question { get; set; }
    }
}
