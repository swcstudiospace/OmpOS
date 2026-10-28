namespace Autonogrammer.Presentation.Settings;

/// <summary>
/// Where the Network page gets a diagnostics run. A run sends one unauthenticated request to each endpoint the source knows and nothing
/// else. It happens only when the user asks; the page never runs it by itself.
/// </summary>
public interface INetworkDiagnosticsSource
{
    /// <summary>Checks every endpoint the source knows.</summary>
    /// <param name="cancellationToken">Cancels the run. A cancelled run reports nothing.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    /// <exception cref="SettingsSourceException">The run could not be made at all.</exception>
    Task<NetworkDiagnosticsReport> RunAsync(CancellationToken cancellationToken);
}
