using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class AllocationEngineService
    {
        private readonly AppDbContext _context;
        private readonly LedgerService _ledgerService;

        public AllocationEngineService(AppDbContext context, LedgerService? ledgerService = null)
        {
            _context = context;
            _ledgerService = ledgerService ?? new LedgerService(context);
        }

        /// <summary>
        /// Executa o motor de alocação automática de investidores para um empréstimo aprovado.
        /// Retorna (true, diagnostico) em caso de sucesso ou (false, mensagem_erro) em caso de falha.
        /// IMPORTANTE: Este método NÃO chama SaveChangesAsync — o chamador é responsável por persistir.
        /// </summary>
        public async Task<(bool Success, string ErrorMessage)> AllocateInvestorsForLoanAsync(Guid loanRequestId, Guid? investorGroupId = null)
        {
            var loan = await _context.LoanRequests
                .FirstOrDefaultAsync(l => l.Id == loanRequestId);

            if (loan == null)
            {
                Console.WriteLine($"[ALOCACAO-LOG] Empréstimo {loanRequestId} não encontrado.");
                return (false, "Solicitação de empréstimo não encontrada.");
            }

            decimal loanAmount = loan.ValorSolicitado;
            Console.WriteLine($"[ALOCACAO-LOG] Iniciando alocação para Empréstimo {loanRequestId} | Valor: {loanAmount:C2}");

            // ====================================================================
            // PASSO 1 e 2 UNIFICADOS E OTIMIZADOS
            // ====================================================================
            var config = await _context.CreditConfigs.FirstOrDefaultAsync();
            int maxInvestors = config?.MaxInvestidoresPorEmprestimo ?? 10;
            if (maxInvestors <= 0) maxInvestors = 10;

            Console.WriteLine($"[ALOCACAO-LOG] Limite máximo de investidores configurado: {maxInvestors}");

            var eligibleQuery = _context.Users
                .Where(u => u.Role == "Investidor" && u.Status == "Aprovado")
                .Join(
                    _context.InvestorWallets,
                    u => u.Id,
                    w => w.Id,
                    (u, w) => new { User = u, Wallet = w }
                );

            if (investorGroupId.HasValue && investorGroupId.Value != Guid.Empty)
            {
                var groupInvestorIds = _context.InvestorGroupMembers
                    .Where(m => m.GroupId == investorGroupId.Value)
                    .Select(m => m.InvestorId);

                eligibleQuery = eligibleQuery.Where(x => groupInvestorIds.Contains(x.User.Id));
            }

            eligibleQuery = eligibleQuery
                .Where(x => x.Wallet.SaldoDisponivel > 0)
                .OrderByDescending(x => x.Wallet.SaldoDisponivel)
                .Take(maxInvestors);

            var eligibleInvestors = await eligibleQuery.ToListAsync();

            Console.WriteLine($"[ALOCACAO-LOG] Investidores elegíveis recuperados no banco: {eligibleInvestors.Count}");

            if (!eligibleInvestors.Any())
            {
                Console.WriteLine($"[ALOCACAO-LOG] FALHOU: Nenhum investidor elegível encontrado.");
                if (investorGroupId.HasValue && investorGroupId.Value != Guid.Empty)
                {
                    return (false, "Nenhum investidor do grupo selecionado possui saldo disponível.");
                }
                return (false, "Nenhum investidor elegível encontrado na plataforma com saldo disponível.");
            }

            // ====================================================================
            // PASSO 3: Algoritmo iterativo de distribuição igualitária
            // ====================================================================
            var pool = eligibleInvestors.ToList();
            bool overallStable = false;
            var allocations = new Dictionary<Guid, decimal>();
            int outerLoopSafety = 0;

            while (!overallStable && pool.Any() && outerLoopSafety < 100)
            {
                outerLoopSafety++;
                bool poolStable = false;
                int iterations = 0;

                while (!poolStable && pool.Any() && iterations < 100)
                {
                    iterations++;
                    int M = pool.Count;
                    decimal allocatedSum = 0;
                    allocations.Clear();

                    for (int i = 0; i < M; i++)
                    {
                        decimal shareForInv = i < M - 1
                            ? Math.Round(loanAmount / M, 2)
                            : loanAmount - allocatedSum;

                        allocations[pool[i].User.Id] = shareForInv;
                        allocatedSum += shareForInv;
                    }

                    var tooLow = pool
                        .Where(p => p.Wallet.SaldoDisponivel < allocations[p.User.Id])
                        .ToList();

                    Console.WriteLine($"[ALOCACAO-LOG] Iteração {iterations} (Outer {outerLoopSafety}): Pool={M} | Excluídos por saldo insuficiente={tooLow.Count}");
                    foreach (var t in tooLow)
                    {
                        Console.WriteLine($"[ALOCACAO-LOG]   Excluído: {t.User.Email} | SaldoDisp={t.Wallet.SaldoDisponivel:C2} < FatiaRequerida={allocations[t.User.Id]:C2}");
                    }

                    if (!tooLow.Any())
                    {
                        poolStable = true;
                    }
                    else
                    {
                        foreach (var low in tooLow)
                            pool.Remove(low);
                    }
                }

                if (!poolStable || !pool.Any())
                {
                    break;
                }

                // Se o pool já está estável e seu tamanho é menor ou igual ao limite de investidores, terminamos!
                if (pool.Count <= maxInvestors)
                {
                    overallStable = true;
                }
                else
                {
                    // Pool estável, mas excede maxInvestors.
                    // Pegamos os top maxInvestors (que já estão em ordem decrescente de saldo) e re-estabilizamos.
                    Console.WriteLine($"[ALOCACAO-LOG] Pool estável de {pool.Count} investidores excede o limite de {maxInvestors}. Filtrando os top {maxInvestors} com maiores saldos e re-estabilizando.");
                    pool = pool.Take(maxInvestors).ToList();
                }
            }

            if (!overallStable || !pool.Any())
            {
                decimal saldoTotalPool = eligibleInvestors.Sum(p => p.Wallet.SaldoDisponivel);
                Console.WriteLine($"[ALOCACAO-LOG] FALHOU: Pool de investidores insuficiente.");
                Console.WriteLine($"[ALOCACAO-LOG]   Valor do empréstimo: {loanAmount:C2} | Saldo total no pool: {saldoTotalPool:C2}");
                
                if (investorGroupId.HasValue && investorGroupId.Value != Guid.Empty)
                {
                    var groupMembers = await _context.InvestorGroupMembers
                        .Where(m => m.GroupId == investorGroupId.Value)
                        .Include(m => m.Investor)
                        .ThenInclude(u => u!.Profile)
                        .Join(_context.InvestorWallets, m => m.InvestorId, w => w.Id, (m, w) => new { m, w })
                        .ToListAsync();
                    
                    var details = groupMembers.Select(x => $"{x.m.Investor!.Profile?.Nome ?? x.m.Investor.Email}: {x.w.SaldoDisponivel:C2}");
                    string detailsStr = string.Join(" | ", details);
                    return (false, $"O grupo selecionado não possui saldo suficiente para cobrir {loanAmount:C2}. Saldos atuais: {detailsStr}");
                }

                return (false, "Falha na alocação: Saldo insuficiente entre os investidores elegíveis na plataforma.");
            }

            // ====================================================================
            // PASSO 4: Distribuir participações e registrar no banco
            // ====================================================================
            Console.WriteLine($"[ALOCACAO-LOG] Distribuindo entre {pool.Count} investidores...");

                    foreach (var item in pool)
                    {
                        var wallet = item.Wallet;
                        decimal allocatedShare = allocations[item.User.Id];

                        decimal saldoAnterior = wallet.SaldoDisponivel;
                        if (wallet.SaldoDisponivel < allocatedShare)
                            throw new InvalidOperationException("Saldo insuficiente para reserva.");
                        wallet.SaldoDisponivel -= allocatedShare;
                        wallet.SaldoComprometido += allocatedShare;
                        wallet.DataAtualizacao = DateTime.UtcNow;

                        Console.WriteLine($"[ALOCACAO-LOG]   {item.User.Email}: SaldoDisp {saldoAnterior:C2} → {wallet.SaldoDisponivel:C2} | SaldoComp +{allocatedShare:C2} → {wallet.SaldoComprometido:C2}");

                        // Criar registro de participação (Investment)
                        var investment = new Investment
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = item.User.Id,
                            LoanId = loan.Id,
                            ValorInvestido = allocatedShare,
                            SaldoPrincipalPendente = allocatedShare,
                            Status = "Comprometido",
                            Rentabilidade = wallet.Rentabilidade,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.Investments.Add(investment);

                        // Registrar transação do investidor
                        var investorTx = new InvestorTransaction
                        {
                            Id = Guid.NewGuid(),
                            InvestorId = item.User.Id,
                            Tipo = "ALOCACAO_AUTOMATICA",
                            Valor = -allocatedShare,
                            SaldoAnterior = saldoAnterior,
                            SaldoPosterior = wallet.SaldoDisponivel,
                            DataMovimentacao = DateTime.UtcNow,
                            Descricao = $"Alocação automática comprometida para o empréstimo {loan.Id} — Valor: {allocatedShare:C2}",
                            UsuarioResponsavel = "Sistema"
                        };
                        _context.InvestorTransactions.Add(investorTx);

                        // Registrar no histórico financeiro
                        _context.FinancialEventHistories.Add(new FinancialEventHistory
                        {
                            Id = Guid.NewGuid(),
                            Data = DateTime.UtcNow,
                            UsuarioResponsavel = "Sistema",
                            EventoExecutado = "ALOCACAO_COMPROMETIDO",
                            Observacao = $"Alocação comprometida para investidor {item.User.Email} no empréstimo {loan.Id} — Valor: {allocatedShare:C2}",
                            LoanRequestId = loan.Id,
                            Valor = allocatedShare
                        });

                        // Reservation changes wallet compartments; no borrower payment occurs here.
                    }

            return (true, string.Empty);
        }
    }
}
