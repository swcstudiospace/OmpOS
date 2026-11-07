using System.Globalization;
using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Contracts.Dispatch;
using Autonogrammer.Coordination;

namespace Autonogrammer.Api.Dispatch.Events;

/// <summary>
/// Streams one account's feed: the retained events after the resume position, then the live tail. It subscribes to <see cref="IDispatchSignals"/>
/// before its first read, so no event is missed, and it re-reads the feed at least every <see cref="PollInterval"/> so a lost signal costs one
/// interval. A position older than the retained history yields exactly one <c>resync</c> event, then the tail from the newest retained identifier.
/// </summary>
internal static class DispatchFeedStreamer
{
    /// <summary>The fallback re-read interval when no signal arrives.</summary>
    public static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>The event name of a resync.</summary>
    public const string ResyncKind = "resync";

    /// <summary>Streams the account's feed after <paramref name="afterId"/>.</summary>
    /// <param name="store">The feed store.</param>
    /// <param name="signals">The wake-up seam.</param>
    /// <param name="time">The clock the fallback interval is measured with.</param>
    /// <param name="accountId">The owner whose feed is streamed. No other account's events are read.</param>
    /// <param name="afterId">The last identifier the client has; zero starts at the beginning of the retained history.</param>
    /// <param name="cancellationToken">Ends the stream.</param>
    /// <returns>The server-sent items.</returns>
    public static async IAsyncEnumerable<SseItem<string>> SubscribeAsync(
        IDispatchStore store,
        IDispatchSignals signals,
        TimeProvider time,
        string accountId,
        long afterId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(signals);
        ArgumentNullException.ThrowIfNull(time);
        var wake = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });
        var subscription = await signals.SubscribeAsync(
            (_, _) =>
            {
                _ = wake.Writer.TryWrite(true);
                return ValueTask.CompletedTask;
            },
            cancellationToken).ConfigureAwait(false);
        await using (subscription.ConfigureAwait(false))
        {
            var position = afterId;
            var resynced = false;
            while (!cancellationToken.IsCancellationRequested)
            {
                var page = await store.ReadFeedAsync(accountId, position, cancellationToken).ConfigureAwait(false);
                var events = page.Events;
                if (page.Resync && !resynced)
                {
                    resynced = true;
                    position = events.Count == 0 ? position : events[^1].Id;
                    yield return ResyncItem(position, time);
                    events = [];
                }

                foreach (var record in events)
                {
                    position = record.Id;
                    yield return FeedItem(record);
                }

                await WaitForChangeAsync(wake, time, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static SseItem<string> FeedItem(DispatchFeedEventRecord record)
    {
        var dto = new DispatchFeedEventDto
        {
            Id = record.Id,
            Kind = record.Kind,
            At = record.At.ToUnixTimeSeconds(),
            Payload = record.Payload,
        };
        return new SseItem<string>(System.Text.Json.JsonSerializer.Serialize(dto, DispatchJsonContext.Default.DispatchFeedEventDto), record.Kind)
        {
            EventId = record.Id.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static SseItem<string> ResyncItem(long position, TimeProvider time)
    {
        var dto = new DispatchFeedEventDto
        {
            Id = position,
            Kind = ResyncKind,
            At = time.GetUtcNow().ToUnixTimeSeconds(),
        };
        return new SseItem<string>(System.Text.Json.JsonSerializer.Serialize(dto, DispatchJsonContext.Default.DispatchFeedEventDto), ResyncKind)
        {
            EventId = position.ToString(CultureInfo.InvariantCulture),
        };
    }

    private static async Task WaitForChangeAsync(Channel<bool> wake, TimeProvider time, CancellationToken cancellationToken)
    {
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var poll = Task.Delay(PollInterval, time, wait.Token);
        var woken = wake.Reader.WaitToReadAsync(wait.Token).AsTask();
        await Task.WhenAny(poll, woken).ConfigureAwait(false);
        await wait.CancelAsync().ConfigureAwait(false);
        while (wake.Reader.TryRead(out _))
        {
            // Drain the wake-ups that arrived during the wait; the feed read that follows sees them all.
        }
    }
}
