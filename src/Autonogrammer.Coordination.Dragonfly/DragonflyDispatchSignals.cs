using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="IDispatchSignals"/> backed by the Dragonfly pub/sub channel <c>autonogrammer:v1:sig:dispatch</c>. The payload is the
/// device id as UTF-8 and nothing else. Delivery is asynchronous: each subscription buffers signals and hands them to its
/// handler in order, one at a time. A handler that throws affects only its own delivery.
/// </summary>
/// <remarks>
/// Signals are open: when Dragonfly cannot answer, a publish is dropped and a subscribe returns a handle that delivers nothing.
/// The hub's 200 ms poll is the safety net, so an outage never fails a request. The channel is built only by
/// <see cref="DragonflyKeyspace.SignalDispatch"/>.
/// </remarks>
public sealed class DragonflyDispatchSignals : IDispatchSignals
{
    private const int SignalInboxCapacity = 256;

    private readonly ISubscriber _subscriber;
    private readonly ILogger<DragonflyDispatchSignals> _logger;
    private readonly DragonflyResilience _resilience;

    private static readonly Action<ILogger, RedisChannel, Exception?> HandlerFailed =
        LoggerMessage.Define<RedisChannel>(
            LogLevel.Warning,
            new EventId(1, "DispatchSignalHandlerFailed"),
            "A dispatch signal handler failed on channel {Channel}.");

    private static readonly Action<ILogger, string, Exception?> Unavailable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(2, "DispatchSignalsUnavailable"),
            "Dragonfly could not answer a dispatch signal {Operation}; the poll safety net applies.");

    /// <summary>Creates a signal hub over the subscriber of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="logger">Receives handler failures at Warning. Null discards them.</param>
    /// <param name="resilience">The pipeline of the connection. Null builds one from the defaults.</param>
    public DragonflyDispatchSignals(IConnectionMultiplexer multiplexer, ILogger<DragonflyDispatchSignals>? logger = null, DragonflyResilience? resilience = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger ?? NullLogger<DragonflyDispatchSignals>.Instance;
        _resilience = resilience ?? DragonflyResilience.Create();
    }

    /// <summary>Publishes a device-id hint. A failure is swallowed: the poll is the safety net.</summary>
    /// <param name="deviceId">The device that may have work waiting. Never a command.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    public async Task PublishAsync(string deviceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        cancellationToken.ThrowIfCancellationRequested();

        if (!await _resilience.TryOpenAsync(() => _subscriber.PublishAsync(DragonflyKeyspace.SignalDispatch, deviceId), cancellationToken).ConfigureAwait(false))
        {
            Unavailable(_logger, "publish", null);
        }
    }

    /// <summary>Subscribes a handler to device-id hints. Dispose the returned handle to unsubscribe.</summary>
    /// <param name="onSignal">Called with the device id of each signal.</param>
    /// <param name="cancellationToken">Cancels the subscribe call. It does not end an established subscription.</param>
    /// <returns>A handle that unsubscribes when disposed. When Dragonfly cannot answer, the handle delivers nothing.</returns>
    public async Task<IAsyncDisposable> SubscribeAsync(Func<string, CancellationToken, ValueTask> onSignal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onSignal);
        cancellationToken.ThrowIfCancellationRequested();

        var inbox = Channel.CreateBounded<string>(new BoundedChannelOptions(SignalInboxCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var channel = DragonflyKeyspace.SignalDispatch;
        Action<RedisChannel, RedisValue> handler = (ignoredChannel, value) => inbox.Writer.TryWrite(value.ToString());
        if (!await _resilience.TryOpenAsync(() => _subscriber.SubscribeAsync(channel, handler), cancellationToken).ConfigureAwait(false))
        {
            Unavailable(_logger, "subscribe", null);
            _ = inbox.Writer.TryComplete();
            return NoSubscription.Instance;
        }

        var subscription = new Subscription(_subscriber, channel, handler, inbox.Writer);
        _ = PumpAsync(inbox.Reader, subscription, onSignal);
        return subscription;
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Signals are best-effort. A failing handler must not end the subscription, so the failure affects only its own delivery.")]
    private async Task PumpAsync(ChannelReader<string> inbox, Subscription subscription, Func<string, CancellationToken, ValueTask> onSignal)
    {
        while (await inbox.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            while (inbox.TryRead(out var deviceId))
            {
                if (subscription.IsDisposed)
                {
                    continue;
                }

                try
                {
                    await onSignal(deviceId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    HandlerFailed(_logger, DragonflyKeyspace.SignalDispatch, ex);
                }
            }
        }
    }

    private sealed class Subscription : IAsyncDisposable
    {
        private readonly ISubscriber _subscriber;
        private readonly RedisChannel _channel;
        private readonly Action<RedisChannel, RedisValue> _handler;
        private readonly ChannelWriter<string> _inbox;
        private int _disposed;

        public Subscription(ISubscriber subscriber, RedisChannel channel, Action<RedisChannel, RedisValue> handler, ChannelWriter<string> inbox)
        {
            _subscriber = subscriber;
            _channel = channel;
            _handler = handler;
            _inbox = inbox;
        }

        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

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
}
