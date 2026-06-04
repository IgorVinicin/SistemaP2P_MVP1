using System;
using System.Collections.Generic;

namespace backend.Application.DTOs
{
    public class SubmitAnswersDto
    {
        public List<AnswerSubmissionDto> Answers { get; set; } = new List<AnswerSubmissionDto>();
    }

    public class AnswerSubmissionDto
    {
        public Guid QuestionId { get; set; }
        public Guid OptionId { get; set; }
    }
}
