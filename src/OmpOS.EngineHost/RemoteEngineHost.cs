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
/// <see cref="TakeHeldEvents"/> takes them; the next prompt delivers any still held before its own reply.
/// </summary>
public sealed class RemoteEngineHost : IEngineHost
{
    /// <summary>The request header carrying the resume position.</summary>
    public const string LastEventIdHeader = "Last-Event-ID";

    private readonly HttpClient _http;
    private readonly Lock _gate = new();
    private CancellationTokenSource? _activeCts;
    private string? _sessionId;

    /// <summary>
    /// The delivered cursor: the Seq of the last event handed to a consumer. Every stream starts after it
    /// (exclusive), so a completion that was already delivered is never replayed into a later turn.
    /// </summary>
    private long _lastSeq;

    /// <summary>
    /// The highest Seq accepted so far, delivered or held. Seq is monotonic per session, so any Seq at or
    /// below this mark is a replay overlap and is dropped.
    /// </summary>
    private long _highWater;

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
        var sessionId = await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

        // Events held by ReconnectAsync belong to earlier turns: they reach the consumer before this prompt's reply.
        while (TakeHeld() is { } held)
        {
            yield return held;
        }

        await SubmitAsync(sessionId, prompt, cancellationToken).ConfigureAwait(false);
        await foreach (var (seq, evt) in StreamAsync(sessionId, ResumePosition(), cancellationToken).ConfigureAwait(false))
        {
            MarkDelivered(seq);
            yield return evt;
        }
    }

    /// <summary>
    /// Fetches the events after the delivered cursor (exclusive) and holds them, in Seq order. Raises <see cref="MissedEventsHeld"/>
    /// so the consumer can render them with <see cref="TakeHeldEvents"/>. <see cref="SubmitPromptAsync"/> delivers any still held before its own reply.
    /// Nothing is dropped: the cursor moves only when a consumer receives an event.
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
    /// (duplicate, heartbeat residue, corrupt, or unrecognized). Moves no cursor: the caller does when it
    /// hands the event to a consumer.
    /// </summary>
    private (long? Seq, HarnessEvent Event)? ProcessFrame(string? frameId, string data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        long? seq = long.TryParse(frameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        if (seq is long known && IsDuplicate(known))
        {
            return null;
        }

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

        return (seq, mapped);
    }

    /// <summary>Fetches the events after <paramref name="resumeFrom"/> and holds them for the next prompt.</summary>
    private async Task HoldMissedAsync(string sessionId, long? resumeFrom, CancellationToken cancellationToken)
    {
        var held = false;
        try
        {
            await foreach (var (seq, evt) in StreamAsync(sessionId, resumeFrom, cancellationToken).ConfigureAwait(false))
            {
                lock (_gate)
                {
                    _held.Enqueue((seq, evt));
                    if (seq is long accepted)
                    {
                        _highWater = Math.Max(_highWater, accepted);
                    }
                }

                held = true;
            }
        }
        finally
        {
            if (held)
            {
                MissedEventsHeld?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Takes the oldest held event and marks it delivered, or returns null when none is held.</summary>
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

    private bool IsDuplicate(long seq)
    {
        lock (_gate)
        {
            return seq <= _highWater;
        }
    }


    private static HarnessEvent? MapPayload(StreamEventPayload payload) => payload switch
    {
        TextDeltaPayload text => new TextDeltaEvent(text.Delta),
        CompletedPayload completed => new CompletedEvent(completed.Reason),
        ErrorPayload error => new ErrorEvent(error.Message, error.Retryable),
        DiagnosticPayload diagnostic => new DiagnosticEvent(diagnostic.Message),
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
