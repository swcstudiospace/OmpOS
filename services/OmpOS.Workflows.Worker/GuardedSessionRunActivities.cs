using OmpOS.Contracts.Sandboxes;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Fail-loud placeholder for <see cref="ISessionRunActivities"/> used when the worker is
/// enabled without real activities registered.
/// </summary>
/// <remarks>
/// Every method throws <see cref="InvalidOperationException"/> so an operator who flips
/// <c>Workflows:Enabled=true</c> can never silently fabricate successful runs (canned
/// descriptors, <c>FinalSeq = 1</c>, zero artifacts). Set
/// <c>Workflows:EnableFakeActivities=true</c> explicitly to opt into
/// <see cref="FakeSessionRunActivities"/> for replay and smoke runs.
/// </remarks>
internal sealed class GuardedSessionRunActivities : ISessionRunActivities
{
    private const string EnableMessage = "Real session-run activities are not registered. Register an ISessionRunActivities implementation or set Workflows:EnableFakeActivities=true explicitly for smoke runs.";

    /// <inheritdoc />
    public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(EnableMessage);
    }

    /// <inheritdoc />
    public Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(EnableMessage);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(EnableMessage);
    }

    /// <inheritdoc />
    public Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken)
    {
        throw new InvalidOperationException(EnableMessage);
    }
}
