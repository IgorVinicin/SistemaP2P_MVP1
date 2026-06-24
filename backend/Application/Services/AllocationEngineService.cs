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

        public AllocationEngineService(AppDbContext context)
        {
            _context = context;
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
            // PASSO 1: Buscar TODOS os investidores com suas carteiras
            // Usar JOIN explícito via InvestorWallets para garantir que apenas
            // investidores com carteiras existentes sejam retornados
            // ====================================================================
            var allInvestidores = await _context.Users
                .Where(u => u.Role == "Investidor")
                .ToListAsync();

            Console.WriteLine($"[ALOCACAO-LOG] Total de usuários com Role=Investidor: {allInvestidores.Count}");
            foreach (var u in allInvestidores)
            {
                Console.WriteLine($"[ALOCACAO-LOG]   ID={u.Id} | Email={u.Email} | Status={u.Status}");
            }

            // Buscar carteiras separadamente para garantir o JOIN correto
            var investorIds = allInvestidores.Select(u => u.Id).ToList();
            var wallets = await _context.InvestorWallets
                .Where(w => investorIds.Contains(w.Id))
                .ToListAsync();

            Console.WriteLine($"[ALOCACAO-LOG] Carteiras encontradas: {wallets.Count}");
            foreach (var w in wallets)
            {
                Console.WriteLine($"[ALOCACAO-LOG]   WalletId={w.Id} | SaldoDisponivel={w.SaldoDisponivel:C2} | SaldoComprometido={w.SaldoComprometido:C2}");
            }

            // ====================================================================
            // PASSO 2: Filtrar investidores elegíveis (Status=Aprovado + tem carteira)
            // ====================================================================
            var eligibleInvestors = allInvestidores
                .Where(u => u.Status == "Aprovado" && wallets.Any(w => w.Id == u.Id))
                .Select(u => new
                {
                    User = u,
                    Wallet = wallets.First(w => w.Id == u.Id)
                })
                .ToList();

            Console.WriteLine($"[ALOCACAO-LOG] Investidores elegíveis (Status=Aprovado + tem carteira): {eligibleInvestors.Count}");

            if (!eligibleInvestors.Any())
            {
                Console.WriteLine($"[ALOCACAO-LOG] FALHOU: Nenhum investidor elegível encontrado.");
                Console.WriteLine($"[ALOCACAO-LOG]   - Total de investidores: {allInvestidores.Count}");
                Console.WriteLine($"[ALOCACAO-LOG]   - Investidores com Status=Aprovado: {allInvestidores.Count(u => u.Status == "Aprovado")}");
                Console.WriteLine($"[ALOCACAO-LOG]   - Investidores com carteira: {wallets.Count}");
                return false;
            }

            // ====================================================================
            // PASSO 3: Algoritmo iterativo de distribuição igualitária
            // ====================================================================
            var pool = eligibleInvestors.ToList();
            bool poolStable = false;
            var allocations = new Dictionary<Guid, decimal>();
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

                Console.WriteLine($"[ALOCACAO-LOG] Iteração {iterations}: Pool={M} | Excluídos por saldo insuficiente={tooLow.Count}");
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
                decimal saldoTotalPool = eligibleInvestors.Sum(p => p.Wallet.SaldoDisponivel);
                Console.WriteLine($"[ALOCACAO-LOG] FALHOU: Pool de investidores insuficiente.");
                Console.WriteLine($"[ALOCACAO-LOG]   Valor do empréstimo: {loanAmount:C2} | Saldo total no pool: {saldoTotalPool:C2}");
                return false;
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
                    Status = "Comprometido",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Investments.Add(investment);

                // Registrar transação do investidor
                var transaction = new InvestorTransaction
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
                _context.InvestorTransactions.Add(transaction);

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
            }

            Console.WriteLine($"[ALOCACAO-LOG] Alocação concluída com sucesso! {pool.Count} investidores alocados.");
            return true;
        }
    }
}
