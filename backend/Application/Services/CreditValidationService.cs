using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class CreditValidationResult
    {
        public bool Aprovado { get; set; }
        public decimal LimiteMaximo { get; set; }
        public decimal TaxaJuros { get; set; }
        public string NivelRisco { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
    }

    public class CreditValidationService
    {
        private readonly AppDbContext _context;

        public CreditValidationService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreditValidationResult> ValidarCreditoDoUsuario(int score)
        {
            // 1. Validar score mínimo global
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            int minScoreRequired = config?.ScoreMinimoParaSolicitacao ?? 150;

            if (score < minScoreRequired)
            {
                return new CreditValidationResult
                {
                    Aprovado = false,
                    Mensagem = "Seu score atual não permite solicitação de crédito."
                };
            }

            // 2. Encontrar a regra de crédito ativa correspondente ao score
            var rule = await _context.ScoreCreditRules
                .FirstOrDefaultAsync(r => r.Ativo && score >= r.ScoreMin && score <= r.ScoreMax);

            if (rule == null)
            {
                return new CreditValidationResult
                {
                    Aprovado = false,
                    Mensagem = "Nenhuma regra de crédito configurada para a sua faixa de score atual."
                };
            }

            return new CreditValidationResult
            {
                Aprovado = true,
                LimiteMaximo = rule.LimiteEmprestimo,
                TaxaJuros = rule.TaxaJuros,
                NivelRisco = rule.NivelRisco,
                Mensagem = "Crédito pré-aprovado com sucesso."
            };
        }

        public async Task<bool> PossuiEmprestimoAtivo(Guid userId)
        {
            var statusAtivos = new[] { "pendente", "emanalise", "aprovado", "ativo" };
            return await _context.LoanRequests
                .AnyAsync(l => l.UserId == userId && statusAtivos.Contains(l.Status.ToLower()));
        }
    }
}
