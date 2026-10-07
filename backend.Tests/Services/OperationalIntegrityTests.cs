using System.Security.Claims;
using System.Text;
using backend.API.Middleware;
using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Services;

public class OperationalIntegrityTests
{
    private static AppDbContext CreateDatabase() => new(new DbContextOptionsBuilder<AppDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static DefaultHttpContext Request(string user, string body = "{\"valor\":100}")
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user) }, "test"));
        context.Request.Method = "POST";
        context.Request.Path = "/api/investor/treasury/deposit-pix";
        context.Request.ContentType = "application/json";
        context.Request.Headers["X-Idempotency-Key"] = "same-key";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));
        context.Response.Body = new MemoryStream();
        return context;
    }

    [Fact]
    public async Task Idempotency_ReplaysOnlySameUserAndPayload()
    {
        using var db = CreateDatabase();
        var calls = 0;
        var middleware = new IdempotencyMiddleware(async context =>
        {
            calls++;
            await context.Response.WriteAsJsonAsync(new { calls });
        });
        await middleware.InvokeAsync(Request("user-one"), db);
        await middleware.InvokeAsync(Request("user-one"), db);
        Assert.Equal(1, calls);
        var changed = Request("user-one", "{\"valor\":200}");
        await middleware.InvokeAsync(changed, db);
        Assert.Equal(409, changed.Response.StatusCode);
        Assert.Equal(1, calls);
        await middleware.InvokeAsync(Request("user-two"), db);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Idempotency_RestoresResponseStreamAfterFailure()
    {
        using var db = CreateDatabase();
        var context = Request("user");
        var stream = context.Response.Body;
        var middleware = new IdempotencyMiddleware(_ => throw new InvalidOperationException("failure"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context, db));
        Assert.Same(stream, context.Response.Body);
        Assert.Empty(db.IdempotencyRecords);
    }

    [Fact]
    public async Task CashFlow_DoesNotCountInternalWalletLegsAsExternalMoney()
    {
        using var db = CreateDatabase();
        var date = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
        void Add(LedgerAccount from, LedgerAccount to, decimal value, LedgerTransactionType type)
            => db.LedgerTransactions.Add(new LedgerTransaction
            {
                Id = Guid.NewGuid(), TransactionDate = date, CreatedAt = date,
                ContaOrigem = from, ContaDestino = to, Valor = value, TipoMovimentacao = type
            });
        Add(LedgerAccount.ExternalGateway, LedgerAccount.PlatformCash, 100, LedgerTransactionType.InvestorDeposit);
        Add(LedgerAccount.PlatformCash, LedgerAccount.InvestorWallet, 100, LedgerTransactionType.InvestorDeposit);
        Add(LedgerAccount.InvestorWallet, LedgerAccount.PlatformCash, 20, LedgerTransactionType.InvestorWithdrawal);
        Add(LedgerAccount.PlatformCash, LedgerAccount.ExternalGateway, 20, LedgerTransactionType.InvestorWithdrawal);
        await db.SaveChangesAsync();
        var summary = await new CashFlowService(db).GetSummaryAsync();
        Assert.Equal(100, summary.TotalDepositosExternos);
        Assert.Equal(20, summary.TotalSaquesExternos);
        Assert.Equal(100, summary.TotalEntradas);
        Assert.Equal(20, summary.TotalSaidas);
        Assert.Equal(80, summary.SaldoOperacional);
    }

    [Fact]
    public async Task CashFlow_ClosingDoesNotIncludeNextDay()
    {
        using var db = CreateDatabase();
        var range = CashFlowService.GetBrasiliaDayRangeUtc(new DateTime(2026, 10, 7));
        foreach (var date in new[] { range.StartUtc, range.EndUtc.AddTicks(1) })
            db.LedgerTransactions.Add(new LedgerTransaction
            {
                Id = Guid.NewGuid(), TransactionDate = date, ContaOrigem = LedgerAccount.ExternalGateway,
                ContaDestino = LedgerAccount.PlatformCash, Valor = 100, TipoMovimentacao = LedgerTransactionType.InvestorDeposit
            });
        await db.SaveChangesAsync();
        var snapshot = await new CashFlowService(db).GenerateSnapshotAsync(new DateTime(2026, 10, 7));
        Assert.Equal(new DateTime(2026, 10, 7), snapshot.SnapshotDate.Date);
        Assert.Equal(100, snapshot.TotalEntradas);
    }

    [Fact]
    public async Task Yield_DailyEstimateDoesNotChangeBalanceOrReduceMonthlyCredit()
    {
        using var db = CreateDatabase();
        var user = new User { Id = Guid.NewGuid(), Email = "investor", Role = "Investidor", Status = "Aprovado" };
        db.Users.Add(user);
        db.InvestorWallets.Add(new InvestorWallet { Id = user.Id, SaldoDisponivel = 30000, RentabilidadeSaldoDisponivel = 1 });
        db.CreditConfigs.Add(new CreditConfig { Id = Guid.NewGuid(), TaxaRendimentoPool = 2 });
        await db.SaveChangesAsync();
        var service = new InvestorYieldService(db);
        var preview = await service.GetDailyYieldPreviewAsync(user.Id);
        Assert.Equal(10m, preview);
        Assert.Empty(db.InvestorTransactions);
        var monthlyPreview = await service.GetMonthlyYieldPreviewAsync(DateTime.UtcNow);
        var monthly = await service.ProcessMonthlyYieldAsync();
        Assert.Equal(monthlyPreview.TotalRendimentoEstimado, monthly.TotalYield);
        Assert.Equal(300m, monthly.TotalYield);
        Assert.Equal(30300m, (await db.InvestorWallets.FindAsync(user.Id))!.SaldoDisponivel);
        Assert.True(await service.GetDailyYieldPreviewAsync(user.Id) > 0);
    }

    [Fact]
    public async Task Yield_ZeroMonthlyPaymentStillClosesMonthWithoutHidingDailyEstimate()
    {
        using var db = CreateDatabase();
        var user = new User { Id = Guid.NewGuid(), Email = "investor", Role = "Investidor", Status = "Aprovado" };
        db.Users.Add(user);
        db.InvestorWallets.Add(new InvestorWallet { Id = user.Id, SaldoDisponivel = 1000, RentabilidadeSaldoDisponivel = 1 });
        db.CreditConfigs.Add(new CreditConfig { Id = Guid.NewGuid(), TaxaRendimentoPool = 2 });
        db.InvestorTransactions.Add(new InvestorTransaction
        {
            Id = Guid.NewGuid(), InvestorId = user.Id, Tipo = "RENDIMENTO_DIARIO", Valor = 10, DataMovimentacao = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var service = new InvestorYieldService(db);
        Assert.Equal(0, (await service.ProcessMonthlyYieldAsync()).TotalYield);
        Assert.True((await service.GetMonthlyYieldPreviewAsync(DateTime.UtcNow)).JaDistribuidoEsteMes);
        Assert.True(await service.GetDailyYieldPreviewAsync(user.Id) > 0);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ProcessMonthlyYieldAsync());
        Assert.Equal(1000, (await db.InvestorWallets.FindAsync(user.Id))!.SaldoDisponivel);
    }

    [Fact]
    public async Task Yield_EstimateIncludesRemuneratedCapitalButDoesNotCreditOrTouchReservedWithdrawal()
    {
        using var db = CreateDatabase();
        var wallet = new InvestorWallet { Id = Guid.NewGuid(), SaldoDisponivel = 3000,
            SaldoComprometido = 3000, SaldoInvestido = 6000, SaldoBloqueado = 10000,
            RentabilidadeSaldoDisponivel = 1, RentabilidadeSaldoComprometido = 3 };
        db.InvestorWallets.Add(wallet);
        await db.SaveChangesAsync();
        var estimate = await new InvestorYieldService(db).GetDailyYieldPreviewAsync(wallet.Id);
        Assert.Equal(10m, estimate); // (30 available + 270 allocated monthly) / 30.
        Assert.Equal(3000m, wallet.SaldoDisponivel);
        Assert.Equal(10000m, wallet.SaldoBloqueado);
        Assert.Empty(db.InvestorTransactions);
        Assert.Empty(db.LedgerTransactions);
    }

    [Fact]
    public async Task Yield_AdvancesReduceTotalEntitlementAfterMoneyMovesIntoCommittedBalance()
    {
        using var db = CreateDatabase();
        var user = new User { Id = Guid.NewGuid(), Email = "investor", Role = "Investidor", Status = "Aprovado" };
        db.Users.Add(user);
        db.InvestorWallets.Add(new InvestorWallet { Id = user.Id, SaldoDisponivel = 100, SaldoComprometido = 1000, RentabilidadeSaldoDisponivel = 1, Rentabilidade = 3 });
        db.CreditConfigs.Add(new CreditConfig { Id = Guid.NewGuid(), TaxaRendimentoPool = 2 });
        db.InvestorTransactions.Add(new InvestorTransaction { Id = Guid.NewGuid(), InvestorId = user.Id, Tipo = "RENDIMENTO_DIARIO", Valor = 10, DataMovimentacao = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new InvestorYieldService(db);
        var preview = await service.GetMonthlyYieldPreviewAsync(DateTime.UtcNow);
        Assert.Equal(21, preview.TotalRendimentoEstimado); // 1 available + 30 committed - 10 advanced.
        Assert.Equal(21, (await service.ProcessMonthlyYieldAsync()).TotalYield);
    }
}
