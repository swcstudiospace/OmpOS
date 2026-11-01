using System.Net;
using System.Net.Sockets;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Tests;

/// <summary>
/// A multiplexer aimed at a loopback port that nothing listens on, so every call fails. It never touches the shared test container,
/// which other test modules use in parallel.
/// </summary>
internal sealed class UnreachableDragonfly : IDisposable
{
    private UnreachableDragonfly(ConnectionMultiplexer multiplexer)
    {
        Multiplexer = multiplexer;
    }

    /// <summary>Gets the connection that has no server.</summary>
    public ConnectionMultiplexer Multiplexer { get; }

    /// <summary>Opens the connection. <c>abortConnect=false</c> lets it exist without a server.</summary>
    /// <returns>The unreachable connection.</returns>
    public static async Task<UnreachableDragonfly> OpenAsync()
    {
        int port;
        using (var listener = new TcpListener(IPAddress.Loopback, 0))
        {
            listener.Start();
            port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
        }

        var configuration = ConfigurationOptions.Parse($"127.0.0.1:{port}");
        configuration.AbortOnConnectFail = false;
        configuration.ConnectTimeout = 300;
        configuration.AsyncTimeout = 250;
        configuration.SyncTimeout = 250;
        var multiplexer = await ConnectionMultiplexer.ConnectAsync(configuration).ConfigureAwait(false);
        return new UnreachableDragonfly(multiplexer);
    }

    /// <summary>Closes the connection.</summary>
    public void Dispose()
    {
        Multiplexer.Dispose();
    }
}
