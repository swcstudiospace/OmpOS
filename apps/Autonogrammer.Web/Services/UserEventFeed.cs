using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Catalog;

namespace Autonogrammer.Web.Services;

/// <summary>
/// Browser-side consumer of <c>GET /events</c>. Frames are parsed with <see cref="SseParser"/>; a dropped connection reconnects with
/// exponential backoff (on the injected <see cref="TimeProvider"/>) and resumes with <c>Last-Event-ID</c>. Unlike the session stream it never
/// ends on a terminal event: only cancellation ends it, or the reconnect bound of <see cref="SseStreamOptions.MaxReconnects"/> when one is set.
/// </summary>
internal sealed class UserEventFeed : IUserEventFeed
{
    private readonly HttpClient _http;
    private readonly SseStreamOptions _options;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="UserEventFeed"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    /// <param name="options">The reconnect policy, or null for an unbounded policy with a 15 second ceiling.</param>
    /// <param name="time">The clock of the reconnect delays, or null for the system clock.</param>
    internal UserEventFeed(HttpClient http, SseStreamOptions? options = null, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _options = options ?? new SseStreamOptions { MaxReconnects = null, MaxRetryDelay = TimeSpan.FromSeconds(15) };
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<UserStreamEvent> StreamAsync(long? afterId, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var position = new Position(afterId);
        var reconnects = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var progressed = false;
            await foreach (var evt in ReadConnectionAsync(position, cancellationToken).ConfigureAwait(false))
            {
                progressed = true;
                yield return evt;
            }

            if (progressed)
            {
                reconnects = 0;
            }

            if (_options.MaxReconnects.HasValue && reconnects >= _options.MaxReconnects.Value)
            {
                throw new ApiClientException(
                    $"The event stream disconnected {reconnects + 1} time(s); giving up after {_options.MaxReconnects.Value} reconnect(s).");
            }

            reconnects++;
            await Task.Delay(BackoffFor(reconnects), _time, cancellationToken).ConfigureAwait(false);
        }
    }

    private static UserStreamEvent? ToEvent(SseFrame frame, Position position)
    {
        var parsedId = long.TryParse(frame.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : (long?)null;
        if (parsedId is long seen && position.Last is long last && seen <= last)
        {
            return null;
        }

        UserStreamEvent? result = null;
        try
        {
            result = frame.EventType switch
            {
                CatalogEventKinds.CatalogChanged => new UserStreamEvent(CatalogEventKinds.CatalogChanged, parsedId, JsonSerializer.Deserialize(frame.Data, CatalogJsonContext.Default.CatalogChangedEvent), null, null, null),
                CatalogEventKinds.SessionStatus => new UserStreamEvent(CatalogEventKinds.SessionStatus, parsedId, null, JsonSerializer.Deserialize(frame.Data, CatalogJsonContext.Default.SessionStatusEvent), null, null),
                CatalogEventKinds.Stage => new UserStreamEvent(CatalogEventKinds.Stage, parsedId, null, null, JsonSerializer.Deserialize(frame.Data, CatalogJsonContext.Default.StageEvent), null),
                CatalogEventKinds.Resync => new UserStreamEvent(CatalogEventKinds.Resync, parsedId, null, null, null, JsonSerializer.Deserialize(frame.Data, CatalogJsonContext.Default.CatalogResyncEvent)),
                _ => null,
            };
        }
        catch (JsonException)
        {
            result = null;
        }

        if (result is { Kind: CatalogEventKinds.Resync })
        {
            // The identifiers after a resync may come from another API process, so the old high-water mark must not drop them.
            position.Last = null;
            return result;
        }

        if (parsedId is long accepted)
        {
            position.Last = accepted;
        }

        return IsPopulated(result) ? result : null;
    }

    private static bool IsPopulated(UserStreamEvent? evt) =>
        evt is not null && (evt.Catalog is not null || evt.Status is not null || evt.Stage is not null || evt.Resync is not null);

    private static HttpRequestMessage BuildRequest(long? last)
    {
        var url = last is long position ? "events?since=" + position.ToString(CultureInfo.InvariantCulture) : "events";
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url, UriKind.Relative));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (last is long header)
        {
            request.Headers.TryAddWithoutValidation(SseEventStream.LastEventIdHeader, header.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    private async IAsyncEnumerable<UserStreamEvent> ReadConnectionAsync(Position position, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = BuildRequest(position.Last);
        HttpResponseMessage? response = null;
        try
        {
            response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // The API is unreachable: the caller backs off and reconnects.
        }

        if (response is null)
        {
            yield break;
        }

        using (response)
        {
            await ApiProblems.ThrowIfNotSuccessAsync(response, cancellationToken).ConfigureAwait(false);
            var parser = new SseParser();
            Stream? stream = null;
            try
            {
                stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                // The connection ended before the body started.
            }

            if (stream is null)
            {
                yield break;
            }

            await using (stream.ConfigureAwait(false))
            {
                using var reader = new StreamReader(stream, Encoding.UTF8);
                var buffer = new char[4096];
                while (true)
                {
                    var read = await ReadChunkAsync(reader, buffer, cancellationToken).ConfigureAwait(false);
                    if (read <= 0)
                    {
                        break;
                    }

                    foreach (var frame in parser.Feed(new string(buffer, 0, read)))
                    {
                        if (ToEvent(frame, position) is { } evt)
                        {
                            yield return evt;
                        }
                    }
                }
            }
        }
    }

    private static async Task<int> ReadChunkAsync(StreamReader reader, char[] buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            return 0;
        }
        catch (HttpRequestException)
        {
            return 0;
        }
    }

    private TimeSpan BackoffFor(int attempt)
    {
        var shift = Math.Min(attempt - 1, 10);
        var millis = _options.InitialRetryDelay.TotalMilliseconds * (1L << shift);
        return TimeSpan.FromMilliseconds(Math.Min(millis, _options.MaxRetryDelay.TotalMilliseconds));
    }

    private sealed class Position(long? last)
    {
        public long? Last { get; set; } = last;
    }
}
