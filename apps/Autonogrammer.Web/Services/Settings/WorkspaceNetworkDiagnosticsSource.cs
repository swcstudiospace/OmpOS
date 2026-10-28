using Autonogrammer.Presentation.Settings;

namespace Autonogrammer.Web.Services.Settings;

/// <summary>
/// The Network page over the API. The browser cannot reach the providers (CORS) and the API sends no request to an upstream, so a run lists the upstreams
/// the API is configured for, every one as <see cref="EndpointStatus.NotChecked"/> with the API's own sentence. It never reports an endpoint as reachable.
/// </summary>
internal sealed class WorkspaceNetworkDiagnosticsSource : INetworkDiagnosticsSource
{
    private readonly WorkspaceSettingsClient _client;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="WorkspaceNetworkDiagnosticsSource"/> class.</summary>
    /// <param name="client">The settings client.</param>
    /// <param name="time">The clock that stamps the report.</param>
    internal WorkspaceNetworkDiagnosticsSource(WorkspaceSettingsClient client, TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(time);
        _client = client;
        _time = time;
    }

    /// <inheritdoc/>
    /// <exception cref="SettingsSourceException">The API could not be asked; the message says why.</exception>
    public async Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _client.GetNetworkAsync(cancellationToken).ConfigureAwait(false);
            var checks = response.Endpoints
                .Select(endpoint => new EndpointCheck(endpoint.Name, endpoint.Host, EndpointStatus.NotChecked, null, null, response.Reason))
                .ToArray();
            return new NetworkDiagnosticsReport(checks, _time.GetUtcNow());
        }
        catch (Exception exception) when (WorkspaceSettingsFailure.IsExpected(exception))
        {
            throw new SettingsSourceException(WorkspaceSettingsFailure.Describe(exception), exception);
        }
    }
}
