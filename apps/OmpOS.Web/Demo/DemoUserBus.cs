using System.Text.Json;
using System.Threading.Channels;
using OmpOS.Contracts.Catalog;

namespace OmpOS.Web.Demo;

/// <summary>
/// The Demo's <c>GET /events</c>: it numbers the catalogue, status and stage events, keeps the last 1024 for <c>Last-Event-ID</c> resume and
/// answers a position it cannot serve (newer than the head, or older than the history) with one <c>resync</c> event, like the real API.
/// </summary>
internal sealed class DemoUserBus
{
    /// <summary>The number of events kept for resume.</summary>
    internal const int HistoryLimit = 1024;

    private readonly List<DemoUserFrame> _history = [];
    private readonly List<Channel<DemoUserFrame>> _subscribers = [];
    private long _lastId;

    /// <summary>Publishes an event to every subscriber.</summary>
    /// <param name="kind">The SSE event name.</param>
    /// <param name="json">The one-line JSON data.</param>
    internal void Publish(string kind, string json)
    {
        var frame = new DemoUserFrame(++_lastId, kind, json);
        _history.Add(frame);
        if (_history.Count > HistoryLimit)
        {
            _history.RemoveAt(0);
        }

        foreach (var subscriber in _subscribers.ToList())
        {
            _ = subscriber.Writer.TryWrite(frame);
        }
    }

    /// <summary>Opens a subscription.</summary>
    /// <param name="afterId">The last identifier the client saw, or null for the live tail.</param>
    /// <returns>A channel that first holds the replay (or one <c>resync</c> frame), then every later event.</returns>
    internal Channel<DemoUserFrame> Subscribe(long? afterId)
    {
        var channel = Channel.CreateUnbounded<DemoUserFrame>(new UnboundedChannelOptions { SingleReader = true });
        if (afterId is { } position)
        {
            var oldest = _history.Count == 0 ? _lastId : _history[0].Id!.Value;
            if (position > _lastId || position < oldest - 1)
            {
                var json = JsonSerializer.Serialize(new CatalogResyncEvent { Reason = "The position is not in this server's history." }, CatalogJsonContext.Default.CatalogResyncEvent);
                _ = channel.Writer.TryWrite(new DemoUserFrame(null, CatalogEventKinds.Resync, json));
            }
            else
            {
                foreach (var frame in _history.Where(frame => frame.Id > position))
                {
                    _ = channel.Writer.TryWrite(frame);
                }
            }
        }

        _subscribers.Add(channel);
        return channel;
    }

    /// <summary>Closes a subscription.</summary>
    /// <param name="channel">The channel <see cref="Subscribe"/> returned.</param>
    internal void Unsubscribe(Channel<DemoUserFrame> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        _ = _subscribers.Remove(channel);
        _ = channel.Writer.TryComplete();
    }
}
