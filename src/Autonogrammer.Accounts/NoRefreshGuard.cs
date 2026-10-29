namespace Autonogrammer.Accounts;

/// <summary>The guard that locks nothing.</summary>
internal sealed class NoRefreshGuard : IRefreshGuard, IAsyncDisposable
{
    /// <summary>Gets the only instance.</summary>
    public static NoRefreshGuard Instance { get; } = new();

    /// <inheritdoc />
    public ValueTask<IAsyncDisposable> AcquireAsync(CancellationToken cancellationToken) => ValueTask.FromResult<IAsyncDisposable>(this);

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
