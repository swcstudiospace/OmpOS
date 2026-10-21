namespace OmpOS.Workflows;

/// <summary>
/// Direct (real-time) implementation of <see cref="IWorkflowContext"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="DateTimeOffset.UtcNow"/>, <see cref="Guid.NewGuid()"/>, and
/// <see cref="Random.Shared"/> for non-deterministic execution. Suitable for
/// the direct runner where replay determinism is not required. <see cref="DelayAsync"/>
/// carries the direct runner's retry backoff (ADR-0003, Q-WF-RETRY).
/// </remarks>
public sealed class DirectWorkflowContext : IWorkflowContext
{
    private readonly Random _random = Random.Shared;

    /// <inheritdoc />
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    /// <inheritdoc />
    public Guid NewGuid() => Guid.NewGuid();

    /// <inheritdoc />
    public Random Random => _random;

    /// <inheritdoc />
    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        return Task.Delay(delay, cancellationToken);
    }

    /// <summary>
    /// No-op by design on the direct path (ADR-0003, Q-WF-RETRY).
    /// </summary>
    /// <param name="details">The heartbeat details, discarded.</param>
    /// <remarks>
    /// A heartbeat exists to reset an activity heartbeat timeout. The direct path has no
    /// such timeout: activities run under the caller's cancellation token and their attempts
    /// are bounded by the direct runner's retry policy. Progress is observable through the
    /// session event log, so heartbeat details are not recorded here.
    /// </remarks>
    public void Heartbeat(string details)
    {
        // Intentionally empty: there is no in-process heartbeat timeout to reset.
    }
}
