using System.Globalization;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using OmpOS.Contracts.Sandboxes;
using OmpOS.Contracts.Serialization;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.EngineHost;

/// <summary>
/// Mobile host: consumes the backend API only and never spawns a process. Prompts are submitted
/// over HTTP (<c>POST /sessions</c>, <c>POST /sessions/{id}/prompts</c>) and events stream back over
/// the resumable SSE endpoint (<c>GET /sessions/{id}/events</c>). A transport drop ends the stream
/// without throwing. Every stream starts after the delivered cursor (exclusive, sent as <c>?since=</c> and
/// <c>Last-Event-ID</c>) and de-duplicates by Seq. <see cref="ReconnectAsync"/> fetches missed events and holds them until
/// <see cref="TakeHeldEvents"/> takes them, in Seq order, each exactly once. A prompt's own stream yields only that
/// turn's events, after its POST is accepted; held events are delivered only through <see cref="TakeHeldEvents"/>.
/// </summary>
public sealed class RemoteEngineHost : IEngineHost
{
    /// <summary>The request header carrying the resume position.</summary>
    public const string LastEventIdHeader = "Last-Event-ID";

    private readonly HttpClient _http;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _activeCts;
    private string? _sessionId;

    // The single stream-owner lease: _leaseHeld while a live turn or a hold owns the session stream,
    // _leaseFreed signals waiters when it is released. All access is under _gate; see the lease methods below.
    private bool _leaseHeld;
    private TaskCompletionSource? _leaseFreed;

    /// <summary>
    /// The delivered cursor: the Seq of the last event handed to a consumer. Every stream starts after it
    /// (exclusive), so a completion that was already delivered is never replayed into a later turn.
    /// </summary>
    private long _lastSeq;

    /// <summary>
    /// The highest Seq delivered so far. Seq is monotonic per session, so any Seq at or
    /// below this mark is a replay overlap and is dropped. Only delivery moves this mark;
    /// holding a missed event merely claims its Seq (see <see cref="_heldSeqs"/>).
    /// </summary>
    private long _highWater;

    /// <summary>Seqs claimed by exactly one delivery path but not yet delivered, guarded by <see cref="_gate"/>.</summary>
    /// <remarks>A claim reserves a Seq for either the live turn or the held queue, so a replay the server sends
    /// across sequential fetches is delivered once: the second fetch skips what the first already claimed.</remarks>
    private readonly HashSet<long> _heldSeqs = new();

    /// <summary>Events fetched by <see cref="ReconnectAsync"/> that no consumer has received yet, in Seq order.</summary>
    private readonly Queue<(long? Seq, HarnessEvent Event)> _held = new();

    /// <summary>Initializes a new instance of the <see cref="RemoteEngineHost"/> class.</summary>
    /// <param name="httpClient">An HTTP client whose base address is the API origin. Not owned; not disposed.</param>
    /// <param name="descriptor">The descriptor to report, or the remote-only default.</param>
    public RemoteEngineHost(HttpClient httpClient, EngineHostDescriptor? descriptor = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        if (httpClient.BaseAddress is null)
        {
            throw new ArgumentException("The HTTP client must have BaseAddress set to the API origin.", nameof(httpClient));
        }

        _http = httpClient;
        Descriptor = descriptor ?? new EngineHostDescriptor { Kind = EngineHostKind.Remote, Isolation = Isolation.Container, Attestation = Attestation.None };
    }

    /// <inheritdoc />
    public EngineHostDescriptor Descriptor { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(
        string prompt,
        PromptOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ArgumentNullException.ThrowIfNull(options);

        // The lease makes the single stream owner atomic without blocking Dispose: a live turn waits for a
        // hold in flight (cancellably), while a resume takes the lease only when free. It is held until the
        // iterator ends, including on break or dispose, so the two paths can never overlap and delivery
        // order follows the wire.
        await AcquireLeaseAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var sessionId = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

            // H1: this turn yields only the events of the prompt submitted here, and only after its POST is
            // accepted. Held history is never replayed inline: the consumer takes it through TakeHeldEvents
            // before submitting (H4), and any replay the server still sends is skipped by Seq claim below.
            await SubmitAsync(sessionId, prompt, cancellationToken).ConfigureAwait(false);

            await foreach (var (seq, evt) in StreamAsync(sessionId, ResumePosition(), cancellationToken).ConfigureAwait(false))
            {
                MarkDelivered(seq);
                yield return evt;
            }
        }
        finally
        {
            ReleaseLease();
        }
    }

    /// <summary>
    /// Fetches the events after the delivered cursor (exclusive) and holds them, in Seq order. Raises <see cref="MissedEventsHeld"/>
    /// so the consumer can render them with <see cref="TakeHeldEvents"/>, which is the only path that delivers held events.
    /// Nothing is dropped: the cursor moves only when a consumer receives an event. When a turn is already streaming, the
    /// resume returns at once: that stream owns the session and delivers everything after the cursor itself.
    /// </summary>
    /// <param name="cancellationToken">Cancels the fetch.</param>
    /// <returns>A task that completes when the fetch ends (terminal event, server close, or transport drop).</returns>
    /// <exception cref="InvalidOperationException">No prompt has been submitted yet, so there is nothing to resume.</exception>
    public Task ReconnectAsync(CancellationToken cancellationToken = default)
    {
        string? sessionId;
        lock (_gate)
        {
            sessionId = _sessionId;
        }

        if (sessionId is null)
        {
            throw new InvalidOperationException("No session has been started; call SubmitPromptAsync before ReconnectAsync.");
        }

        return HoldMissedAsync(sessionId, ResumePosition(), cancellationToken);
    }

    /// <summary>
    /// Raised once per <see cref="ReconnectAsync"/> that held at least one missed event, on the thread that completed the fetch.
    /// Subscribers call <see cref="TakeHeldEvents"/> to render the events.
    /// </summary>
    public event EventHandler? MissedEventsHeld;

    /// <summary>
    /// Returns and clears the events held by the last resume, in Seq order. The delivered cursor moves to the last one returned,
    /// so each missed event is handed out exactly once.
    /// </summary>
    /// <returns>The held events, oldest first; empty when none are held.</returns>
    public IReadOnlyList<HarnessEvent> TakeHeldEvents()
    {
        var taken = new List<HarnessEvent>();
        while (TakeHeld() is { } held)
        {
            taken.Add(held);
        }

        return taken;
    }

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancellationTokenSource? current;
        lock (_gate)
        {
            current = _activeCts;
        }

        if (current is not null)
        {
            try
            {
                current.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The stream finished concurrently; there is nothing left to cancel.
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>Takes the session stream lease only when it is free, without waiting.</summary>
    /// <returns>True when this call now owns the lease; false when a live turn or a hold already owns it.</returns>
    private bool TryAcquireLease()
    {
        lock (_gate)
        {
            if (_leaseHeld)
            {
                return false;
            }

            _leaseHeld = true;
            return true;
        }
    }

    /// <summary>
    /// Takes the session stream lease, waiting for a hold in flight to release it. The wait honors
    /// <paramref name="cancellationToken"/>, so abandoning the caller never blocks: closing or abandoning
    /// an iterator always releases the lease in its finally, and no code path waits without that token.
    /// </summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    private async Task AcquireLeaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task? freed;
            lock (_gate)
            {
                if (!_leaseHeld)
                {
                    _leaseHeld = true;
                    return;
                }

                _leaseFreed ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                freed = _leaseFreed.Task;
            }

            await freed.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Releases the session stream lease and wakes one waiter, if any.</summary>
    private void ReleaseLease()
    {
        TaskCompletionSource? freed;
        lock (_gate)
        {
            _leaseHeld = false;
            freed = _leaseFreed;
            _leaseFreed = null;
        }

        freed?.TrySetResult();
    }

    private async Task<string> EnsureSessionAsync(CancellationToken cancellationToken)
    {
        var existing = Volatile.Read(ref _sessionId);
        if (existing is not null)
        {
            return existing;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "sessions")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var id = await ReadSessionIdAsync(response, cancellationToken).ConfigureAwait(false);

        // A concurrent caller may have won the race; either way exactly one id sticks.
        return Interlocked.CompareExchange(ref _sessionId, id, null) ?? id;
    }

    private async Task SubmitAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(new PromptBody { Prompt = prompt }, EngineHostJsonContext.Default.PromptBody);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"sessions/{Uri.EscapeDataString(sessionId)}/prompts")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async IAsyncEnumerable<(long? Seq, HarnessEvent Event)> StreamAsync(
        string sessionId,
        long? resumeFrom,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var request = BuildStreamRequest(sessionId, resumeFrom);
        CancellationTokenSource current;
        lock (_gate)
        {
            current = _activeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        try
        {
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, current.Token).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var stream = await response.Content.ReadAsStreamAsync(current.Token).ConfigureAwait(false);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            await foreach (var (id, data) in ReadFramesAsync(reader, current.Token).ConfigureAwait(false))
            {
                if (ProcessFrame(id, data) is not { } frame)
                {
                    continue;
                }

                yield return frame;
                if (frame.Event is CompletedEvent or ErrorEvent)
                {
                    break;
                }
            }
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_activeCts, current))
                {
                    _activeCts = null;
                }
            }

            current.Dispose();
        }
    }

    private static HttpRequestMessage BuildStreamRequest(string sessionId, long? resumeFrom)
    {
        var path = resumeFrom is long resume
            ? $"sessions/{Uri.EscapeDataString(sessionId)}/events?since={resume.ToString(CultureInfo.InvariantCulture)}"
            : $"sessions/{Uri.EscapeDataString(sessionId)}/events";
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        if (resumeFrom is long resumeSeq)
        {
            request.Headers.Add(LastEventIdHeader, resumeSeq.ToString(CultureInfo.InvariantCulture));
        }

        return request;
    }

    private static async IAsyncEnumerable<(string? Id, string Data)> ReadFramesAsync(
        StreamReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? frameId = null;
        var data = new StringBuilder();
        while (true)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException)
            {
                // Transport drop: end the stream without throwing; ReconnectAsync resumes from the cursor.
                break;
            }

            if (line is null)
            {
                break;
            }

            if (ConsumeLine(line, ref frameId, data))
            {
                yield return (frameId, data.ToString());
                frameId = null;
                data.Clear();
            }
        }

        // A trailing partial frame (body ended without a blank line) still dispatches, per SSE.
        if (frameId is not null || data.Length > 0)
        {
            yield return (frameId, data.ToString());
        }
    }

    /// <summary>Consumes one SSE line; returns true when a blank line dispatches the pending frame.</summary>
    private static bool ConsumeLine(string rawLine, ref string? frameId, StringBuilder data)
    {
        var line = rawLine.EndsWith('\r') ? rawLine[..^1] : rawLine;
        if (line.Length == 0)
        {
            return frameId is not null || data.Length > 0;
        }

        if (line[0] == ':')
        {
            return false;
        }

        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var field = colon < 0 ? line : line[..colon];
        var value = colon < 0 ? string.Empty : line[(colon + 1)..].TrimStart(' ');
        if (string.Equals(field, "data", StringComparison.Ordinal))
        {
            data.Append(data.Length == 0 ? value : "\n" + value);
        }
        else if (string.Equals(field, "id", StringComparison.Ordinal))
        {
            frameId = value;
        }

        return false;
    }

    /// <summary>
    /// Maps one dispatched frame onto a harness event with its Seq, or null when it must not surface
    /// (duplicate, already claimed by the other delivery path, heartbeat residue, corrupt, or unrecognized).
    /// Moves no cursor: the caller does when it hands the event to a consumer.
    /// </summary>
    private (long? Seq, HarnessEvent Event)? ProcessFrame(string? frameId, string data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        long? seq = long.TryParse(frameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;

        StreamEventPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize(data, OmpOsJsonContext.Default.StreamEventPayload);
        }
        catch (JsonException)
        {
            // Corrupt frame: skipped. A later resume may read it again and skip it again.
            return null;
        }

        var mapped = payload is null ? null : MapPayload(payload);
        if (mapped is null)
        {
            return null;
        }

        // Claiming happens after mapping, so a frame discarded above is never reserved and a later
        // resume may still deliver its Seq.
        if (seq is long known && !TryClaim(known))
        {
            return null;
        }

        return (seq, mapped);
    }

    /// <summary>Fetches the events after <paramref name="resumeFrom"/> and holds them for the next prompt.</summary>
    /// <remarks>When a live turn already holds the session lease, this returns at once: that stream owns the
    /// session and delivers everything after the cursor itself. Holding advances no cursor;
    /// <see cref="TakeHeldEvents"/> moves the delivered cursor to the last event it hands out.</remarks>
    private async Task HoldMissedAsync(string sessionId, long? resumeFrom, CancellationToken cancellationToken)
    {
        // The lease makes the single stream owner atomic: it is taken and released as one step under the
        // gate, so a resume can never start fetching alongside a live turn.
        if (!TryAcquireLease())
        {
            return;
        }

        var held = false;
        try
        {
            await foreach (var (seq, evt) in StreamAsync(sessionId, resumeFrom, cancellationToken).ConfigureAwait(false))
            {
                // The Seq was claimed in ProcessFrame; holding advances no cursor, only the queue.
                lock (_gate)
                {
                    _held.Enqueue((seq, evt));
                }

                held = true;
            }
        }
        finally
        {
            ReleaseLease();
            if (held)
            {
                MissedEventsHeld?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Takes the oldest held event and marks it delivered, or returns null when none is held.</summary>
    /// <remarks>Delivery moves both the delivered cursor and the replay mark to the taken Seq and releases its
    /// claim, so the next stream resumes after it and never replays it.</remarks>
    private HarnessEvent? TakeHeld()
    {
        lock (_gate)
        {
            if (!_held.TryDequeue(out var held))
            {
                return null;
            }

            if (held.Seq is long seq)
            {
                _lastSeq = Math.Max(_lastSeq, seq);
                _highWater = Math.Max(_highWater, seq);
                _heldSeqs.Remove(seq);
            }

            return held.Event;
        }
    }

    /// <summary>Records that a consumer received <paramref name="seq"/>: it becomes the delivered cursor.</summary>
    private void MarkDelivered(long? seq)
    {
        if (seq is not long delivered)
        {
            return;
        }

        lock (_gate)
        {
            _lastSeq = Math.Max(_lastSeq, delivered);
            _highWater = Math.Max(_highWater, delivered);
            _heldSeqs.Remove(delivered);
        }
    }

    /// <summary>The resume position for a new stream: the delivered cursor, or null (from the start) before any delivery.</summary>
    private long? ResumePosition()
    {
        lock (_gate)
        {
            return _lastSeq == 0 ? (long?)null : _lastSeq;
        }
    }

    /// <summary>
    /// Claims <paramref name="seq"/> for the calling delivery path, or returns false when it was already
    /// delivered (at or below the replay mark) or claimed by the other path.
    /// </summary>
    private bool TryClaim(long seq)
    {
        lock (_gate)
        {
            return seq > _highWater && _heldSeqs.Add(seq);
        }
    }

    private static HarnessEvent? MapPayload(StreamEventPayload payload) => payload switch
    {
        TextDeltaPayload text => new TextDeltaEvent(text.Delta),
        CompletedPayload completed => new CompletedEvent(completed.Reason),
        ErrorPayload error => new ErrorEvent(error.Message, error.Retryable),
        DiagnosticPayload diagnostic => new DiagnosticEvent(diagnostic.Message),
        // The engine never emits user_message: the client that sent the prompt echoes it locally, so there is nothing to map.
        UserMessagePayload => null,
        UiBlockPayload block => new UiBlockEvent(block.Block),
        UnknownPayload unknown => MapUnknown(unknown),
        _ => null,
    };

    private static HarnessEvent? MapUnknown(UnknownPayload unknown) => unknown.OriginalKind switch
    {
        "thought" => Field(unknown.Raw, "text") is string text ? new ThoughtEvent(text) : null,
        "tool_call" => MapToolCall(unknown.Raw),
        "tool_call_update" => MapToolCallUpdate(unknown.Raw),
        "permission_request" => MapPermissionRequest(unknown.Raw),
        "plan" => Field(unknown.Raw, "markdown") is string markdown ? new PlanEvent(markdown) : null,
        _ => null,
    };

    private static ToolCallEvent? MapToolCall(JsonElement raw)
    {
        var callId = Field(raw, "callId");
        var name = Field(raw, "name");
        var payloadJson = Field(raw, "payloadJson");
        return callId is not null && name is not null && payloadJson is not null
            ? new ToolCallEvent(callId, name, payloadJson)
            : null;
    }

    private static ToolCallUpdateEvent? MapToolCallUpdate(JsonElement raw)
    {
        var callId = Field(raw, "callId");
        var status = Field(raw, "status");
        var payloadJson = Field(raw, "payloadJson");
        return callId is not null && status is not null && payloadJson is not null
            ? new ToolCallUpdateEvent(callId, status, payloadJson)
            : null;
    }

    private static PermissionRequestEvent? MapPermissionRequest(JsonElement raw)
    {
        var callId = Field(raw, "callId");
        var name = Field(raw, "name");
        var payloadJson = Field(raw, "payloadJson");
        return callId is not null && name is not null && payloadJson is not null
            ? new PermissionRequestEvent(callId, name, payloadJson)
            : null;
    }

    private static string? Field(JsonElement raw, string name) =>
        raw.ValueKind == JsonValueKind.Object
        && raw.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static async Task<string> ReadSessionIdAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(body);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, "id", StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                return property.Value.GetString()!;
            }
        }

        throw new InvalidOperationException("The API session response did not contain a session id.");
    }
}
