using System.Diagnostics.CodeAnalysis;
using OmpOS.Contracts.Sandboxes;
using Temporalio.Activities;

namespace OmpOS.Workflows.Worker;

/// <summary>
/// Temporal activity implementations for the session run workflow.
/// </summary>
/// <remarks>
/// Thin adapter over the shared <see cref="ISessionRunActivities"/> contract: each method
/// carries the <c>[Activity]</c> attribute so the worker can expose it, and forwards
/// heartbeats to <see cref="ActivityExecutionContext"/> for long-running prompt execution.
/// Retry policies live at the call site in <see cref="TemporalSessionRunWorkflow"/>.
/// Cancellation and heartbeats flow through the ambient
/// <see cref="ActivityExecutionContext"/> (never activity arguments, which must stay
/// serializable), while the shared contract keeps explicit tokens for the direct runner.
/// </remarks>
[SuppressMessage(
    "Design",
    "CA1515:Consider making public types internal",
    Justification = "The Temporal worker host resolves the activity type from DI and the workflow references it in activity expressions; it must stay visible outside the worker assembly.")]
public sealed class TemporalSessionRunActivities
{
    private readonly ISessionRunActivities _inner;

    /// <summary>
    /// Initializes a new instance of the <see cref="TemporalSessionRunActivities"/> class.
    /// </summary>
    /// <param name="inner">The inner activities implementation.</param>
    public TemporalSessionRunActivities(ISessionRunActivities inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Provisions a sandbox for the session run.</summary>
    /// <param name="input">The workflow input containing sandbox identifiers and idempotency key.</param>
    /// <returns>The provisioned sandbox descriptor.</returns>
    [Activity]
    public Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input)
    {
        return _inner.ProvisionSandboxAsync(input, ActivityCancellationToken);
    }

    /// <summary>Runs the user prompt in the provisioned sandbox.</summary>
    /// <param name="input">The workflow input containing the prompt and correlation identifiers.</param>
    /// <returns>The final sequence number reached in the session log.</returns>
    [Activity]
    public Task<long> RunPromptAsync(SessionRunInput input)
    {
        return _inner.RunPromptAsync(input, ReportHeartbeat, ActivityCancellationToken);
    }

    /// <summary>Collects artifacts produced during the session run.</summary>
    /// <param name="input">The workflow input containing run identifiers.</param>
    /// <returns>The collected artifact references (URIs or identifiers).</returns>
    [Activity]
    public Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input)
    {
        return _inner.CollectArtifactsAsync(input, ActivityCancellationToken);
    }

    /// <summary>Tears down the sandbox.</summary>
    /// <param name="input">The workflow input containing the sandbox identifier.</param>
    /// <returns><see langword="true"/> once teardown has completed.</returns>
    [Activity]
    public async Task<bool> TeardownSandboxAsync(SessionRunInput input)
    {
        await _inner.TeardownSandboxAsync(input, ActivityCancellationToken).ConfigureAwait(false);
        return true;
    }
    private static CancellationToken ActivityCancellationToken =>
        ActivityExecutionContext.HasCurrent
            ? ActivityExecutionContext.Current.CancellationToken
            : CancellationToken.None;

    private static void ReportHeartbeat(string details)
    {
        if (ActivityExecutionContext.HasCurrent)
        {
            ActivityExecutionContext.Current.Heartbeat(details);
        }
    }
}
