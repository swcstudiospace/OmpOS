namespace OmpOS.EngineHost;

/// <summary>
/// No-op <see cref="ISessionRecovery"/>: nothing is recoverable and discards are dropped.
/// </summary>
public sealed class NoOpSessionRecovery : ISessionRecovery
{
    /// <inheritdoc />
    public Task<IReadOnlyList<string>> ListRecoverableSessionIdsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<string>>([]);
    }

    /// <inheritdoc />
    public Task DiscardAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
