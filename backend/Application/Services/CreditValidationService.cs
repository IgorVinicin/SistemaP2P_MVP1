using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
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

        public async Task<(bool PodeSolicitar, string MensagemBlock, int ParcelasPagas, int MinimoExigido)> VerificarElegibilidadeRenovacao(Guid userId)
        {
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            int minimoExigido = config?.MinimoParcelasPagasNovaSolicitacao ?? 4;

            // Encontrar empréstimos pendentes ou ativos
            var activeLoans = await _context.LoanRequests
                .Where(l => l.UserId == userId &&
                            (l.Status == "Pendente" ||
                             l.Status == "EmAnalise" ||
                             l.Status == "Aprovado" ||
                             l.Status == "Ativo" ||
                             l.Status == "EmPagamento" ||
                             l.Status == "Inadimplente"))
                .ToListAsync();

            if (activeLoans.Count == 0)
            {
                return (true, string.Empty, 0, minimoExigido);
            }

            // Regra especial: se o tomador tem alguma solicitação paralela em andamento (Pendente ou EmAnalise), ele não pode solicitar outra
            var pendente = activeLoans.FirstOrDefault(l => l.Status == "Pendente" || l.Status == "EmAnalise");
            if (pendente != null)
            {
                return (false, "Você já possui uma solicitação de empréstimo em análise.", 0, minimoExigido);
            }

            // Para cada empréstimo ativo, verificar a quantidade de parcelas pagas
            int menorParcelasPagas = int.MaxValue;
            bool temEmprestimoBloqueante = false;

            foreach (var loan in activeLoans)
            {
                int pagasCount = await _context.LoanInstallments
                    .CountAsync(inst => inst.LoanRequestId == loan.Id && inst.Status == "Paga");

                if (pagasCount < menorParcelasPagas)
                {
                    menorParcelasPagas = pagasCount;
                }

                if (pagasCount < minimoExigido)
                {
                    temEmprestimoBloqueante = true;
                }
            }

            int pagasExibicao = menorParcelasPagas == int.MaxValue ? 0 : menorParcelasPagas;

            if (temEmprestimoBloqueante)
            {
                return (false, $"Você precisa quitar pelo menos {minimoExigido} parcelas para solicitar um novo empréstimo.", pagasExibicao, minimoExigido);
            }

            return (true, string.Empty, pagasExibicao, minimoExigido);
        }
    }
}
