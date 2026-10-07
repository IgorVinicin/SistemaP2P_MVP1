using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Services;

public class PartialWithdrawalApprovalTests
{
    private static DbContextOptions<AppDbContext> Options() => new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private static async Task<(InvestorWallet Wallet, WithdrawalRequest Request)> Seed(AppDbContext db)
    {
        var user = new User { Id = Guid.NewGuid(), Role = "Investidor", Status = "Aprovado", Email = "investor@example.com" };
        var wallet = new InvestorWallet { Id = user.Id, SaldoDisponivel = 1000 };
        db.Users.Add(user); db.InvestorWallets.Add(wallet);
        db.DepositRequests.Add(new DepositRequest { Id = Guid.NewGuid(), InvestorId = user.Id, Valor = 1000, Status = "Completed", ProcessedAt = DateTime.UtcNow.AddHours(-100) });
        await db.SaveChangesAsync();
        var request = await new TreasuryService(db).RequestWithdrawalAsync(user.Id, 1000, null, user.Email, "EMAIL");
        return (wallet, request);
    }

    [Fact]
    public async Task PartialApprovalPreservesOriginalAndReleasesOnlyDifferenceOnce()
    {
        using var db = new AppDbContext(Options());
        var (wallet, request) = await Seed(db);
        var treasury = new TreasuryService(db);
        await treasury.ApproveWithdrawalAsync(request.Id, "Admin", 400);
        Assert.Equal(1000, request.ValorSolicitado);
        Assert.Equal(400, request.Valor);
        Assert.Equal("Approved", request.Status);
        Assert.Equal(600, wallet.SaldoDisponivel);
        Assert.Equal(400, wallet.SaldoBloqueado);
        Assert.Equal(0, wallet.TotalSacado);
        Assert.Single(db.InvestorTransactions.Where(t => t.Tipo == "ESTORNO_PARCIAL_SAQUE"));
        Assert.Equal(600, db.InvestorTransactions.Single(t => t.Tipo == "ESTORNO_PARCIAL_SAQUE").Valor);
        await Assert.ThrowsAsync<InvalidOperationException>(() => treasury.ApproveWithdrawalAsync(request.Id, "Admin", 300));
        Assert.Equal(600, wallet.SaldoDisponivel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(0.001)]
    public async Task InvalidApprovalNeverChangesReservation(decimal amount)
    {
        using var db = new AppDbContext(Options());
        var (wallet, request) = await Seed(db);
        await Assert.ThrowsAnyAsync<Exception>(() => new TreasuryService(db).ApproveWithdrawalAsync(request.Id, "Admin", amount));
        Assert.Equal("Pending", request.Status);
        Assert.Equal(1000, wallet.SaldoBloqueado);
        Assert.Equal(0, wallet.SaldoDisponivel);
        Assert.Empty(db.InvestorTransactions.Where(t => t.Tipo == "ESTORNO_PARCIAL_SAQUE"));
    }

    [Fact]
    public async Task FailedApprovedTransferRestoresOnlyTheRemaining400Once()
    {
        using var db = new AppDbContext(Options());
        var (wallet, request) = await Seed(db);
        var treasury = new TreasuryService(db);
        await treasury.ApproveWithdrawalAsync(request.Id, "Admin", 400);
        request.Status = "Failed";
        await db.SaveChangesAsync();
        await treasury.RejectWithdrawalAsync(request.Id, "Webhook", "Failure");
        await treasury.RejectWithdrawalAsync(request.Id, "Webhook", "Failure replay");
        Assert.Equal(1000, wallet.SaldoDisponivel);
        Assert.Equal(0, wallet.SaldoBloqueado);
        Assert.Equal(0, wallet.TotalSacado);
        Assert.Equal(400, db.InvestorTransactions.Single(t => t.Tipo == "ESTORNO_SAQUE").Valor);
        Assert.Equal(1000, request.ValorSolicitado);
    }

    [Fact]
    public async Task ConfirmedPartialTransferWithdrawsExactly400()
    {
        using var db = new AppDbContext(Options());
        var (wallet, request) = await Seed(db);
        var treasury = new TreasuryService(db);
        await treasury.ApproveWithdrawalAsync(request.Id, "Admin", 400);
        request.Status = "Processing"; request.GatewayReference = "transfer_400";
        await db.SaveChangesAsync();
        await treasury.ConfirmWithdrawalAsync(request.Id, "transfer_400", "Webhook");
        Assert.Equal(400, wallet.TotalSacado);
        Assert.Equal(0, wallet.SaldoBloqueado);
        Assert.Equal(600, wallet.SaldoDisponivel);
        Assert.Equal(400, db.LedgerTransactions.Single(t => t.TipoMovimentacao == LedgerTransactionType.InvestorWithdrawal).Valor);
    }

    [Fact]
    public async Task StaleParallelApprovalCannotReleaseTheDifferenceAgain()
    {
        var options = Options();
        using var first = new AppDbContext(options);
        var (wallet, request) = await Seed(first);
        using var second = new AppDbContext(options);
        await second.WithdrawalRequests.FindAsync(request.Id);
        await second.InvestorWallets.FindAsync(wallet.Id);
        await new TreasuryService(first).ApproveWithdrawalAsync(request.Id, "First", 400);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new TreasuryService(second).ApproveWithdrawalAsync(request.Id, "Second", 300));
        using var verification = new AppDbContext(options);
        Assert.Equal(600, (await verification.InvestorWallets.FindAsync(wallet.Id))!.SaldoDisponivel);
        Assert.Equal(400, (await verification.WithdrawalRequests.FindAsync(request.Id))!.Valor);
    }
}
