namespace Autonogrammer.Harness.Providers;

/// <summary>
/// The outcome of one probe of a local provider endpoint. The message is written for the user and tells them what to do
/// next; it never contains a key.
/// </summary>
/// <param name="Status">What the probe found.</param>
/// <param name="ProbedUri">The model-list address that was requested.</param>
/// <param name="HttpStatus">The HTTP status the server answered with, or null when nothing answered.</param>
/// <param name="ModelIds">The model ids the server listed, in the order it listed them. Empty unless it is running.</param>
/// <param name="Message">A sentence for the user that names the provider, the address and the next step.</param>
public sealed record ProviderProbeResult(
    ProviderProbeStatus Status,
    Uri ProbedUri,
    int? HttpStatus,
    IReadOnlyList<string> ModelIds,
    string Message)
{
    /// <summary>True when something answered at the endpoint, whether or not it accepted the probe.</summary>
    public bool IsReachable => Status != ProviderProbeStatus.NotRunning;
}
