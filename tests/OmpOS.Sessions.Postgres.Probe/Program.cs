using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Sessions.Postgres.Probe;

/// <summary>
/// Appends a run of events to one session through the PostgreSQL session store, then exits. The two-process test starts it as
/// a separate OS process, so the writes come from a process that shares nothing with the test but the database. It exits 0
/// when every append has committed.
/// </summary>
/// <remarks>
/// <para>Environment variables, all required:</para>
/// <list type="bullet">
/// <item><c>OMPOS_PROBE_POSTGRES_CONNECTION</c>: the connection string of the database. It carries credentials and is never printed.</item>
/// <item><c>OMPOS_PROBE_SESSION_ID</c>: the session the events belong to. The session must already exist.</item>
/// <item><c>OMPOS_PROBE_EVENT_COUNT</c>: a positive number of events. Event <c>i</c> carries the text delta <c>i</c>.</item>
/// </list>
/// <para>Failures end the process with a non-zero exit code. The message names the missing variable and never its value.</para>
/// </remarks>
internal static class Program
{
    /// <summary>The environment variable that holds the PostgreSQL connection string.</summary>
    internal const string ConnectionVariable = "OMPOS_PROBE_POSTGRES_CONNECTION";

    /// <summary>The environment variable that holds the session identifier.</summary>
    internal const string SessionIdVariable = "OMPOS_PROBE_SESSION_ID";

    /// <summary>The environment variable that holds the number of events to append.</summary>
    internal const string EventCountVariable = "OMPOS_PROBE_EVENT_COUNT";

    /// <summary>A fixed instant, so the appended rows do not depend on the clock.</summary>
    private static readonly DateTimeOffset Timestamp = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    private static async Task Main()
    {
        var connectionString = RequiredVariable(ConnectionVariable);
        var sessionId = RequiredVariable(SessionIdVariable);
        var count = ParseCount(RequiredVariable(EventCountVariable));

        await AppendAsync(connectionString, sessionId, count).ConfigureAwait(false);
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

            // A starting instance ensures the schema, as the API does. The call is idempotent and serializes on an advisory lock.
            await PostgresSessionSchema.EnsureCreatedAsync(provider.GetRequiredService<NpgsqlDataSource>(), CancellationToken.None).ConfigureAwait(false);

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
