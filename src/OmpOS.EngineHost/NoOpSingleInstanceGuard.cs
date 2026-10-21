namespace OmpOS.EngineHost;

/// <summary>
/// No-op <see cref="ISingleInstanceGuard"/>: acquisition always succeeds.
/// </summary>
public sealed class NoOpSingleInstanceGuard : ISingleInstanceGuard
{
    /// <inheritdoc />
    public Task<bool> TryAcquireAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task ReleaseAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
