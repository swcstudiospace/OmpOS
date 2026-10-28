namespace Autonogrammer.Coordination;

/// <summary>A subscription returned by <see cref="InMemorySessionSignals"/>. Disposing it unsubscribes.</summary>
internal sealed class InMemorySessionSignalSubscription : IAsyncDisposable
{
    private readonly InMemorySessionSignals _owner;
    private readonly Func<string, CancellationToken, ValueTask> _onSignal;
    private int _disposed;

    /// <summary>Creates a subscription that forwards signals to <paramref name="onSignal"/>.</summary>
    /// <param name="owner">The signal hub that holds this subscription.</param>
    /// <param name="onSignal">The handler to invoke.</param>
    public InMemorySessionSignalSubscription(InMemorySessionSignals owner, Func<string, CancellationToken, ValueTask> onSignal)
    {
        _owner = owner;
        _onSignal = onSignal;
    }

    /// <summary>Invokes the handler, unless the subscription was already disposed.</summary>
    internal ValueTask DeliverAsync(string sessionId, CancellationToken cancellationToken)
    {
        return Volatile.Read(ref _disposed) != 0 ? ValueTask.CompletedTask : _onSignal(sessionId, cancellationToken);
    }

    /// <summary>Unsubscribes. Safe to call more than once.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _owner.Remove(this);
        }

        return ValueTask.CompletedTask;
    }
}
