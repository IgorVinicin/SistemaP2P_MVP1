using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Application.Services;

public record WithdrawalAvailability(decimal SaldoSacavel, decimal SaldoEmCarencia, DateTime? ProximaLiberacaoSaque);

/// <summary>Confirmed deposits become withdrawable after 72 hours; reserved or invested funds remain outside available balance.</summary>
public class WithdrawalAvailabilityService(AppDbContext context)
{
    public async Task<WithdrawalAvailability> GetAsync(InvestorWallet wallet, DateTime? referenceUtc = null)
    {
        var now = referenceUtc ?? DateTime.UtcNow;
        var cutoff = now.AddHours(-72);
        // Missing confirmation time is never replaced by request creation time.
        var deposits = await context.DepositRequests.AsNoTracking()
            .Where(d => d.InvestorId == wallet.Id && d.Status == "Completed" &&
                (!d.ProcessedAt.HasValue || d.ProcessedAt.Value > cutoff))
            .Select(d => new { d.Valor, d.ProcessedAt }).ToListAsync();
        decimal available = Math.Max(0, wallet.SaldoDisponivel);
        decimal held = Math.Min(available, deposits.Where(d => d.Valor > 0).Sum(d => d.Valor));
        var next = deposits.Where(d => d.ProcessedAt.HasValue)
            .Select(d => d.ProcessedAt!.Value.AddHours(72)).OrderBy(d => d).Cast<DateTime?>().FirstOrDefault();
        bool hasMaturedDeposit = await context.DepositRequests.AsNoTracking().AnyAsync(d =>
            d.InvestorId == wallet.Id && d.Status == "Completed" && d.Valor > 0 &&
            d.ProcessedAt.HasValue && d.ProcessedAt.Value <= cutoff);
        if (!hasMaturedDeposit) return new WithdrawalAvailability(0, available, next);
        return new WithdrawalAvailability(available - held, held, next);
    }
}
