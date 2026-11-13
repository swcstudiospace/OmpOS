using Autonogrammer.Api.Audit;
using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.IntegrationTests.DataPlane;
using Autonogrammer.Sessions.Postgres;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>
/// The ledger outbox fence on a run-owned migrated database. The outbox marks a row sent only when its tenant hash resolves through
/// <c>account_tenant_bindings</c> to an account whose admission is open. A row whose tenant has no binding, or whose account erasure has removed the binding,
/// is refused and stays unsent. The bound active-account control proves the same path marks the row sent when the fence allows it.
/// </summary>
public sealed class LedgerOutboxFenceTests
{
    private const string MigratorKey = "Sessions:Postgres:MigratorConnectionString";
    private const string Account = "acct-outbox-fence";
    private const string TenantHash = "tenant-hash-outbox-fence";
    private const string SentAtSql = "SELECT sent_at FROM ledger_outbox WHERE id = @id";

    /// <summary>A row whose tenant has no binding is refused by MarkSent, and the row stays unsent.</summary>
    [Fact]
    public async Task UnboundTenant_MarkSentIsRefused_AndTheRowStaysUnsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using (dataSource.ConfigureAwait(false))
        {
            await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Account], ct).ConfigureAwait(true);
            var store = new PostgresLedgerOutboxStore(dataSource);
            var id = await EnqueueAndClaimAsync(store, ct).ConfigureAwait(true);

            _ = await Should.ThrowAsync<AccountErasingException>(() => store.MarkSentAsync(id, ct).AsTask()).ConfigureAwait(true);
            (await SentAtAsync(dataSource, id, ct).ConfigureAwait(true)).ShouldBeNull("the refused mark leaves the row unsent");
        }
    }

    /// <summary>Control: a row whose tenant is bound to an active account is marked sent, and the store releases it from its pending depth.</summary>
    [Fact]
    public async Task BoundActiveAccount_MarkSentSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using (dataSource.ConfigureAwait(false))
        {
            await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Account], ct).ConfigureAwait(true);
            await BindAsync(dataSource, ct).ConfigureAwait(true);
            var store = new PostgresLedgerOutboxStore(dataSource);
            var id = await EnqueueAndClaimAsync(store, ct).ConfigureAwait(true);

            await store.MarkSentAsync(id, ct).ConfigureAwait(true);

            (await SentAtAsync(dataSource, id, ct).ConfigureAwait(true)).ShouldNotBeNull("the allowed mark stamps sent_at");
            store.Depth.ShouldBe(0L);
        }
    }

    /// <summary>After the account is erased, the outbox refuses to mark its already-claimed row sent, and the row stays unsent.</summary>
    [Fact]
    public async Task AfterErasure_MarkSentIsRefused_AndTheRowStaysUnsent()
    {
        var ct = TestContext.Current.CancellationToken;
        var database = await CreateMigratedDatabaseAsync(ct).ConfigureAwait(true);
        await using var databaseScope = database.ConfigureAwait(true);
        var dataSource = new PostgresSessionConnection(database.ConnectionString).CreateDataSource();
        await using (dataSource.ConfigureAwait(false))
        {
            await AccountLifecycleProvisioning.ProvisionAsync(dataSource, [Account], ct).ConfigureAwait(true);
            await BindAsync(dataSource, ct).ConfigureAwait(true);
            var store = new PostgresLedgerOutboxStore(dataSource);
            var id = await EnqueueAndClaimAsync(store, ct).ConfigureAwait(true);

            await new PostgresAccountErasure(dataSource).EraseAsync(Account, TenantHash, static (_, _) => Task.CompletedTask, ct).ConfigureAwait(true);
            _ = await Should.ThrowAsync<AccountErasingException>(() => store.MarkSentAsync(id, ct).AsTask()).ConfigureAwait(true);

            (await SentAtAsync(dataSource, id, ct).ConfigureAwait(true)).ShouldBeNull("the refused mark leaves the erased account's row unsent");
        }
    }

    /// <summary>Creates the account's tenant binding in its own transaction, as account provisioning does.</summary>
    /// <param name="dataSource">The runtime data source whose search path names the schema.</param>
    /// <param name="cancellationToken">Cancels the database round trips.</param>
    private static async Task BindAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(true);
        await using (connection.ConfigureAwait(false))
        {
            var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(true);
            await using var transactionScope = transaction.ConfigureAwait(true);
            await AccountTenantBindings.EnsureAsync(connection, transaction, Account, TenantHash, cancellationToken).ConfigureAwait(true);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Enqueues one audit row for the account through the store's own transaction, then claims it, returning its outbox id.</summary>
    /// <param name="store">The outbox store under test.</param>
    /// <param name="cancellationToken">Cancels the database round trips.</param>
    /// <returns>The outbox row id.</returns>
    private static async Task<long> EnqueueAndClaimAsync(PostgresLedgerOutboxStore store, CancellationToken cancellationToken)
    {
        await store.EnqueueAsync(Sample(), transaction: null, Account, cancellationToken).ConfigureAwait(true);
        var due = await store.ClaimDueAsync(1, DateTimeOffset.UtcNow.AddMinutes(1), cancellationToken).ConfigureAwait(true);
        due.Count.ShouldBe(1, "the enqueued row is due");
        return due[0].Id;
    }

    /// <summary>Reads the outbox row's <c>sent_at</c>; null when the row is unsent.</summary>
    /// <param name="dataSource">The runtime data source.</param>
    /// <param name="id">The outbox row id.</param>
    /// <param name="cancellationToken">Cancels the database round trip.</param>
    private static async Task<object?> SentAtAsync(NpgsqlDataSource dataSource, long id, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(SentAtSql);
        command.Parameters.AddWithValue("id", id);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(true);
        return value is DBNull ? null : value;
    }

    private static AuditLedgerEvent Sample() => new()
    {
        Item = "01ARZ3NDEKTSV4RRFFQ69G5FAV",
        Outcome = "accepted",
        Summary = "audit",
        TenantHash = TenantHash,
    };

    /// <summary>Creates a throwaway database and migrates it to the compiled head, so the outbox, bindings and erasure run against the real schema.</summary>
    /// <param name="cancellationToken">Cancels the creation and the migration.</param>
    /// <returns>The migrated database. The caller disposes it.</returns>
    private static async Task<ThrowawayDatabase> CreateMigratedDatabaseAsync(CancellationToken cancellationToken)
    {
        var database = await ThrowawayDatabase.CreateAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using var output = new StringWriter();
            using var error = new StringWriter();
            string[] migrate = ["migrate", $"--{MigratorKey}={database.ConnectionString}"];
            var code = await MigrateCommand.RunAsync(migrate, output, error, cancellationToken).ConfigureAwait(false);
            code.ShouldBe(MigrateCommand.Succeeded);
            return database;
        }
        catch
        {
            await database.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
