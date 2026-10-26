using System.Threading.Channels;
using StackExchange.Redis;

namespace OmpOS.Coordination.Dragonfly;

/// <summary>
/// A subscription returned by <see cref="DragonflySessionSignals"/>. Disposing it unsubscribes from Dragonfly and completes
/// the inbox. The pump checks <see cref="IsDisposed"/> before each handler invocation, so no new handler call starts after
/// disposal begins. A handler that is already running may still finish after <see cref="DisposeAsync"/> returns.
/// </summary>
internal sealed class DragonflySessionSignalSubscription : IAsyncDisposable
{
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel;
    private readonly Action<RedisChannel, RedisValue> _handler;
    private readonly ChannelWriter<string> _inbox;
    private int _disposed;

    /// <summary>Wraps an established Redis subscription and the inbox its handler fills.</summary>
    /// <param name="subscriber">The subscriber the handler was registered with.</param>
    /// <param name="channel">The subscribed channel.</param>
    /// <param name="handler">The exact handler registered, which unsubscribes this subscription and no other.</param>
    /// <param name="inbox">The writer side of the inbox the handler fills.</param>
    public DragonflySessionSignalSubscription(
        ISubscriber subscriber,
        RedisChannel channel,
        Action<RedisChannel, RedisValue> handler,
        ChannelWriter<string> inbox)
    {
        _subscriber = subscriber;
        _channel = channel;
        _handler = handler;
        _inbox = inbox;
    }

    /// <summary>Gets a value indicating whether disposal has begun. The pump reads it before every handler invocation.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Unsubscribes and completes the inbox. Safe to call more than once.</summary>
    /// <returns>A task that completes when the unsubscribe has been applied.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            await _subscriber.UnsubscribeAsync(_channel, _handler).ConfigureAwait(false);
        }
        finally
        {
            _ = _inbox.TryComplete();
        }
    }
}
