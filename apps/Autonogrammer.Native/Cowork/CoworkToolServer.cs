using System.Text;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Harness.Mcp;
using Autonogrammer.Harness.McpGateway;

namespace Autonogrammer.Native.Cowork;

/// <summary>One loopback gateway per Cowork head. A failed health check degrades the session; it does not fail it.</summary>
internal sealed class CoworkToolServer : IAsyncDisposable
{
    /// <summary>The prefix of the degraded notice.</summary>
    public const string UnavailablePrefix = "Cowork tools are unavailable: ";

    private readonly McpToolRegistry _registry = new(enabledByDefault: false);
    private readonly Lock _gate = new();
    private int _activeOpens;
    private TaskCompletionSource? _opensDrained;
    private Task? _disposeTask;
    private McpGatewayServer? _gateway;
    private readonly ICoworkToolHost? _coworkTools;
    private int _disposed;

    /// <summary>Creates the server. Its gateway serves <paramref name="coworkTools"/> beside the registry; null serves registry tools only.</summary>
    /// <param name="coworkTools">The Cowork tool host the gateway serves, or null.</param>
    public CoworkToolServer(ICoworkToolHost? coworkTools = null)
    {
        _coworkTools = coworkTools;
    }

    /// <summary>Gets the gateway, or null before it started or after it failed.</summary>
    public McpGatewayServer? Gateway => _gateway;

    /// <summary>Gets whether the gateway is closed.</summary>
    public bool GatewayClosed => _gateway is null || Volatile.Read(ref _disposed) != 0;

    /// <summary>Starts the one gateway. A bind failure is returned as a degraded notice.</summary>
    /// <returns>Null when the gateway is listening.</returns>
    public string? Start()
    {
        lock (_gate)
        {
            if (_disposed != 0)
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
                _gateway = McpGatewayServer.Start(_registry, invoker, _coworkTools);
                return null;
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return "state=ERROR: " + UnavailablePrefix + exception.GetType().Name;
            }
        }
    }

    /// <summary>Opens a session and health-checks <c>initialize</c>. A failed check, or a gateway closed during it, closes the session slot and returns the degraded notice. Cancellation closes the slot it opened and throws <see cref="OperationCanceledException"/>; it is not reported as a notice.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the health check.</param>
    /// <returns>The entry, or null with a notice when tools are unavailable.</returns>
    public async Task<(CoworkGatewayEntry? Entry, string? Notice)> OpenAsync(string sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        McpGatewayServer gateway;
        Uri url;
        lock (_gate)
        {
            var start = Start();
            if (start is not null || _gateway is null)
            {
                return "state=ERROR: " + (start ?? UnavailablePrefix + "the gateway is not listening");
            }
            gateway = _gateway;
            try
            {
                url = gateway.OpenSession(sessionId);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                return (null, UnavailablePrefix + exception.GetType().Name);
            }

            _activeOpens++;
        }

        var transferred = false;
        try
        {
            // SOTA LOADING emitted upstream in host before/during HealthCheckAsync (150s a11y); here on fail ERROR
            var notice = await HealthCheckAsync(url, gateway.BearerToken, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (notice is not null || _disposed != 0)
                {
                    return (null, "state=ERROR: " + (notice ?? UnavailablePrefix + "the gateway is closed"));
                }
                var entry = new CoworkGatewayEntry(url, gateway.BearerToken);
                transferred = true;
                return (entry, null);
            }
        }
        finally
        {
            CompleteOpen(gateway, sessionId, transferred);
        }
    }

    private void CompleteOpen(McpGatewayServer gateway, string sessionId, bool transferred)
    {
        try
        {
            if (!transferred)
            {
                _ = gateway.CloseSession(sessionId);
            }
        }
        finally
        {
            lock (_gate)
            {
                _activeOpens--;
                if (_activeOpens == 0)
                {
                    _opensDrained?.SetResult();
                }
            }
        }
    }

    /// <summary>Closes one session slot. The listener stays up.</summary>
    /// <param name="sessionId">The session identifier.</param>
    public void CloseSession(string sessionId) => _gateway?.CloseSession(sessionId);

    /// <summary>Describes the server without the bearer.</summary>
    /// <returns>The endpoint, or a closed marker.</returns>
    public override string ToString() => _gateway?.ToString() ?? "CoworkToolServer(closed)";

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposeTask is null)
            {
                Volatile.Write(ref _disposed, 1);
                var pending = _activeOpens == 0
                    ? Task.CompletedTask
                    : (_opensDrained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
                _disposeTask = DisposeCoreAsync(pending);
            }

            return new ValueTask(_disposeTask);
        }
    }

    private async Task DisposeCoreAsync(Task pending)
    {
        await pending.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        McpGatewayServer? gateway;
        lock (_gate)
        {
            gateway = _gateway;
            _gateway = null;
        }
        try
        {
            if (gateway is not null)
            {
                await gateway.DisposeAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            await _registry.DisposeAsync().ConfigureAwait(false);
        }
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return UnavailablePrefix + exception.GetType().Name;
        }
    }
}
