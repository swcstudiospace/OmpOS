using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The <see cref="INetworkDiagnosticsSource"/> of the web head. A page can reach one endpoint: the Autonogrammer API it talks to. The check calls
/// <c>GET /health/ready</c>; model provider endpoints are checked by the API host, not by the browser (CORS would forbid it).
/// </summary>
internal sealed class WebNetworkDiagnosticsSource : INetworkDiagnosticsSource
{
    private readonly IOmpApiClient _api;
    private readonly string _host;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="WebNetworkDiagnosticsSource"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="host">The host (and port) of the API, for display. Never a credential.</param>
    /// <param name="time">The clock of the latency and the report time.</param>
    internal WebNetworkDiagnosticsSource(IOmpApiClient api, string host, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentNullException.ThrowIfNull(time);
        _api = api;
        _host = host;
        _time = time;
    }

    /// <inheritdoc />
    public async Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        EndpointCheck check;
        try
        {
            var ready = await _api.IsReadyAsync(cancellationToken).ConfigureAwait(true);
            var latency = _time.GetElapsedTime(started);
            check = ready
                ? new EndpointCheck("Autonogrammer API", _host, EndpointStatus.Reachable, 200, latency, "The API answered its readiness check.")
                : new EndpointCheck("Autonogrammer API", _host, EndpointStatus.Unreachable, null, latency, "The API answered, but it reports that it is not ready.");
        }
        catch (Exception fault) when (WebFailureText.IsExpected(fault))
        {
            check = new EndpointCheck("Autonogrammer API", _host, EndpointStatus.Unreachable, null, null, WebFailureText.Describe(fault));
        }

        return new NetworkDiagnosticsReport([check], _time.GetUtcNow());
    }
}
