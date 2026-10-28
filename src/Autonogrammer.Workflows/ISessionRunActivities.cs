using Autonogrammer.Contracts.Sandboxes;

namespace Autonogrammer.Workflows;

/// <summary>
/// Activities for the session run workflow (provision, run prompt, collect, teardown).
/// </summary>
/// <remarks>
/// Activities are executed by the runner and contain the
/// effectful operations. The workflow orchestrates them with teardown in a finally block.
/// </remarks>
public interface ISessionRunActivities
{
    /// <summary>
    /// Provisions a sandbox for the session run.
    /// </summary>
    /// <param name="input">The workflow input containing sandbox identifiers and idempotency key.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The provisioned sandbox descriptor.</returns>
    Task<SandboxDescriptor> ProvisionSandboxAsync(SessionRunInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the user prompt in the provisioned sandbox, streaming events to the session log.
    /// </summary>
    /// <param name="input">The workflow input containing the prompt and correlation identifiers.</param>
    /// <param name="heartbeat">Callback to record heartbeat details (e.g., last sequence number processed).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The final sequence number reached in the session log.</returns>
    Task<long> RunPromptAsync(SessionRunInput input, Action<string> heartbeat, CancellationToken cancellationToken);

    /// <summary>
    /// Collects artifacts produced during the session run.
    /// </summary>
    /// <param name="input">The workflow input containing run identifiers.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A list of artifact references (URIs or identifiers).</returns>
    Task<IReadOnlyList<string>> CollectArtifactsAsync(SessionRunInput input, CancellationToken cancellationToken);

    /// <summary>
    /// Tears down the sandbox. Called in a finally block to guarantee cleanup.
    /// </summary>
    /// <param name="input">The workflow input containing the sandbox identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task representing the teardown operation.</returns>
    Task TeardownSandboxAsync(SessionRunInput input, CancellationToken cancellationToken);
}
