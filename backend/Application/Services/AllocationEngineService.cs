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

        public AllocationEngineService(AppDbContext context, LedgerService ledgerService = null)
        {
            _context = context;
            _ledgerService = ledgerService ?? new LedgerService(context);
        }

        /// <summary>
        /// Executa o motor de alocação automática de investidores para um empréstimo aprovado.
        /// Retorna (true, diagnostico) em caso de sucesso ou (false, mensagem_erro) em caso de falha.
        /// IMPORTANTE: Este método NÃO chama SaveChangesAsync — o chamador é responsável por persistir.
        /// </summary>
        public async Task<bool> AllocateInvestorsForLoanAsync(Guid loanRequestId)
        {
            var loan = await _context.LoanRequests
                .FirstOrDefaultAsync(l => l.Id == loanRequestId);

            if (loan == null)
            {
                Console.WriteLine($"[ALOCACAO-LOG] Empréstimo {loanRequestId} não encontrado.");
                return false;
            }

            decimal loanAmount = loan.ValorSolicitado;
            Console.WriteLine($"[ALOCACAO-LOG] Iniciando alocação para Empréstimo {loanRequestId} | Valor: {loanAmount:C2}");

            // ====================================================================
            // PASSO 1 e 2 UNIFICADOS E OTIMIZADOS
            // Buscar apenas os investidores Aprovados com Saldo Disponível > 0, 
            // limitados a no máximo maxInvestors, diretamente no banco de dados.
            // Isso previne estouro de memória (Out Of Memory) e lentidão.
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
                )
                .Where(x => x.Wallet.SaldoDisponivel > 0)
                .OrderByDescending(x => x.Wallet.SaldoDisponivel)
                .Take(maxInvestors);

            var eligibleInvestors = await eligibleQuery.ToListAsync();

            Console.WriteLine($"[ALOCACAO-LOG] Investidores elegíveis recuperados no banco: {eligibleInvestors.Count}");

            if (!eligibleInvestors.Any())
            {
                Console.WriteLine($"[ALOCACAO-LOG] FALHOU: Nenhum investidor elegível encontrado.");
                return false;
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
                return false;
            }

            // ====================================================================
            // PASSO 4: Distribuir participações e registrar no banco
            // ====================================================================
            Console.WriteLine($"[ALOCACAO-LOG] Distribuindo entre {pool.Count} investidores...");

            int maxRetries = 3;
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    foreach (var item in pool)
                    {
                        var wallet = item.Wallet;
                        decimal allocatedShare = allocations[item.User.Id];

                        decimal saldoAnterior = wallet.SaldoDisponivel;
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

                        // Registrar no Ledger (InvestorWallet -> BorrowerAccount)
                        if (_ledgerService != null)
                        {
                            await _ledgerService.AddTransactionAsync(
                                source: LedgerAccount.InvestorWallet,
                                destination: LedgerAccount.BorrowerAccount,
                                amount: allocatedShare,
                                type: LedgerTransactionType.LoanDisbursement,
                                referenceType: "Loan",
                                referenceId: loan.Id,
                                loanId: loan.Id,
                                userId: item.User.Id,
                                observacao: $"Desembolso do investidor {item.User.Email} para empréstimo {loan.Id}",
                                createdBy: "Sistema"
                            );
                        }
                    }

                    // Força salvamento na própria engine já que temos que controlar concorrência Otimista
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    Console.WriteLine($"[ALOCACAO-LOG] Alocação concluída com sucesso! {pool.Count} investidores alocados.");
                    return true;
                }
                catch (DbUpdateConcurrencyException)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[ALOCACAO-LOG] Conflito de Concorrência Detectado. Tentativa {attempt} de {maxRetries}.");
                    if (attempt == maxRetries)
                    {
                        Console.WriteLine($"[ALOCACAO-LOG] Falha ao alocar após múltiplas tentativas de concorrência.");
                        return false;
                    }
                    
                    // Atualiza a entidade do context para a versão do banco
                    foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.Entity is InvestorWallet))
                    {
                        await entry.ReloadAsync();
                    }
                    await Task.Delay(100); // Backoff leve
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"[ALOCACAO-LOG] Erro durante a alocação: {ex.Message}");
                    return false;
                }
            }

            return false;
        }
    }
}
