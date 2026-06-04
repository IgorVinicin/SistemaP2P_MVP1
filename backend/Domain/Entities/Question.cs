using System;
using System.Collections.Generic;

namespace backend.Domain.Entities
{
    public class Question
    {
        public Guid Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public bool Ativa { get; set; } = true;

        public ICollection<AnswerOption> Options { get; set; } = new List<AnswerOption>();
    }
}
