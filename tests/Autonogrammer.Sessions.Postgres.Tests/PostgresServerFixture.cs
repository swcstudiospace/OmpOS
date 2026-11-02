using System.Diagnostics.CodeAnalysis;
using Npgsql;
using Xunit;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>
/// Supplies one PostgreSQL server for the whole test run. The server comes from the environment variable
/// <c>AUTONOGRAMMER_TEST_POSTGRES_CONNECTION</c> when it is set (a CI service or a developer database, which must have the
/// TimescaleDB extension available). Otherwise a TimescaleDB 2.13 container on PostgreSQL 16 is started with podman and
/// removed when the run ends. When neither is possible the fixture throws; the tests never skip.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "xUnit discovers only public test classes, and those classes take this fixture as a constructor parameter, so the fixture must be public.")]
public sealed class PostgresServerFixture : IAsyncLifetime
{
    /// <summary>The environment variable that names an existing administrative PostgreSQL connection string.</summary>
    public const string ConnectionEnvironmentVariable = "AUTONOGRAMMER_TEST_POSTGRES_CONNECTION";

    /// <summary>The name <c>scripts/test-services.sh</c> gives the throwaway PostgreSQL container. A server named by <see cref="ConnectionEnvironmentVariable"/> is assumed to be that container.</summary>
    public const string TestServicesContainerName = "autonogrammer-test-postgres";

    /// <summary>The environment variable that names the throwaway PostgreSQL container of a named test-services run (scripts/test-services.sh exports it).</summary>
    public const string ContainerEnvironmentVariable = "AUTONOGRAMMER_TEST_POSTGRES_CONTAINER";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private PodmanPostgresContainer? _container;

    /// <summary>Gets the administrative connection string. Tests create their own schema on this server.</summary>
    public string AdminConnectionString { get; private set; } = string.Empty;

    /// <summary>Gets the name of the podman container that runs the server, for the tests that run <c>psql</c> inside it.</summary>
    public string ContainerName { get; private set; } = TestServicesContainerName;

    /// <summary>Connects to the server, starting a container when no connection string is configured.</summary>
    /// <returns>A task that completes when the server is ready.</returns>
    public async ValueTask InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable(ConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            AdminConnectionString = configured;
            ContainerName = Environment.GetEnvironmentVariable(ContainerEnvironmentVariable) is { Length: > 0 } named ? named : TestServicesContainerName;
        }
        else
        {
            using var cancellation = new CancellationTokenSource(StartTimeout);
            _container = await PodmanPostgresContainer.StartAsync(cancellation.Token).ConfigureAwait(false);
            AdminConnectionString = _container.ConnectionString;
            ContainerName = _container.Name;
        }

        await RequireTimescaleAsync().ConfigureAwait(false);
    }

    private async Task RequireTimescaleAsync()
    {
        using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb')";
        if (await command.ExecuteScalarAsync().ConfigureAwait(false) is not true)
        {
            throw new InvalidOperationException(
                "The test PostgreSQL server has no TimescaleDB extension in its default database. The migrations never create the extension: use the pinned " +
                "timescale/timescaledb image (scripts/test-services.sh) or install the extension once as an administrator.");
        }
    }

    /// <summary>Removes the container this fixture started, if any.</summary>
    /// <returns>A task that completes when the cleanup is done.</returns>
    public async ValueTask DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
            _container = null;
        }
    }
}
