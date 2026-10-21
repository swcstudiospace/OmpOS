using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OmpOS.Contracts.Streaming;
using Shouldly;
using Xunit;
using static OmpOS.Sessions.Postgres.Tests.SessionSamples;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>
/// Appends in one OS process and reads in another, both against one PostgreSQL database. The writer is the
/// <c>OmpOS.Sessions.Postgres.Probe</c> console app, which the project reference copies next to this assembly.
/// </summary>
/// <remarks>
/// The test needs an administrative connection string in <c>OMPOS_TEST_POSTGRES_CONNECTION</c>. Without it the test is
/// skipped and no container is started. It does not use <see cref="PostgresServerFixture"/>, which would start one.
/// </remarks>
public sealed class PostgresTwoProcessAppendTests
{
    /// <summary>The number of events the probe appends.</summary>
    private const int EventCount = 25;

    private const string SessionId = "s-two-process";

    private const string ProbeAssemblyName = "OmpOS.Sessions.Postgres.Probe.dll";

    // These names mirror the constants in the probe's Program class. The probe is a separate program with no shared assembly.
    private const string ProbeConnectionVariable = "OMPOS_PROBE_POSTGRES_CONNECTION";
    private const string ProbeSessionIdVariable = "OMPOS_PROBE_SESSION_ID";
    private const string ProbeEventCountVariable = "OMPOS_PROBE_EVENT_COUNT";

    /// <summary>Events appended by another process read back here in full: Seq is 1..N with no gaps, and there are N of them.</summary>
    [Fact]
    public async Task EventsAppendedByAnotherProcessReadBackGaplessFromSeqOne()
    {
        var ct = TestContext.Current.CancellationToken;
        var adminConnection = Environment.GetEnvironmentVariable(PostgresServerFixture.ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(adminConnection))
        {
            Assert.Skip($"Set {PostgresServerFixture.ConnectionEnvironmentVariable} to an administrative PostgreSQL connection string to run the two-process test.");
            return;
        }

        var probePath = Path.Combine(AppContext.BaseDirectory, ProbeAssemblyName);
        File.Exists(probePath).ShouldBeTrue($"The probe assembly must be built next to the test assembly: {probePath}");

        var database = await PostgresTestDatabase.CreateAsync(adminConnection, ct).ConfigureAwait(true);
        try
        {
            await SeedSessionAsync(database.ConnectionString, ct).ConfigureAwait(true);
            await RunProbeAsync(probePath, database.ConnectionString, ct).ConfigureAwait(true);

            List<StreamEvent> events;
            var provider = BuildStore(database.ConnectionString);
            await using (provider.ConfigureAwait(false))
            {
                var repository = provider.GetRequiredService<PostgresSessionRepository>();
                events = await ListAsync(repository, SessionId, 0, ct).ConfigureAwait(true);
            }

            events.Select(static evt => evt.Seq).ToArray().ShouldBe(Enumerable.Range(1, EventCount).Select(static i => (long)i).ToArray());
            events.Count.ShouldBe(EventCount);
            events.Select(static evt => evt.Payload).OfType<TextDeltaPayload>().Select(static payload => payload.Delta).ToArray()
                .ShouldBe(Enumerable.Range(0, EventCount).Select(static i => i.ToString(CultureInfo.InvariantCulture)).ToArray());
        }
        finally
        {
            await database.DisposeAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Builds a service provider that registers the PostgreSQL session store for one connection string, as production does.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <returns>The provider. The caller disposes it.</returns>
    private static ServiceProvider BuildStore(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [ServiceCollectionExtensions.ConnectionStringKey] = connectionString,
            })
            .Build();
        return new ServiceCollection().AddPostgresSessionStore(configuration).BuildServiceProvider();
    }

    /// <summary>Creates the schema and the session the probe appends to.</summary>
    /// <param name="connectionString">The database connection string.</param>
    /// <param name="ct">Cancels the operation.</param>
    private static async Task SeedSessionAsync(string connectionString, CancellationToken ct)
    {
        var provider = BuildStore(connectionString);
        await using (provider.ConfigureAwait(false))
        {
            await PostgresSessionSchema.EnsureCreatedAsync(provider.GetRequiredService<NpgsqlDataSource>(), ct).ConfigureAwait(true);
            await provider.GetRequiredService<PostgresSessionRepository>().SaveAsync(Sample(SessionId), ct).ConfigureAwait(true);
        }
    }

    /// <summary>Runs the probe as a separate process and fails unless it exits with code 0.</summary>
    /// <param name="probePath">The probe assembly.</param>
    /// <param name="connectionString">The database connection string, passed through the environment only.</param>
    /// <param name="ct">Cancels the wait. A cancelled probe is killed.</param>
    private static async Task RunProbeAsync(string probePath, string connectionString, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(DotnetHost())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add(probePath);
        startInfo.Environment[ProbeConnectionVariable] = connectionString;
        startInfo.Environment[ProbeSessionIdVariable] = SessionId;
        startInfo.Environment[ProbeEventCountVariable] = EventCount.ToString(CultureInfo.InvariantCulture);

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The probe process did not start.");
        var standardOutput = process.StandardOutput.ReadToEndAsync(ct);
        var standardError = process.StandardError.ReadToEndAsync(ct);
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        var output = await standardOutput.ConfigureAwait(true);
        var error = await standardError.ConfigureAwait(true);
        process.ExitCode.ShouldBe(0, $"The probe exited with code {process.ExitCode}.\nSTDOUT:\n{output}\nSTDERR:\n{error}");
    }

    /// <summary>The host that runs the probe assembly: the dotnet executable the test host was started with, or the one on PATH.</summary>
    /// <returns>The dotnet executable path, or <c>dotnet</c>.</returns>
    private static string DotnetHost()
    {
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        return string.IsNullOrWhiteSpace(host) ? "dotnet" : host;
    }
}
