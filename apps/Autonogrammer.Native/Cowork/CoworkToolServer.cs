using System.Net.Http;
using System.Text;
using Autonogrammer.Harness.Mcp;
using Autonogrammer.Harness.McpGateway;

namespace Autonogrammer.Native.Cowork;

/// <summary>One loopback gateway per Cowork head. A failed health check degrades the session; it does not fail it.</summary>
internal sealed class CoworkToolServer : IAsyncDisposable
{
    /// <summary>The prefix of the degraded notice.</summary>
    public const string UnavailablePrefix = "Cowork tools are unavailable: ";

    private readonly McpToolRegistry _registry = new(enabledByDefault: false);
    private McpGatewayServer? _gateway;
    private int _disposed;

    /// <summary>Gets the gateway, or null before it started or after it failed.</summary>
    public McpGatewayServer? Gateway => _gateway;

    /// <summary>Gets whether the gateway is closed.</summary>
    public bool GatewayClosed => _gateway is null || Volatile.Read(ref _disposed) != 0;

    /// <summary>Starts the one gateway. A bind failure is returned as a degraded notice.</summary>
    /// <returns>Null when the gateway is listening.</returns>
    public string? Start()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return UnavailablePrefix + "the gateway is closed";
        }

        if (_gateway is not null)
        {
            return null;
        }

        try
        {
            var invoker = new McpToolInvoker(_registry, static (_, _) => ValueTask.FromResult(false));
            _gateway = McpGatewayServer.Start(_registry, invoker);
            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UnavailablePrefix + exception.GetType().Name;
        }
    }

    /// <summary>Opens a session and health-checks <c>initialize</c>. Failure closes the session slot and returns the degraded notice.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the health check.</param>
    /// <returns>The entry, or null with a notice when tools are unavailable.</returns>
    public async Task<(CoworkGatewayEntry? Entry, string? Notice)> OpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        var start = Start();
        if (start is not null || _gateway is null)
        {
            return (null, start ?? UnavailablePrefix + "the gateway is not listening");
        }

        Uri url;
        try
        {
            url = _gateway.OpenSession(sessionId);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return (null, UnavailablePrefix + exception.GetType().Name);
        }

        var notice = await HealthCheckAsync(url, _gateway.BearerToken, cancellationToken).ConfigureAwait(false);
        if (notice is not null)
        {
            _ = _gateway.CloseSession(sessionId);
            return (null, notice);
        }

        return (new CoworkGatewayEntry(url, _gateway.BearerToken), null);
    }

    /// <summary>Closes one session slot. The listener stays up.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void CloseSession(string sessionId) => _gateway?.CloseSession(sessionId);

    /// <summary>Describes the server without the bearer.</summary>
    /// <returns>The endpoint, or a closed marker.</returns>
    public override string ToString() => _gateway?.ToString() ?? "CoworkToolServer(closed)";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_gateway is not null)
        {
            await _gateway.DisposeAsync().ConfigureAwait(false);
            _gateway = null;
        }

        await _registry.DisposeAsync().ConfigureAwait(false);
    }

    private static async Task<string?> HealthCheckAsync(Uri url, string bearer, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2025-11-25\",\"capabilities\":{},\"clientInfo\":{\"name\":\"cowork\",\"version\":\"1\"}}}", Encoding.UTF8, "application/json"),
            };
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + bearer);
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return UnavailablePrefix + "the gateway health check returned " + ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            return null;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UnavailablePrefix + exception.GetType().Name;
        }
    }
}
