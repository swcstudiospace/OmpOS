namespace OmpOS.Workflows;

/// <summary>
/// Direct (real-time) implementation of <see cref="IWorkflowContext"/>.
/// </summary>
/// <remarks>
/// Uses <see cref="DateTimeOffset.UtcNow"/>, <see cref="Guid.NewGuid()"/>, and
/// <see cref="Random.Shared"/> for non-deterministic execution. Suitable for
/// the direct runner where replay determinism is not required.
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

    /// <inheritdoc />
    public void Heartbeat(string details)
    {
        // Direct runner: heartbeat is a no-op (observability could be added here)
    }
}
