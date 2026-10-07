using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Services;

public class WithdrawalAvailabilityTests
{
    private static AppDbContext Database() => new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static DepositRequest Deposit(Guid investor, decimal amount, DateTime? processed, string status = "Completed")
        => new() { Id = Guid.NewGuid(), InvestorId = investor, Valor = amount, Status = status, CreatedAt = DateTime.UtcNow.AddDays(-30), ProcessedAt = processed };

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 100)]
    [InlineData(1, 100)]
    public async Task ReleasesAtExactly72HoursFromConfirmation(int ticksAfterRelease, decimal expected)
    {
        using var db = Database();
        var confirmed = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 100 };
        db.DepositRequests.Add(Deposit(wallet.Id, 100, confirmed));
        await db.SaveChangesAsync();
        var result = await new WithdrawalAvailabilityService(db).GetAsync(wallet, confirmed.AddHours(72).AddTicks(ticksAfterRelease));
        Assert.Equal(expected, result.SaldoSacavel);
        Assert.Equal(100 - expected, result.SaldoEmCarencia);
        Assert.Equal(ticksAfterRelease < 0 ? confirmed.AddHours(72) : null, result.ProximaLiberacaoSaque);
    }

    [Fact]
    public async Task NewDepositsDoNotRelockOldMaturedFunds()
    {
        using var db = Database();
        var now = DateTime.UtcNow;
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 1000 };
        db.DepositRequests.AddRange(Deposit(wallet.Id, 500, now.AddHours(-100)), Deposit(wallet.Id, 300, now.AddHours(-24)), Deposit(wallet.Id, 200, now.AddHours(-12)));
        await db.SaveChangesAsync();
        var service = new WithdrawalAvailabilityService(db);
        var current = await service.GetAsync(wallet, now);
        Assert.Equal(500, current.SaldoSacavel);
        Assert.Equal(500, current.SaldoEmCarencia);
        Assert.Equal(now.AddHours(48), current.ProximaLiberacaoSaque);
        Assert.Equal(800, (await service.GetAsync(wallet, now.AddHours(48))).SaldoSacavel);
        Assert.Equal(1000, (await service.GetAsync(wallet, now.AddHours(60))).SaldoSacavel);
    }

    [Fact]
    public async Task InvestedCommittedAndReservedFundsAreNeverAddedToWithdrawalBalance()
    {
        using var db = Database();
        var now = DateTime.UtcNow;
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 70, SaldoInvestido = 500, SaldoComprometido = 200, SaldoBloqueado = 100 };
        db.DepositRequests.Add(Deposit(wallet.Id, 100, now.AddHours(-1)));
        await db.SaveChangesAsync();
        var result = await new WithdrawalAvailabilityService(db).GetAsync(wallet, now);
        Assert.Equal(0, result.SaldoSacavel);
        Assert.Equal(70, result.SaldoEmCarencia);
        Assert.Equal(100, wallet.SaldoBloqueado);
    }

    [Fact]
    public async Task CreationTimeDoesNotReleaseDepositAndUnknownConfirmationRemainsHeld()
    {
        using var db = Database();
        var now = DateTime.UtcNow;
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 300 };
        db.DepositRequests.AddRange(Deposit(wallet.Id, 100, now), Deposit(wallet.Id, 100, null), Deposit(wallet.Id, 999, now, "Pending"));
        await db.SaveChangesAsync();
        var result = await new WithdrawalAvailabilityService(db).GetAsync(wallet, now.AddHours(72));
        Assert.Equal(200, result.SaldoSacavel);
        Assert.Equal(100, result.SaldoEmCarencia);
        Assert.Null(result.ProximaLiberacaoSaque);
    }

    [Fact]
    public async Task TreasuryRejectsHeldMoneyAndReservesOnlyMaturedAvailableBalance()
    {
        using var db = Database();
        var user = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado", Email = "investor@example.com" };
        var wallet = new InvestorWallet { Id = user.Id, SaldoDisponivel = 500 };
        db.Users.Add(user); db.InvestorWallets.Add(wallet);
        db.DepositRequests.AddRange(Deposit(user.Id, 200, DateTime.UtcNow.AddHours(-1)), Deposit(user.Id, 300, DateTime.UtcNow.AddHours(-100)));
        await db.SaveChangesAsync();
        var treasury = new TreasuryService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() => treasury.RequestWithdrawalAsync(user.Id, 301, null, user.Email, "EMAIL"));
        Assert.Equal(500, wallet.SaldoDisponivel);
        Assert.Equal(0, wallet.SaldoBloqueado);
        Assert.Empty(db.WithdrawalRequests);
        await treasury.RequestWithdrawalAsync(user.Id, 300, null, user.Email, "EMAIL");
        Assert.Equal(200, wallet.SaldoDisponivel);
        Assert.Equal(300, wallet.SaldoBloqueado);
        Assert.Equal(0, (await new WithdrawalAvailabilityService(db).GetAsync(wallet)).SaldoSacavel);
    }

    [Fact]
    public async Task DailyYieldCannotBeWithdrawnBeforeAnyConfirmedDepositMatures()
    {
        using var db = Database();
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 101, Rendimentos = 1 };
        var confirmed = DateTime.UtcNow;
        db.DepositRequests.Add(Deposit(wallet.Id, 100, confirmed));
        await db.SaveChangesAsync();
        var service = new WithdrawalAvailabilityService(db);
        Assert.Equal(0, (await service.GetAsync(wallet, confirmed.AddHours(71))).SaldoSacavel);
        Assert.Equal(101, (await service.GetAsync(wallet, confirmed.AddHours(72))).SaldoSacavel);
    }

    [Fact]
    public async Task BalanceWithoutConfirmedDepositRequiresReconciliationBeforeWithdrawal()
    {
        using var db = Database();
        var result = await new WithdrawalAvailabilityService(db).GetAsync(new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 500 });
        Assert.Equal(0, result.SaldoSacavel);
        Assert.Equal(500, result.SaldoEmCarencia);
        Assert.Null(result.ProximaLiberacaoSaque);
    }
}
