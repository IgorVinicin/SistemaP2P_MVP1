using System;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class CpfValidationResult
    {
        public bool IsValid { get; set; }
        public string? Cpf { get; set; }
        public string? NomeReceita { get; set; }
        public string? SituacaoCadastral { get; set; } // "REGULAR", "SUSPENSA", "CANCELADA", "PENDENTE_DE_REGULARIZACAO", etc.
        public DateTime? DataNascimentoReceita { get; set; }
        public string? ErrorMessage { get; set; }
        public bool ConsultouReceitaFederal { get; set; }
    }

    public interface ICpfConsultationService
    {
        Task<CpfValidationResult> ConsultarCpfAsync(string cpf, DateTime? dataNascimento = null, string? nome = null);
    }
}
