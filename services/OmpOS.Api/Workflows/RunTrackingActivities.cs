using OmpOS.Api.Services;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Workflows;

namespace OmpOS.Api.Workflows;

/// <summary>
/// Wraps the session-run activities so a hosted run reports when its harness starts the prompt: the run is Queued until then and
/// Running afterwards. Every call is delegated unchanged; a run the <see cref="RunRegistry"/> does not know is not affected.
/// </summary>
internal sealed class RunTrackingActivities : ISessionRunActivities
{
    private readonly ISessionRunActivities _inner;
    private readonly RunRegistry _runs;

    /// <summary>Initializes a new instance of the <see cref="RunTrackingActivities"/> class.</summary>
    /// <param name="inner">The activities that do the work.</param>
    /// <param name="runs">The hosted-run index.</param>
    public RunTrackingActivities(ISessionRunActivities inner, RunRegistry runs)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(runs);
        _inner = inner;
        _runs = runs;
    }

    /// <inheritdoc />
    public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken) =>
        _inner.ProvisionSandboxAsync(input, cancellationToken);

    /// <inheritdoc />
    public Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        _runs.MarkStarted(input.RunId);
        return _inner.RunPromptAsync(input, heartbeat, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken) =>
        _inner.CollectArtifactsAsync(input, cancellationToken);

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken) =>
        _inner.TeardownSandboxAsync(input, cancellationToken);
}
