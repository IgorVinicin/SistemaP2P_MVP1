namespace backend.Application.DTOs
{
    public class UpdateScoreDto
    {
        public int NovoScore { get; set; }
        public string Motivo { get; set; } = string.Empty;
    }
}
