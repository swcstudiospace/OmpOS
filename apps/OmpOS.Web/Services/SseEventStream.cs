using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Web.Services;

/// <summary>
/// Browser-side SSE consumer for <c>GET /sessions/{id}/events</c>: streams the response body
/// incrementally (<c>ResponseHeadersRead</c>), parses <c>id:</c>/<c>event:</c>/<c>data:</c>
/// frames, and yields each event as soon as its frame is parsed. On the OmpOS wire
/// <c>data</c> carries the <c>StreamEventPayload</c> JSON and <c>id</c> carries the Seq, so the
/// client rebuilds the <see cref="StreamEvent"/> envelope from the frame (the same approach
/// <c>RemoteEngineHost</c> uses). De-duplication is a high-water mark over the monotonic Seq,
/// reconnects resume with <c>Last-Event-ID</c> set to the last seen Seq using exponential
/// backoff, and the stream stops on the terminal event (<c>completed</c> or <c>error</c>).
/// </summary>
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "The Stream suffix names SSE streaming, matching the 04-02 plan contract; the type does not derive from System.IO.Stream.")]
internal sealed class SseEventStream
{
    /// <summary>The request header carrying the resume position.</summary>
    internal const string LastEventIdHeader = "Last-Event-ID";

    private static readonly HashSet<string> TerminalEventTypes = new(StringComparer.Ordinal)
    {
        "completed",
        "error",
    };

    private readonly HttpClient _http;
    private readonly SseStreamOptions _options;

    /// <summary>Initializes a new instance of the <see cref="SseEventStream"/> class.</summary>
    /// <param name="http">An HTTP client whose base address is the API origin.</param>
    /// <param name="options">The reconnect policy, or null for defaults.</param>
    internal SseEventStream(HttpClient http, SseStreamOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _options = options ?? new SseStreamOptions();
    }

    /// <summary>
    /// Streams the session's events from <paramref name="sinceSeq"/> (inclusive when set),
    /// transparently resuming across disconnects until the terminal event or cancellation.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="sinceSeq">The first Seq to include, or null to stream from the head.</param>
    /// <param name="cancellationToken">Cancellation token; cancelling stops the stream.</param>
    /// <returns>Each unseen event exactly once, in Seq order per connection.</returns>
    /// <exception cref="ApiClientException">The reconnect bound was reached without a terminal event.</exception>
    internal async IAsyncEnumerable<StreamEvent> StreamSessionAsync(
        string sessionId,
        long? sinceSeq = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var tracker = new SeqTracker(sinceSeq is long since ? since - 1 : null);
        var reconnects = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = new ConnectionOutcome();
            await foreach (var evt in StreamConnectionAsync(sessionId, tracker, outcome, cancellationToken).ConfigureAwait(false))
            {
                yield return evt;
            }

            if (outcome.Terminal)
            {
                yield break;
            }

            if (_options.MaxReconnects.HasValue && reconnects >= _options.MaxReconnects.Value)
            {
                throw new ApiClientException(
                    $"The event stream disconnected {reconnects + 1} time(s) without a terminal event; giving up after {_options.MaxReconnects.Value} reconnect(s).");
            }

            reconnects++;
            await Task.Delay(BackoffFor(reconnects), cancellationToken).ConfigureAwait(false);
        }
    }

    private async IAsyncEnumerable<StreamEvent> StreamConnectionAsync(
        string sessionId,
        SeqTracker tracker,
        ConnectionOutcome outcome,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = BuildRequest(sessionId, tracker.HighWater);
        var response = await SendOnceAsync(request, cancellationToken).ConfigureAwait(false);
        if (response is null)
        {
            yield break;
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new ApiClientException(
                    response.StatusCode,
                    await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false));
            }

            await foreach (var evt in ReadBodyAsync(response, sessionId, tracker, outcome, cancellationToken).ConfigureAwait(false))
            {
                yield return evt;
            }
        }
    }

    private async Task<HttpResponseMessage?> SendOnceAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            return await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private static async IAsyncEnumerable<StreamEvent> ReadBodyAsync(
        HttpResponseMessage response,
        string sessionId,
        SeqTracker tracker,
        ConnectionOutcome outcome,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Stream? stream = null;
        var loadFailed = false;
        try
        {
            stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            loadFailed = true;
        }
        catch (HttpRequestException)
        {
            loadFailed = true;
        }

        if (loadFailed || stream is null)
        {
            yield break;
        }

        var parser = new SseParser();
        await using (stream.ConfigureAwait(false))
        {
            using var reader = new StreamReader(stream, Encoding.UTF8);
            var buffer = new char[4096];
            while (!outcome.Terminal)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read;
                try
                {
                    read = await reader.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                }
                catch (IOException)
                {
                    break;
                }
                catch (HttpRequestException)
                {
                    break;
                }

                if (read == 0)
                {
                    break;
                }

                foreach (var evt in ConvertChunk(sessionId, parser, new string(buffer, 0, read), tracker, outcome))
                {
                    yield return evt;
                }
            }
        }

        foreach (var evt in ConvertTrailing(sessionId, parser, tracker, outcome))
        {
            yield return evt;
        }
    }

    private static IEnumerable<StreamEvent> ConvertChunk(
        string sessionId,
        SseParser parser,
        string chunk,
        SeqTracker tracker,
        ConnectionOutcome outcome)
    {
        foreach (var frame in parser.Feed(chunk))
        {
            var evt = ConvertFrame(sessionId, frame, tracker, out var terminal);
            if (evt is not null)
            {
                yield return evt;
            }

            if (terminal)
            {
                outcome.Terminal = true;
                yield break;
            }
        }
    }

    private static IEnumerable<StreamEvent> ConvertTrailing(
        string sessionId,
        SseParser parser,
        SeqTracker tracker,
        ConnectionOutcome outcome)
    {
        if (outcome.Terminal)
        {
            yield break;
        }

        var trailing = parser.Flush();
        if (trailing is null)
        {
            yield break;
        }

        var evt = ConvertFrame(sessionId, trailing, tracker, out var terminal);
        if (evt is not null)
        {
            yield return evt;
        }

        if (terminal)
        {
            outcome.Terminal = true;
        }
    }

    private static HttpRequestMessage BuildRequest(string sessionId, long? lastSeen)
    {
        var url = new StringBuilder($"sessions/{Uri.EscapeDataString(sessionId)}/events");
        if (lastSeen.HasValue)
        {
            url.Append("?since=").Append(lastSeen.Value.ToString(CultureInfo.InvariantCulture));
        }

        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(url.ToString(), UriKind.Relative));
        request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (lastSeen.HasValue)
        {
            request.Headers.TryAddWithoutValidation(
                LastEventIdHeader,
                lastSeen.Value.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    /// <summary>
    /// A frame is terminal when its event name or its payload says so. The payload is judged on its own, so a
    /// terminal frame stops the stream even when it carries no id or a Seq that was already delivered.
    /// </summary>
    private static bool IsTerminal(SseFrame frame, StreamEventPayload? payload) =>
        (frame.EventType is not null && TerminalEventTypes.Contains(frame.EventType))
        || payload is CompletedPayload or ErrorPayload;

    /// <summary>
    /// Converts one frame to an event, or returns null when it must not surface (corrupt, duplicate, or diagnostic).
    /// <paramref name="terminal"/> is set for every frame, independent of whether the event surfaces.
    /// </summary>
    private static StreamEvent? ConvertFrame(string sessionId, SseFrame frame, SeqTracker tracker, out bool terminal)
    {
        var payload = ParsePayload(frame.Data);
        terminal = IsTerminal(frame, payload);
        if (payload is null
            || !long.TryParse(frame.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seq)
            || !tracker.TryAdd(seq))
        {
            return null;
        }

        if (payload is DiagnosticPayload)
        {
            // Non-terminal, and the web UI has no notice path: the frame is consumed (its seq is tracked) but never shown.
            return null;
        }

        return new StreamEvent
        {
            Seq = seq,
            SessionId = sessionId,
            CorrelationId = sessionId,
            Timestamp = DateTimeOffset.UtcNow,
            Payload = payload,
        };
    }

    private static StreamEventPayload? ParsePayload(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(data, OmpOsJsonContext.Default.StreamEventPayload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The error body is best-effort context on an already-failed call; any read failure keeps the status-only exception.")]
    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private TimeSpan BackoffFor(int attempt)
    {
        var shift = Math.Min(attempt - 1, 10);
        var millis = (double)_options.InitialRetryDelay.TotalMilliseconds * (1L << shift);
        return TimeSpan.FromMilliseconds(Math.Min(millis, _options.MaxRetryDelay.TotalMilliseconds));
    }

    private sealed class ConnectionOutcome
    {
        public bool Terminal;
    }
}
