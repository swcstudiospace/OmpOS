using Autonogrammer.Api.DataPlane;
using Autonogrammer.Api.Options;
using Autonogrammer.Coordination;
using Autonogrammer.Sessions.Postgres;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Autonogrammer.Api.Accounts;

/// <summary>
/// The one-time backfill of <c>account_tenant_bindings</c> for accounts provisioned before migration M0015. It binds every active account that has no binding
/// yet to the tenant hash of its account id. A binding that already exists is never read or changed. The backfill waits for the schema to reach the compiled
/// head, so it runs whether the migrations ran in this process or in the migrate service. An account that is erasing or erased is skipped by the shared
/// admission, so the backfill never re-creates the binding that erasure removed. It does nothing when the session store is not PostgreSQL.
/// </summary>
internal sealed partial class TenantBindingBackfill
{
    /// <summary>The number of accounts bound in one transaction.</summary>
    public const int BatchSize = 200;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    private const string NextBatchSql =
        "SELECT l.account_id FROM account_lifecycle AS l " +
        "LEFT JOIN account_tenant_bindings AS b ON b.account_id = l.account_id " +
        "WHERE b.account_id IS NULL AND l.state = 'active' AND l.account_id > @after " +
        "ORDER BY l.account_id LIMIT @batch";

    /// <summary>Binds every provisioned account that lacks a binding, then logs how many were bound.</summary>
    /// <param name="services">The application services; the data source, hasher and schema probe are resolved from it.</param>
    /// <param name="cancellationToken">Stops the wait and the backfill when the host stops.</param>
    /// <returns>The number of accounts bound by this run; zero when the host is stopping or the store is not PostgreSQL.</returns>
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var sessions = services.GetRequiredService<IOptions<SessionsOptions>>().Value;
        if (!SessionsOptions.IsPostgresStore(sessions.Store))
        {
            return 0;
        }

        try
        {
            await WaitForSchemaAsync(services.GetRequiredService<SchemaHeadProbe>(), cancellationToken).ConfigureAwait(false);
            var bound = await BindUnboundAsync(
                services.GetRequiredService<NpgsqlDataSource>(),
                services.GetRequiredService<ITenantHasher>(),
                cancellationToken).ConfigureAwait(false);
            var logger = services.GetRequiredService<ILogger<TenantBindingBackfill>>();
            if (logger.IsEnabled(LogLevel.Information))
            {
                LogBound(logger, bound);
            }
            return bound;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping before the backfill finished; the next start runs it again.
            return 0;
        }
    }

    private static async Task WaitForSchemaAsync(SchemaHeadProbe probe, CancellationToken cancellationToken)
    {
        while (true)
        {
            try
            {
                if (await probe.GetAsync(refresh: true, cancellationToken).ConfigureAwait(false) != SchemaState.Behind)
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is Autonogrammer.Resilience.DataPlaneTimeoutException or Npgsql.NpgsqlException)
            {
                // The store is not reachable yet; keep waiting until the host stops or the schema reaches the head.
            }

            await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<int> BindUnboundAsync(NpgsqlDataSource dataSource, ITenantHasher tenants, CancellationToken cancellationToken)
    {
        var bound = 0;
        var after = string.Empty;
        while (true)
        {
            var batch = await ReadBatchAsync(dataSource, after, cancellationToken).ConfigureAwait(false);
            if (batch.Count == 0)
            {
                return bound;
            }

            bound += await BindBatchAsync(dataSource, tenants, batch, cancellationToken).ConfigureAwait(false);
            after = batch[^1];
        }
    }

    private static async Task<IReadOnlyList<string>> ReadBatchAsync(NpgsqlDataSource dataSource, string after, CancellationToken cancellationToken)
    {
        using var command = dataSource.CreateCommand(NextBatchSql);
        command.Parameters.AddWithValue("after", after);
        command.Parameters.AddWithValue("batch", BatchSize);
        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        var accountIds = new List<string>(BatchSize);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            accountIds.Add(reader.GetString(0));
        }

        return accountIds;
    }

    private static async Task<int> BindBatchAsync(
        NpgsqlDataSource dataSource,
        ITenantHasher tenants,
        IReadOnlyList<string> accountIds,
        CancellationToken cancellationToken)
    {
        using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var bound = 0;
        foreach (var accountId in accountIds)
        {
            if (await TryBindAsync(connection, transaction, tenants, accountId, cancellationToken).ConfigureAwait(false))
            {
                bound++;
            }
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return bound;
    }

    private static async Task<bool> TryBindAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        ITenantHasher tenants,
        string accountId,
        CancellationToken cancellationToken)
    {
        try
        {
            await AccountAdmission.AdmitSharedAsync(connection, transaction, accountId, cancellationToken).ConfigureAwait(false);
        }
        catch (AccountErasingException)
        {
            return false;
        }

        await AccountTenantBindings.EnsureAsync(connection, transaction, accountId, tenants.HashTenant(accountId), cancellationToken).ConfigureAwait(false);
        return true;
    }

    [LoggerMessage(EventId = 6844, Level = LogLevel.Information, Message = "The tenant binding backfill bound {Count} accounts.")]
    private static partial void LogBound(ILogger logger, int count);
}
