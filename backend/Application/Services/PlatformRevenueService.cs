using System;
using System.Linq;
using System.Threading.Tasks;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using backend.Application.DTOs;

namespace backend.Application.Services
{
    public class PlatformRevenueService
    {
        private readonly AppDbContext _context;

        public PlatformRevenueService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PlatformRevenue?> RegisterRevenueAsync(
            PlatformRevenueType tipo, 
            decimal valor, 
            Guid? loanId = null, 
            Guid? installmentId = null, 
            Guid? userId = null, 
            string? observacao = null, 
            string createdBy = "Sistema")
        {
            if (valor <= 0)
                return null;

            var revenue = new PlatformRevenue
            {
                Id = Guid.NewGuid(),
                DataCriacao = DateTime.UtcNow,
                TipoReceita = tipo,
                Valor = valor,
                LoanId = loanId,
                InstallmentId = installmentId,
                UserId = userId,
                Observacao = observacao,
                CreatedBy = createdBy,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlatformRevenues.Add(revenue);
            await _context.SaveChangesAsync();
            return revenue;
        }

        public async Task<object> GetDashboardMetricsAsync()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var startOfWeek = today.AddDays(-(int)now.DayOfWeek);
            var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

            var revenues = _context.PlatformRevenues.AsNoTracking();

            var totalRevenue = await revenues.SumAsync(r => r.Valor);
            var todayRevenue = await revenues.Where(r => r.DataCriacao >= today).SumAsync(r => r.Valor);
            var weekRevenue = await revenues.Where(r => r.DataCriacao >= startOfWeek).SumAsync(r => r.Valor);
            var monthRevenue = await revenues.Where(r => r.DataCriacao >= startOfMonth).SumAsync(r => r.Valor);
            var totalEvents = await revenues.CountAsync();
            var lastRevenue = await revenues.OrderByDescending(r => r.DataCriacao).FirstOrDefaultAsync();

            return new
            {
                ReceitaHoje = todayRevenue,
                ReceitaSemana = weekRevenue,
                ReceitaMes = monthRevenue,
                ReceitaTotal = totalRevenue,
                QuantidadeEventos = totalEvents,
                UltimaReceita = lastRevenue
            };
        }

        public async Task<PagedResultDto<PlatformRevenue>> GetRevenuesAsync(
            int page = 1, 
            int pageSize = 20, 
            string? period = null, 
            DateTime? startDate = null, 
            DateTime? endDate = null)
        {
            var query = _context.PlatformRevenues.AsNoTracking().AsQueryable();

            var now = DateTime.UtcNow;
            var today = now.Date;

            if (!string.IsNullOrEmpty(period))
            {
                switch (period.ToLower())
                {
                    case "hoje":
                        query = query.Where(r => r.DataCriacao >= today);
                        break;
                    case "7dias":
                        var last7Days = today.AddDays(-7);
                        query = query.Where(r => r.DataCriacao >= last7Days);
                        break;
                    case "30dias":
                        var last30Days = today.AddDays(-30);
                        query = query.Where(r => r.DataCriacao >= last30Days);
                        break;
                    case "mesatual":
                        var startOfMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                        query = query.Where(r => r.DataCriacao >= startOfMonth);
                        break;
                }
            }
            else if (startDate.HasValue && endDate.HasValue)
            {
                var start = startDate.Value.ToUniversalTime();
                var end = endDate.Value.ToUniversalTime().AddDays(1).AddTicks(-1);
                query = query.Where(r => r.DataCriacao >= start && r.DataCriacao <= end);
            }

            var totalCount = await query.CountAsync();
            page = Math.Max(1, page);
            pageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var items = await query
                .OrderByDescending(r => r.DataCriacao)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return new PagedResultDto<PlatformRevenue>
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
