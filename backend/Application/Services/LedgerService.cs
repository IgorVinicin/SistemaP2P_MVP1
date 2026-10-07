using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Application.DTOs;

namespace backend.Application.Services
{
    public class LedgerService
    {
        private readonly AppDbContext _context;

        public LedgerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<LedgerTransaction> AddTransactionAsync(
            LedgerAccount source,
            LedgerAccount destination,
            decimal amount,
            LedgerTransactionType type,
            string? referenceType = null,
            Guid? referenceId = null,
            Guid? loanId = null,
            Guid? installmentId = null,
            Guid? userId = null,
            string? observacao = null,
            string createdBy = "Sistema")
        {
            if (amount <= 0)
                throw new InvalidOperationException("O valor da transação deve ser maior que zero.");

            if (source == destination)
                throw new InvalidOperationException("Conta de origem não pode ser igual à conta de destino.");

            var transaction = new LedgerTransaction
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                TransactionDate = DateTime.UtcNow,
                TipoMovimentacao = type,
                ContaOrigem = source,
                ContaDestino = destination,
                Valor = amount,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                LoanId = loanId,
                InstallmentId = installmentId,
                UserId = userId,
                Observacao = observacao,
                CreatedBy = createdBy
            };

            _context.LedgerTransactions.Add(transaction);
            
            // O SaveChangesAsync pode ser chamado aqui ou pelo serviço chamador dependendo do contexto transacional
            await _context.SaveChangesAsync();
            return transaction;
        }

        public async Task<PagedResultDto<LedgerTransaction>> GetHistoryAsync(
            int page = 1,
            int pageSize = 20,
            string? period = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            LedgerTransactionType? type = null,
            LedgerAccount? account = null,
            Guid? loanId = null,
            Guid? userId = null)
        {
            var query = _context.LedgerTransactions.AsNoTracking().AsQueryable();

            var now = DateTime.UtcNow;
            var today = now.Date;

            if (!string.IsNullOrEmpty(period))
            {
                switch (period.ToLower())
                {
                    case "hoje":
                        query = query.Where(t => t.TransactionDate >= today);
                        break;
                    case "7dias":
                        query = query.Where(t => t.TransactionDate >= today.AddDays(-7));
                        break;
                    case "30dias":
                        query = query.Where(t => t.TransactionDate >= today.AddDays(-30));
                        break;
                    case "mesatual":
                        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(t => t.TransactionDate >= startOfMonth);
                        break;
                }
            }
            else if (startDate.HasValue && endDate.HasValue)
            {
                var start = startDate.Value.ToUniversalTime();
                var end = endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1);
                query = query.Where(t => t.TransactionDate >= start && t.TransactionDate <= end);
            }

            if (type.HasValue)
                query = query.Where(t => t.TipoMovimentacao == type.Value);

            if (account.HasValue)
                query = query.Where(t => t.ContaOrigem == account.Value || t.ContaDestino == account.Value);

            if (loanId.HasValue)
                query = query.Where(t => t.LoanId == loanId.Value);

            if (userId.HasValue)
                query = query.Where(t => t.UserId == userId.Value);

            var totalCount = await query.CountAsync();
            page = Math.Max(1, page);
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(t => t.TransactionDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<LedgerTransaction>
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
