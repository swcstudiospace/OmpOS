using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Npgsql;

namespace OmpOS.Sessions.Postgres.Tests;

/// <summary>
/// A throwaway PostgreSQL 17 server started with the podman CLI for one test run. The container has a unique name,
/// a free port bound to 127.0.0.1, and a random password that is passed through the environment, not the argument list.
/// </summary>
internal sealed class PodmanPostgresContainer : IAsyncDisposable
{
    /// <summary>The fully qualified image reference.</summary>
    internal const string Image = "docker.io/library/postgres:17";

    private static readonly TimeSpan ReadyTimeout = TimeSpan.FromSeconds(120);

    private readonly string _name;

    private PodmanPostgresContainer(string name, string connectionString)
    {
        _name = name;
        ConnectionString = connectionString;
    }

    /// <summary>Gets the administrative connection string for the running server (user postgres, database postgres).</summary>
    internal string ConnectionString { get; }

    /// <summary>Starts a container, waits until it accepts connections, and returns it.</summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>The running container.</returns>
    /// <exception cref="InvalidOperationException">podman is missing, the run failed, or the server never became ready.</exception>
    internal static async Task<PodmanPostgresContainer> StartAsync(CancellationToken cancellationToken)
    {
        var name = "ompos-test-pg-" + Guid.NewGuid().ToString("N");
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var port = FreeLoopbackPort();

        var run = await PodmanCommand.RunAsync(
            ["run", "-d", "--name", name, "-e", "POSTGRES_PASSWORD", "-p", $"127.0.0.1:{port}:5432", Image],
            new Dictionary<string, string?>(StringComparer.Ordinal) { ["POSTGRES_PASSWORD"] = password },
            cancellationToken).ConfigureAwait(false);
        if (run.ExitCode != 0)
        {
            throw new InvalidOperationException($"podman run for the PostgreSQL test server failed (exit {run.ExitCode}): {run.StandardError.Trim()}");
        }

        var connectionString = new NpgsqlConnectionStringBuilder
        {
            Host = "127.0.0.1",
            Port = port,
            Username = "postgres",
            Password = password,
            Database = "postgres",
        }.ConnectionString;

        var container = new PodmanPostgresContainer(name, connectionString);
        try
        {
            await WaitUntilReadyAsync(connectionString, cancellationToken).ConfigureAwait(false);
            return container;
        }
        catch
        {
            await container.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>Removes the container with <c>podman rm -f</c>.</summary>
    /// <returns>A task that completes when the container is removed.</returns>
    /// <exception cref="InvalidOperationException">podman could not remove the container, which would leak it.</exception>
    public async ValueTask DisposeAsync()
    {
        var result = await PodmanCommand.RunAsync(["rm", "-f", _name], null, CancellationToken.None).ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"podman rm -f {_name} failed (exit {result.ExitCode}): {result.StandardError.Trim()}");
        }
    }

    private static int FreeLoopbackPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    /// <summary>Polls until a real login succeeds. The official image restarts the server once during initialization, so early attempts can fail.</summary>
    private static async Task WaitUntilReadyAsync(string connectionString, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + ReadyTimeout;
        Exception? lastFailure = null;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (NpgsqlException failure)
            {
                lastFailure = failure;
            }

            if (DateTimeOffset.UtcNow >= deadline)
            {
                throw new InvalidOperationException($"The PostgreSQL test server did not accept connections within {ReadyTimeout.TotalSeconds} seconds.", lastFailure);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken).ConfigureAwait(false);
        }
    }
}
