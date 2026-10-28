using System.Globalization;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions.Postgres;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// A hosted routed run persists to the PostgreSQL session log. The host is Development with the PostgreSQL session store and the
/// in-memory harness adapter, which is deterministic and needs no omp binary. The run's events are read back through the API from
/// the log, and the same events are present in the session_events table for the session.
/// </summary>
public sealed class PostgresRoutedRunTests
{
    private static readonly TimeSpan SettleTimeout = TimeSpan.FromSeconds(15);

    /// <summary>Verifies a routed run's events are written to the PostgreSQL log, read back through the API, and present in the table.</summary>
    [Fact]
    public async Task RoutedRun_WritesEventsToPostgresLogAndReadsThemBack()
    {
        using var factory = new ApiFactory(PostgresSessionConfig());
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;
        var id = await SessionClient.CreateSessionAsync(client, testToken).ConfigureAwait(true);

        using var accepted = await SessionClient.PostPromptAsync(client, id, "{\"prompt\":\"hello\"}", testToken).ConfigureAwait(true);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        _ = await SessionClient.WaitForStatusAsync(client, id, SessionStatus.Completed, SettleTimeout, testToken).ConfigureAwait(true);

        var apiEvents = await SessionClient.ReadStreamAsync(client, $"/sessions/{id}/events", 500, testToken).ConfigureAwait(true);
        apiEvents.ShouldNotBeEmpty("the completed run's events are readable through the API");
        apiEvents[^1].EventType.ShouldBe("completed", "the API stream ends on the run's completed event");
        var apiSeqs = apiEvents.Select(static item => item.EventId ?? string.Empty).ToList();

        var repository = factory.Services.GetRequiredService<PostgresSessionRepository>();
        var logged = new List<StreamEvent>();
        await foreach (var evt in repository.ListFromSeqAsync(id, 0, testToken).ConfigureAwait(true))
        {
            logged.Add(evt);
        }

        var loggedSeqs = logged.Select(static evt => evt.Seq.ToString(CultureInfo.InvariantCulture)).ToList();
        loggedSeqs.ShouldNotBeEmpty("the routed run writes its events to the PostgreSQL session log");
        loggedSeqs.ShouldBe(apiSeqs, "the API reads the same Seq sequence from the PostgreSQL log that the run wrote");

        var dataSource = factory.Services.GetRequiredService<NpgsqlDataSource>();
        var command = dataSource.CreateCommand("SELECT COUNT(*) FROM autonogrammer.session_events WHERE session_id = @session_id");
        await using (command.ConfigureAwait(true))
        {
            command.Parameters.AddWithValue("session_id", id);
            var rows = Convert.ToInt64(await command.ExecuteScalarAsync(testToken).ConfigureAwait(true), CultureInfo.InvariantCulture);
            rows.ShouldBe((long)logged.Count, "PostgreSQL holds one session_events row per event of the session");
        }
    }

    private static Dictionary<string, string?> PostgresSessionConfig()
    {
        return new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Sessions:Store"] = "Postgres",
            ["Sessions:Postgres:ConnectionString"] = JwtTestHost.PostgresConnectionString(),
            ["Harness:Adapter"] = "InMemory",
        };
    }
}
