using backend.Application.Services;
using backend.Domain.Entities;
using backend.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace backend.Tests.Services;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("P2P_RUN_POSTGRES_TESTS") != "1")
            Skip = "Defina P2P_RUN_POSTGRES_TESTS=1 para executar em um banco temporário isolado.";
    }
}

public class PostgresIntegrityTests
{
    [PostgresFact]
    public async Task DepositFailureAfterFirstSave_RollsBackBalanceAndLedger()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var (user, deposit) = await SeedDeposit(database);
        await using (var failing = database.Context(new FailSecondSave()))
            await Assert.ThrowsAsync<InjectedFailure>(() => new TreasuryService(failing).ConfirmDepositAsync(deposit, "pay_test", "test"));
        await using var verification = database.Context();
        Assert.Equal(0, (await verification.InvestorWallets.FindAsync(user))!.SaldoDisponivel);
        Assert.Equal("Processing", (await verification.DepositRequests.FindAsync(deposit))!.Status);
        Assert.Empty(await verification.LedgerTransactions.ToListAsync());
        Assert.Empty(await verification.InvestorTransactions.ToListAsync());
    }

    [PostgresFact]
    public async Task ConcurrentDepositConfirmation_CreditsExactlyOnce()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var (user, deposit) = await SeedDeposit(database);
        async Task Confirm()
        {
            await using var context = database.Context();
            try { await new TreasuryService(context).ConfirmDepositAsync(deposit, "pay_test", "test"); }
            catch (Exception ex) when (IsSerializationFailure(ex)) { }
            catch (DbUpdateConcurrencyException) { }
        }
        await Task.WhenAll(Confirm(), Confirm());
        // A gateway delivery retries the whole operation with a fresh request context.
        // Once the winning transaction commits, both re-deliveries must be harmless.
        await using (var retry = database.Context())
            Assert.True(await new TreasuryService(retry).ConfirmDepositAsync(deposit, "pay_test", "delivery-retry"));
        await using (var replay = database.Context())
            Assert.True(await new TreasuryService(replay).ConfirmDepositAsync(deposit, "pay_test", "delivery-replay"));
        await using var verification = database.Context();
        Assert.Equal(100, (await verification.InvestorWallets.FindAsync(user))!.SaldoDisponivel);
        Assert.Equal(100, (await verification.InvestorWallets.FindAsync(user))!.TotalAportado);
        Assert.Equal("Completed", (await verification.DepositRequests.FindAsync(deposit))!.Status);
        Assert.Equal(1, await verification.InvestorTransactions.CountAsync());
        Assert.Equal(2, await verification.LedgerTransactions.CountAsync());
    }

    private static bool IsSerializationFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
            if (current is PostgresException postgres && postgres.SqlState == PostgresErrorCodes.SerializationFailure)
                return true;
        return false;
    }

    [Fact]
    public void SerializationDetectionAcceptsProviderWrappingAndRejectsOtherFailures()
    {
        var serialization = new PostgresException("serialization failure", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure);
        Assert.True(IsSerializationFailure(new InvalidOperationException("provider wrapper", new DbUpdateException("save wrapper", serialization))));
        var unrelated = new PostgresException("constraint failure", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        Assert.False(IsSerializationFailure(new InvalidOperationException("provider wrapper", new DbUpdateException("save wrapper", unrelated))));
        Assert.False(IsSerializationFailure(new InvalidOperationException("unrelated application failure")));
    }

    [PostgresFact]
    public async Task StaleWalletVersion_IsRejectedByPostgres()
    {
        await using var database = await TemporaryDatabase.CreateAsync();
        var (user, _) = await SeedDeposit(database);
        await using var first = database.Context();
        await using var second = database.Context();
        var firstWallet = (await first.InvestorWallets.FindAsync(user))!;
        var secondWallet = (await second.InvestorWallets.FindAsync(user))!;
        firstWallet.SaldoDisponivel = 10;
        await first.SaveChangesAsync();
        secondWallet.SaldoDisponivel = 20;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    private static async Task<(Guid User, Guid Deposit)> SeedDeposit(TemporaryDatabase database)
    {
        await using var context = database.Context();
        var user = new User { Id = Guid.NewGuid(), Email = "postgres-test", Role = "Investidor", Status = "Aprovado" };
        context.Users.Add(user);
        context.InvestorWallets.Add(new InvestorWallet { Id = user.Id });
        var deposit = new DepositRequest { Id = Guid.NewGuid(), InvestorId = user.Id, Valor = 100, Status = "Processing", GatewayReference = "pay_test" };
        context.DepositRequests.Add(deposit);
        await context.SaveChangesAsync();
        return (user.Id, deposit.Id);
    }

    private sealed class InjectedFailure : Exception { }
    private sealed class FailSecondSave : SaveChangesInterceptor
    {
        private int _calls;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (++_calls == 2) throw new InjectedFailure();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _adminConnection;
        private readonly string _name;
        private readonly string _connection;
        private TemporaryDatabase(string adminConnection, string name, string connection)
            => (_adminConnection, _name, _connection) = (adminConnection, name, connection);

        public AppDbContext Context(params IInterceptor[] interceptors) => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connection).AddInterceptors(interceptors).Options);

        public static async Task<TemporaryDatabase> CreateAsync()
        {
            var connection = Environment.GetEnvironmentVariable("P2P_TEST_ADMIN_CONNECTION");
            if (string.IsNullOrWhiteSpace(connection))
                throw new InvalidOperationException("Configure P2P_TEST_ADMIN_CONNECTION apontando a um servidor PostgreSQL dedicado a testes.");
            var builder = new NpgsqlConnectionStringBuilder(connection) { Timeout = 10, Pooling = false };
            var admin = builder.ConnectionString;
            var name = "p2p_test_" + Guid.NewGuid().ToString("N");
            await using (var conn = new NpgsqlConnection(admin))
            {
                await conn.OpenAsync();
                await using var command = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", conn);
                await command.ExecuteNonQueryAsync();
            }
            builder.Database = name;
            var database = new TemporaryDatabase(admin, name, builder.ConnectionString);
            try
            {
                await using var context = database.Context();
                await context.Database.EnsureCreatedAsync();
                return database;
            }
            catch { await database.DisposeAsync(); throw; }
        }

        public async ValueTask DisposeAsync()
        {
            // O nome é criado internamente; a limpeza nunca usa o banco original.
            if (!_name.StartsWith("p2p_test_") || _name.Length != 41) throw new InvalidOperationException("Nome de teste inválido.");
            await using var connection = new NpgsqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand($"DROP DATABASE \"{_name}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }
}
