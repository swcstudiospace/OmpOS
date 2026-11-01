using System.Globalization;
using Autonogrammer.Contracts.Streaming;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Autonogrammer.Sessions.Postgres.Probe;

/// <summary>
/// Writes to the PostgreSQL data plane from its own OS process, then exits. The two-process tests start it as a separate process, so the writes come from
/// a process that shares nothing with the test but the database. It exits 0 when every write has committed.
/// </summary>
/// <remarks>
/// <para>Environment variables:</para>
/// <list type="bullet">
/// <item><c>AUTONOGRAMMER_PROBE_POSTGRES_CONNECTION</c> (required): the connection string of the database. It carries credentials and is never printed.</item>
/// <item><c>AUTONOGRAMMER_PROBE_EVENT_COUNT</c> (required): a positive number of writes.</item>
/// <item><c>AUTONOGRAMMER_PROBE_MODE</c> (optional): <c>feed</c> allocates account feed ids, <c>commands</c> allocates device command sequence numbers, anything else appends session events.</item>
/// <item><c>AUTONOGRAMMER_PROBE_SESSION_ID</c> (append mode): the session the events belong to. The session must already exist. Event <c>i</c> carries the text delta <c>i</c>.</item>
/// <item><c>AUTONOGRAMMER_PROBE_ACCOUNT_ID</c> (feed and commands modes): the account whose row exists in <c>accounts</c> (feed) or owns the device (commands).</item>
/// <item><c>AUTONOGRAMMER_PROBE_DEVICE_ID</c> (commands mode): the device whose row exists in <c>dispatch_devices</c>.</item>
/// </list>
/// <para>Failures end the process with a non-zero exit code. The message names the missing variable and never its value.</para>
/// </remarks>
internal static class Program
{
    /// <summary>The environment variable that holds the PostgreSQL connection string.</summary>
    internal const string ConnectionVariable = "AUTONOGRAMMER_PROBE_POSTGRES_CONNECTION";

    /// <summary>The environment variable that holds the session identifier.</summary>
    internal const string SessionIdVariable = "AUTONOGRAMMER_PROBE_SESSION_ID";

    /// <summary>The environment variable that holds the number of events to append.</summary>
    internal const string EventCountVariable = "AUTONOGRAMMER_PROBE_EVENT_COUNT";

    /// <summary>The environment variable that selects what the probe writes.</summary>
    internal const string ModeVariable = "AUTONOGRAMMER_PROBE_MODE";

    /// <summary>The environment variable that holds the account identifier of the feed and commands modes.</summary>
    internal const string AccountIdVariable = "AUTONOGRAMMER_PROBE_ACCOUNT_ID";

    /// <summary>The environment variable that holds the device identifier of the commands mode.</summary>
    internal const string DeviceIdVariable = "AUTONOGRAMMER_PROBE_DEVICE_ID";

    // The allocation pattern of D-28: the counter row is locked first, the counter moves under that lock, and the row that carries the number
    // is written in the same transaction, so a reader that resumes from a number never skips a late commit.
    private const string LockAccountSql = "SELECT feed_seq FROM accounts WHERE id = @account_id FOR UPDATE";
    private const string NextFeedIdSql = "UPDATE accounts SET feed_seq = feed_seq + 1 WHERE id = @account_id RETURNING feed_seq";
    private const string InsertFeedEventSql = "INSERT INTO dispatch_events (account_id, id, occurred_at, kind) VALUES (@account_id, @id, now(), 'probe')";
    private const string LockDeviceSql = "SELECT command_seq FROM dispatch_devices WHERE id = @device_id FOR UPDATE";
    private const string NextCommandSeqSql = "UPDATE dispatch_devices SET command_seq = command_seq + 1 WHERE id = @device_id RETURNING command_seq";

    private const string InsertCommandSql =
        "INSERT INTO dispatch_commands (id, device_id, account_id, seq, op, state, idempotency_key, expires_at, created_at, updated_at) " +
        "VALUES (@id, @device_id, @account_id, @seq, 'probe', 'queued', @id, now() + INTERVAL '1 hour', now(), now())";

    /// <summary>A fixed instant, so the appended rows do not depend on the clock.</summary>
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private static async Task Main()
    {
        var connectionString = RequiredVariable(ConnectionVariable);
        var count = ParseCount(RequiredVariable(EventCountVariable));
        var mode = Environment.GetEnvironmentVariable(ModeVariable);

        if (string.Equals(mode, "feed", StringComparison.Ordinal))
        {
            await WriteFeedAsync(connectionString, RequiredVariable(AccountIdVariable), count).ConfigureAwait(false);
        }
        else if (string.Equals(mode, "commands", StringComparison.Ordinal))
        {
            await WriteCommandsAsync(connectionString, RequiredVariable(AccountIdVariable), RequiredVariable(DeviceIdVariable), count).ConfigureAwait(false);
        }
        else
        {
            await AppendAsync(connectionString, RequiredVariable(SessionIdVariable), count).ConfigureAwait(false);
        }
    }

    /// <summary>Allocates <paramref name="count"/> feed ids for one account, each under the account row lock, and writes the feed event that carries it.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="accountId">The account whose <c>accounts</c> row exists.</param>
    /// <param name="count">The number of feed events to write.</param>
    private static async Task WriteFeedAsync(string connectionString, string accountId, int count)
    {
        var provider = BuildProvider(connectionString);
        await using (provider.ConfigureAwait(false))
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            for (var index = 0; index < count; index++)
            {
                var connection = await dataSource.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.Parameters.AddWithValue("account_id", accountId);

                        command.CommandText = LockAccountSql;
                        _ = await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
                        command.CommandText = NextFeedIdSql;
                        var id = Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false), CultureInfo.InvariantCulture);

                        // Hold the lock a moment, so a writer that did not take it would let a later id commit first.
                        await Task.Delay(index % 3, CancellationToken.None).ConfigureAwait(false);

                        command.CommandText = InsertFeedEventSql;
                        command.Parameters.AddWithValue("id", id);
                        await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>Allocates <paramref name="count"/> command sequence numbers for one device, each under the device row lock, and writes the command that carries it.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="accountId">The account that owns the device.</param>
    /// <param name="deviceId">The device whose <c>dispatch_devices</c> row exists.</param>
    /// <param name="count">The number of commands to write.</param>
    private static async Task WriteCommandsAsync(string connectionString, string accountId, string deviceId, int count)
    {
        var provider = BuildProvider(connectionString);
        await using (provider.ConfigureAwait(false))
        {
            var dataSource = provider.GetRequiredService<NpgsqlDataSource>();
            var process = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
            for (var index = 0; index < count; index++)
            {
                var connection = await dataSource.OpenConnectionAsync(CancellationToken.None).ConfigureAwait(false);
                await using (connection.ConfigureAwait(false))
                {
                    var transaction = await connection.BeginTransactionAsync(CancellationToken.None).ConfigureAwait(false);
                    await using (transaction.ConfigureAwait(false))
                    {
                        using var command = connection.CreateCommand();
                        command.Transaction = transaction;
                        command.Parameters.AddWithValue("device_id", deviceId);

                        command.CommandText = LockDeviceSql;
                        _ = await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false);
                        command.CommandText = NextCommandSeqSql;
                        var seq = Convert.ToInt64(await command.ExecuteScalarAsync(CancellationToken.None).ConfigureAwait(false), CultureInfo.InvariantCulture);

                        await Task.Delay(index % 3, CancellationToken.None).ConfigureAwait(false);

                        command.CommandText = InsertCommandSql;
                        command.Parameters.AddWithValue("id", $"{process}-{index}");
                        command.Parameters.AddWithValue("account_id", accountId);
                        command.Parameters.AddWithValue("seq", seq);
                        await command.ExecuteNonQueryAsync(CancellationToken.None).ConfigureAwait(false);
                        await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                }
            }
        }
    }

    /// <summary>Builds a service provider that registers the PostgreSQL session store for one connection string, as production does.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The provider. The caller disposes it.</returns>
    private static ServiceProvider BuildProvider(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ServiceCollectionExtensions.ConnectionStringKey] = connectionString,
            })
            .Build();
        return new ServiceCollection().AddPostgresSessionStore(configuration).BuildServiceProvider();
    }

    /// <summary>Appends <paramref name="count"/> events through the registered PostgreSQL session repository.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="sessionId">The session to append to.</param>
    /// <param name="count">The number of events to append.</param>
    private static async Task AppendAsync(string connectionString, string sessionId, int count)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ServiceCollectionExtensions.ConnectionStringKey] = connectionString,
            })
            .Build();
        var services = new ServiceCollection().AddPostgresSessionStore(configuration);
        var provider = services.BuildServiceProvider();
        await using (provider.ConfigureAwait(false))
        {
            // The schema is migrated by the test before the probe starts; the probe only writes.
            PostgresSessionRepository repository = provider.GetRequiredService<PostgresSessionRepository>();
            for (var index = 0; index < count; index++)
            {
                await repository.AppendAsync(sessionId, Event(sessionId, index), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static StreamEvent Event(string sessionId, int index) => new()
    {
        Seq = 0,
        SessionId = sessionId,
        CorrelationId = $"{sessionId}/probe",
        Timestamp = Timestamp,
        Payload = new TextDeltaPayload { Delta = index.ToString(CultureInfo.InvariantCulture) },
    };

    /// <summary>Reads a required environment variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The non-blank value.</returns>
    /// <exception cref="InvalidOperationException">The variable is missing or blank. The message names it, never its value.</exception>
    private static string RequiredVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value)
            ? throw new InvalidOperationException($"Environment variable '{name}' is required.")
            : value;
    }

    /// <summary>Parses the event count, which must be a positive decimal integer.</summary>
    /// <param name="text">The raw value.</param>
    /// <returns>The count.</returns>
    /// <exception cref="FormatException"><paramref name="text"/> is not a positive decimal integer.</exception>
    private static int ParseCount(string text)
    {
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var count) || count < 1)
        {
            throw new FormatException($"Environment variable '{EventCountVariable}' must be a positive decimal integer.");
        }

        return count;
    }
}
