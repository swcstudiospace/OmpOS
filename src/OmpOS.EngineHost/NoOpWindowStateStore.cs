namespace OmpOS.EngineHost;

/// <summary>
/// No-op <see cref="IWindowStateStore"/>: loads nothing and discards saves.
/// </summary>
public sealed class NoOpWindowStateStore : IWindowStateStore
{
    /// <inheritdoc />
    public Task<WindowState?> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<WindowState?>(null);
    }

    /// <inheritdoc />
    public Task SaveAsync(WindowState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}
