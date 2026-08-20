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
        public decimal LimiteDisponivel { get; set; }
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

        public async Task<CreditValidationResult> ValidarCreditoDoUsuario(int score, Guid userId)
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

            // 2. Buscar o perfil do usuário para ver se há limite personalizado
            var profile = await _context.UserProfiles.FindAsync(userId);
            decimal limiteMaximo = 0;
            decimal taxaJuros = 0;
            string nivelRisco = "Médio";

            if (profile != null && profile.LimitePersonalizado.HasValue && profile.LimitePersonalizado.Value > 0)
            {
                limiteMaximo = profile.LimitePersonalizado.Value;
                // Busca a taxa de juros e risco padrão baseado no score
                var ruleForScore = await _context.ScoreCreditRules
                    .FirstOrDefaultAsync(r => r.Ativo && score >= r.ScoreMin && score <= r.ScoreMax);
                taxaJuros = ruleForScore?.TaxaJuros ?? 5.00m;
                nivelRisco = ruleForScore?.NivelRisco ?? "Médio";
            }
            else
            {
                // Encontrar a regra de crédito ativa correspondente ao score
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
                limiteMaximo = rule.LimiteEmprestimo;
                taxaJuros = rule.TaxaJuros;
                nivelRisco = rule.NivelRisco;
            }

            // Calcular principal comprometido (parcelas não pagas de empréstimos já aprovados ou em andamento)
            var principalComprometido = await _context.LoanInstallments
                .Where(i => i.LoanRequest!.UserId == userId && i.Status != "Paga" &&
                            (i.LoanRequest!.Status == "Aprovado" || i.LoanRequest!.Status == "EmCaptacao" || i.LoanRequest!.Status == "Financiado" ||
                             i.LoanRequest!.Status == "Ativo" || i.LoanRequest!.Status == "EmPagamento" || i.LoanRequest!.Status == "Inadimplente"))
                .SumAsync(i => i.ValorPrincipal);

            // Calcular solicitações que ainda estão apenas em análise inicial e não possuem parcelas (desconsiderando solicitações de aumento de limite pendentes)
            var solicitacoesPendentes = await _context.LoanRequests
                .Where(l => l.UserId == userId && (l.Status == "Pendente" || l.Status == "EmAnalise") && !l.SolicitaAumentoLimite)
                .SumAsync(l => l.ValorSolicitado);

            decimal totalComprometido = principalComprometido + solicitacoesPendentes;
            decimal limiteDisponivel = Math.Max(0, limiteMaximo - totalComprometido);

            return new CreditValidationResult
            {
                Aprovado = true,
                LimiteMaximo = limiteMaximo,
                LimiteDisponivel = limiteDisponivel,
                TaxaJuros = taxaJuros,
                NivelRisco = nivelRisco,
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
