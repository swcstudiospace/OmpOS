using System.Diagnostics;
using System.Globalization;
using Autonogrammer.Sessions.Postgres.Migrations;
using Npgsql;
using Shouldly;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// The allocation order of the feed and of the command log, written by two OS processes at once (the <c>Autonogrammer.Sessions.Postgres.Probe</c> console app, in its
/// <c>feed</c> and <c>commands</c> modes) while a resumer reads: the per-account feed id is allocated under the <c>accounts</c> row lock and the per-device sequence number under the
/// <c>dispatch_devices</c> row lock, so a reader that resumes from <c>id &gt; last</c> never skips a late commit and no two commands share a sequence number (D-28).
/// </summary>
[Collection(PostgresServerGroup.Name)]
public sealed class PostgresTwoProcessFeedTests(PostgresServerFixture server)
{
    private const int PerWriter = 40;

    private const string ProbeAssemblyName = "Autonogrammer.Sessions.Postgres.Probe.dll";

    // These names mirror the constants in the probe's Program class. The probe is a separate program with no shared assembly.
    private const string ProbeConnectionVariable = "AUTONOGRAMMER_PROBE_POSTGRES_CONNECTION";
    private const string ProbeEventCountVariable = "AUTONOGRAMMER_PROBE_EVENT_COUNT";
    private const string ProbeModeVariable = "AUTONOGRAMMER_PROBE_MODE";
    private const string ProbeAccountVariable = "AUTONOGRAMMER_PROBE_ACCOUNT_ID";
    private const string ProbeDeviceVariable = "AUTONOGRAMMER_PROBE_DEVICE_ID";

    private static readonly TimeSpan ResumeBudget = TimeSpan.FromSeconds(90);

    /// <summary>Two processes allocate feed ids under the account row lock while a resumer polls from the last id it saw: it sees every id 1..N once, in order, with no skip.</summary>
    [Fact]
    public async Task FeedIdsAllocatedByTwoProcessesNeverSkipForAResumer()
    {
        var ct = TestContext.Current.CancellationToken;
        var probePath = ProbePath();
        var database = await PostgresTestDatabase.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = await MigrateAsync(database, ct).ConfigureAwait(true);
            await using (dataSource.ConfigureAwait(true))
            {
                using (var account = dataSource.CreateCommand("INSERT INTO accounts (id, created_at, last_seen_at) VALUES ('acct-feed', now(), now())"))
                {
                    await account.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
                }

                var environment = ProbeEnvironment(database.ConnectionString, "feed");
                environment[ProbeAccountVariable] = "acct-feed";
                var writers = new[] { RunProbeAsync(probePath, environment, ct), RunProbeAsync(probePath, environment, ct) };
                var resumer = ResumeFeedAsync(new PostgresSessionConnection(database.ConnectionString), "acct-feed", 2 * PerWriter, ct);

                await Task.WhenAll(writers).ConfigureAwait(true);
                var seen = await resumer.ConfigureAwait(true);

                seen.ShouldBe(Enumerable.Range(1, 2 * PerWriter).Select(static id => (long)id).ToArray());
                using var counter = dataSource.CreateCommand("SELECT feed_seq FROM accounts WHERE id = 'acct-feed'");
                (await counter.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe((long)(2 * PerWriter));
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Two processes allocate command sequence numbers for one device under the device row lock: the numbers are 1..N, each once, and the unique key never fires.</summary>
    [Fact]
    public async Task CommandSequenceNumbersAllocatedByTwoProcessesAreDenseAndUnique()
    {
        var ct = TestContext.Current.CancellationToken;
        var probePath = ProbePath();
        var database = await PostgresTestDatabase.CreateAsync(server.AdminConnectionString, ct).ConfigureAwait(true);
        try
        {
            var dataSource = await MigrateAsync(database, ct).ConfigureAwait(true);
            await using (dataSource.ConfigureAwait(true))
            {
                using (var device = dataSource.CreateCommand(
                    "INSERT INTO dispatch_devices (id, account_id, name, platform, public_key, created_at) VALUES ('dev-1', 'acct-feed', 'laptop', 'linux', 'key', now())"))
                {
                    await device.ExecuteNonQueryAsync(ct).ConfigureAwait(true);
                }

                var environment = ProbeEnvironment(database.ConnectionString, "commands");
                environment[ProbeAccountVariable] = "acct-feed";
                environment[ProbeDeviceVariable] = "dev-1";

                await Task.WhenAll(RunProbeAsync(probePath, environment, ct), RunProbeAsync(probePath, environment, ct)).ConfigureAwait(true);

                var sequence = new List<long>();
                using (var command = dataSource.CreateCommand("SELECT seq FROM dispatch_commands WHERE device_id = 'dev-1' ORDER BY seq"))
                {
                    using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(true);
                    while (await reader.ReadAsync(ct).ConfigureAwait(true))
                    {
                        sequence.Add(reader.GetInt64(0));
                    }
                }

                sequence.ShouldBe(Enumerable.Range(1, 2 * PerWriter).Select(static id => (long)id).ToArray());
                using var counter = dataSource.CreateCommand("SELECT command_seq FROM dispatch_devices WHERE id = 'dev-1'");
                (await counter.ExecuteScalarAsync(ct).ConfigureAwait(true)).ShouldBe((long)(2 * PerWriter));
            }
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
        }
    }

    private static string ProbePath()
    {
        var probePath = Path.Combine(AppContext.BaseDirectory, ProbeAssemblyName);
        File.Exists(probePath).ShouldBeTrue($"The probe assembly must be built next to the test assembly: {probePath}");
        return probePath;
    }

    /// <summary>Migrates the throwaway database as the migration role would, and returns a runtime data source over it.</summary>
    private static async Task<NpgsqlDataSource> MigrateAsync(PostgresTestDatabase database, CancellationToken cancellationToken)
    {
        var connection = new PostgresSessionConnection(database.ConnectionString);
        var migrator = connection.CreateDataSource(PostgresRole.Migrator);
        await using (migrator.ConfigureAwait(false))
        {
            await new MigrationRunner(migrator).MigrateAsync(cancellationToken).ConfigureAwait(false);
        }

        return connection.CreateDataSource(PostgresRole.Runtime);
    }

    private static Dictionary<string, string> ProbeEnvironment(string connectionString, string mode) => new(StringComparer.Ordinal)
    {
        [ProbeConnectionVariable] = connectionString,
        [ProbeEventCountVariable] = PerWriter.ToString(CultureInfo.InvariantCulture),
        [ProbeModeVariable] = mode,
    };

    /// <summary>Reads the feed from the last id seen until it holds <paramref name="expected"/> ids or the budget ends, on a data source of its own.</summary>
    private static async Task<long[]> ResumeFeedAsync(PostgresSessionConnection connection, string account, int expected, CancellationToken cancellationToken)
    {
        var dataSource = connection.CreateDataSource(PostgresRole.Runtime);
        await using (dataSource.ConfigureAwait(false))
        {
            return await PollFeedAsync(dataSource, account, expected, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<long[]> PollFeedAsync(NpgsqlDataSource dataSource, string account, int expected, CancellationToken cancellationToken)
    {
        var seen = new List<long>();
        var last = 0L;
        var clock = Stopwatch.StartNew();
        while (seen.Count < expected && clock.Elapsed < ResumeBudget)
        {
            var before = seen.Count;
            using (var command = dataSource.CreateCommand("SELECT id FROM dispatch_events WHERE account_id = @account AND id > @last ORDER BY id"))
            {
                command.Parameters.AddWithValue("account", account);
                command.Parameters.AddWithValue("last", last);
                using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    last = reader.GetInt64(0);
                    seen.Add(last);
                }
            }

            if (seen.Count == before)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(5), cancellationToken).ConfigureAwait(false);
            }
        }

        return [.. seen];
    }

    /// <summary>Runs the probe as a separate process and fails unless it exits with code 0.</summary>
    private static async Task RunProbeAsync(string probePath, IReadOnlyDictionary<string, string> environment, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(probePath);
        foreach (var (name, value) in environment)
        {
            startInfo.Environment[name] = value;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The probe process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var output = await standardOutput.ConfigureAwait(false);
        var error = await standardError.ConfigureAwait(false);
        process.ExitCode.ShouldBe(0, $"The probe exited with code {process.ExitCode}.\nSTDOUT:\n{output}\nSTDERR:\n{error}");
    }

    private static string DotnetHost()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrWhiteSpace(host) ? "dotnet" : host;
    }
}
