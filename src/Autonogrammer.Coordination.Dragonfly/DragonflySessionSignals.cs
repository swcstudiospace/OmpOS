using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;

namespace Autonogrammer.Coordination.Dragonfly;

/// <summary>
/// <see cref="ISessionSignals"/> backed by the Dragonfly pub/sub channel <c>autonogrammer:signals</c>. The payload is the session
/// id as UTF-8. Delivery is asynchronous: each subscription buffers signals and hands them to its handler in order, one
/// at a time. A handler that throws affects only its own delivery. The subscription keeps delivering later signals.
/// Handlers receive <see cref="CancellationToken.None"/>. Disposing the subscription stops new deliveries; a handler that
/// is already running may finish after <c>DisposeAsync</c> returns.
/// </summary>
/// <remarks>
/// Each subscription's inbox holds at most 256 signals. When a handler falls behind, the oldest buffered signal is dropped
/// to make room. A signal only says that a session changed and carries no change data, so a dropped signal is harmless:
/// the reader is delayed until its next read, which is the same outcome as a lost signal. Publishers never wait for handlers.
/// </remarks>
public sealed class DragonflySessionSignals : ISessionSignals
{
    private readonly ISubscriber _subscriber;
    private readonly ILogger<DragonflySessionSignals> _logger;

    /// <summary>The most signals one subscription buffers before the oldest is dropped.</summary>
    private const int SignalInboxCapacity = 256;

    private static readonly Action<ILogger, RedisChannel, Exception?> HandlerFailed =
        LoggerMessage.Define<RedisChannel>(
            LogLevel.Warning,
            new EventId(1, "SessionSignalHandlerFailed"),
            "A session signal handler failed on channel {Channel}.");

    /// <summary>Creates a signal hub over the subscriber of <paramref name="multiplexer"/>.</summary>
    /// <param name="multiplexer">The connection to Dragonfly. The caller owns its lifetime.</param>
    /// <param name="logger">Receives handler failures at Warning. Null discards them.</param>
    public DragonflySessionSignals(IConnectionMultiplexer multiplexer, ILogger<DragonflySessionSignals>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(multiplexer);
        _subscriber = multiplexer.GetSubscriber();
        _logger = logger ?? NullLogger<DragonflySessionSignals>.Instance;
    }

    /// <summary>
    /// Publishes a wake-up for a session to every subscriber, in every process. Callers that publish after a write has
    /// committed should treat a failure as non-fatal: signals are best-effort.
    /// </summary>
    /// <param name="sessionId">The session that changed.</param>
    /// <param name="cancellationToken">Cancels the request before the command is sent.</param>
    public async Task PublishAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        cancellationToken.ThrowIfCancellationRequested();

        await _subscriber.PublishAsync(DragonflyKeys.SignalChannel, sessionId).ConfigureAwait(false);
    }

    /// <summary>Subscribes a handler to wake-ups. Dispose the returned handle to unsubscribe.</summary>
    /// <param name="onSignal">Called with the session id of each signal.</param>
    /// <param name="cancellationToken">Cancels the subscribe call. It does not end an established subscription.</param>
    /// <returns>A handle that unsubscribes when disposed.</returns>
    public async Task<IAsyncDisposable> SubscribeAsync(Func<string, CancellationToken, ValueTask> onSignal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onSignal);
        cancellationToken.ThrowIfCancellationRequested();

        var inbox = Channel.CreateBounded<string>(new BoundedChannelOptions(SignalInboxCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });
        var channel = DragonflyKeys.SignalChannel;
        Action<RedisChannel, RedisValue> handler = (ignoredChannel, value) => inbox.Writer.TryWrite(value.ToString());
        await _subscriber.SubscribeAsync(channel, handler).ConfigureAwait(false);

        var subscription = new DragonflySessionSignalSubscription(_subscriber, channel, handler, inbox.Writer);
        _ = PumpAsync(inbox.Reader, subscription, onSignal);
        return subscription;
    }

    /// <summary>
    /// Hands each buffered signal to the handler. Exits once the inbox is completed and drained. Signals that arrive after
    /// the subscription was disposed are dropped without invoking the handler.
    /// </summary>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Signals are best-effort. A failing handler must not end the subscription, so the failure affects only its own delivery.")]
    private async Task PumpAsync(ChannelReader<string> inbox, DragonflySessionSignalSubscription subscription, Func<string, CancellationToken, ValueTask> onSignal)
    {
        while (await inbox.WaitToReadAsync(CancellationToken.None).ConfigureAwait(false))
        {
            while (inbox.TryRead(out var sessionId))
            {
                if (subscription.IsDisposed)
                {
                    continue;
                }

                try
                {
                    await onSignal(sessionId, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // This delivery failed; the subscription keeps delivering the next signal.
                    HandlerFailed(_logger, DragonflyKeys.SignalChannel, ex);
                }
            }
        }
    }
}
