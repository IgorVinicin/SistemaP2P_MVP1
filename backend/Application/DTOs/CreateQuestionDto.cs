using System.Collections.Generic;

namespace backend.Application.DTOs
{
    public class CreateQuestionDto
    {
        public string Titulo { get; set; } = string.Empty;
        public bool Ativa { get; set; } = true;
        public List<CreateOptionDto> Options { get; set; } = new List<CreateOptionDto>();
    }

    public class CreateOptionDto
    {
        public string Texto { get; set; } = string.Empty;
        public int Pontuacao { get; set; }
    }
}
