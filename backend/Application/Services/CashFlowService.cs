using backend.Application.DTOs;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace backend.Application.Services
{
    public class CashFlowService
    {
        private readonly AppDbContext _context;

        public CashFlowService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CashFlowSnapshot> GetSummaryAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var query = _context.LedgerTransactions.AsNoTracking().AsQueryable();

            if (startDate.HasValue)
                query = query.Where(t => t.TransactionDate >= startDate.Value.ToUniversalTime());
            if (endDate.HasValue)
            {
                var end = endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate <= end);
            }

            // Realizamos agrupamento no banco de dados para evitar carregamento em memória (Escalabilidade)
            
            // TotalEntradas = Tudo que tem Destino como PlatformCash
            var totalEntradas = await query
                .Where(t => t.ContaDestino == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // TotalSaidas = Tudo que tem Origem como PlatformCash
            var totalSaidas = await query
                .Where(t => t.ContaOrigem == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // TotalRecebidoParcelas = Onde Tipo = InstallmentPayment
            var totalRecebidoParcelas = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InstallmentPayment && t.ContaDestino == LedgerAccount.PlatformCash)
                .SumAsync(t => t.Valor);

            // ReceitaPlataforma = Entradas em PlatformRevenue (Origem: PlatformCash)
            var receitaPlataforma = await query
                .Where(t => t.ContaDestino == LedgerAccount.PlatformRevenue)
                .SumAsync(t => t.Valor);

            // TotalDistribuidoInvestidores = Saídas de PlatformCash para InvestorWallet 
            // do tipo RevenueDistribution
            var totalDistribuidoInvestidores = await query
                .Where(t => t.ContaDestino == LedgerAccount.InvestorWallet && t.ContaOrigem == LedgerAccount.PlatformCash && t.TipoMovimentacao == LedgerTransactionType.RevenueDistribution)
                .SumAsync(t => t.Valor);

            // TotalCobertoProtectionFund = Saídas do ProtectionFund para PlatformCash
            var totalCobertoProtectionFund = await query
                .Where(t => t.ContaOrigem == LedgerAccount.ProtectionFund && t.TipoMovimentacao == LedgerTransactionType.ProtectionFundDebit)
                .SumAsync(t => t.Valor);

            // TotalEmprestado = Desembolsos de Investidor para Tomador
            var totalEmprestado = await query
                .Where(t => t.ContaDestino == LedgerAccount.BorrowerAccount && t.TipoMovimentacao == LedgerTransactionType.LoanDisbursement)
                .SumAsync(t => t.Valor);

            var totalDepositosExternos = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InvestorDeposit)
                .SumAsync(t => t.Valor);

            var totalSaquesExternos = await query
                .Where(t => t.TipoMovimentacao == LedgerTransactionType.InvestorWithdrawal)
                .SumAsync(t => t.Valor);

            return new CashFlowSnapshot
            {
                SnapshotDate = endDate ?? DateTime.UtcNow,
                TotalEntradas = totalEntradas,
                TotalSaidas = totalSaidas,
                SaldoOperacional = (totalEntradas + totalDepositosExternos) - (totalSaidas + totalSaquesExternos) + receitaPlataforma,
                ReceitaPlataforma = receitaPlataforma,
                TotalDistribuidoInvestidores = totalDistribuidoInvestidores,
                TotalCobertoProtectionFund = totalCobertoProtectionFund,
                TotalEmprestado = totalEmprestado,
                TotalRecebidoParcelas = totalRecebidoParcelas,
                TotalDepositosExternos = totalDepositosExternos,
                TotalSaquesExternos = totalSaquesExternos,
                CreatedAt = DateTime.UtcNow
            };
        }

        public async Task<CashFlowSnapshot> GenerateSnapshotAsync(DateTime targetDate)
        {
            var date = targetDate.Date.ToUniversalTime();
            var existingSnapshot = await _context.CashFlowSnapshots
                .FirstOrDefaultAsync(s => s.SnapshotDate.Date == date.Date);

            if (existingSnapshot != null)
                throw new InvalidOperationException($"Já existe um snapshot para a data {date.ToShortDateString()}");

            var summary = await GetSummaryAsync(date, date);
            
            // Asseguramos a data exata sem a fração de tempo
            summary.Id = Guid.NewGuid();
            summary.SnapshotDate = date;

            _context.CashFlowSnapshots.Add(summary);
            await _context.SaveChangesAsync();

            return summary;
        }

        public async Task<PagedResultDto<CashFlowSnapshot>> GetSnapshotsAsync(int page = 1, int pageSize = 20)
        {
            var query = _context.CashFlowSnapshots.AsNoTracking();

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(s => s.SnapshotDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<CashFlowSnapshot>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            };
        }
    }
}
