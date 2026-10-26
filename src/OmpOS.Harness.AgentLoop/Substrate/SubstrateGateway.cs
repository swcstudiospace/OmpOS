using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// The host's HTTP client for the Substrate MCP server: <c>GET /healthz</c>, <c>POST /brief</c>, <c>POST /mcp</c> (stateless
/// streamable-HTTP JSON-RPC <c>tools/call</c>), <c>POST /events</c> and <c>POST /events/query</c>. Every response body is read
/// through a 1 MiB cap. A redirect, a response from another origin, a non-2xx status, a time-out or an oversize body becomes a
/// <see cref="SubstrateUnavailableException"/> that carries no token and no response text. The bearer token is read from the
/// token provider on every request, goes only into the <c>Authorization</c> header and is never logged.
/// </summary>
public sealed class SubstrateGateway
{
    /// <summary>The largest response body the gateway reads, in bytes.</summary>
    public const int MaxResponseBytes = 1024 * 1024;

    private const string JsonAccept = "application/json";
    private const string McpAccept = "application/json, text/event-stream";
    private const string BriefAccept = "text/markdown, application/json;q=0.5";
    private const int MaxReportedSources = 20;

    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private static readonly Action<ILogger, string, SubstrateFailureReason, int, Exception?> RequestFailed =
        LoggerMessage.Define<string, SubstrateFailureReason, int>(
            LogLevel.Debug,
            new EventId(1, "SubstrateRequestFailed"),
            "Substrate request {Operation} failed: {Reason} (HTTP status {Status}, 0 when none).");

    private readonly HttpClient _httpClient;
    private readonly SubstrateEndpoint _endpoint;
    private readonly Func<string?> _tokenProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Creates a gateway over an injected HTTP client.</summary>
    /// <param name="httpClient">The client. It must not follow redirects; <see cref="CreateHttpClient"/> builds one that does not.</param>
    /// <param name="endpoint">The validated server address.</param>
    /// <param name="tokenProvider">Returns the bearer token for the next request, or null or empty to send none. It is called per request and its value is never stored or logged.</param>
    /// <param name="timeProvider">The clock that stamps health answers.</param>
    /// <param name="logger">Receives request failures as reason codes only. Null discards them.</param>
    public SubstrateGateway(HttpClient httpClient, SubstrateEndpoint endpoint, Func<string?> tokenProvider, TimeProvider timeProvider, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _httpClient = httpClient;
        _endpoint = endpoint;
        _tokenProvider = tokenProvider;
        _timeProvider = timeProvider;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Gets the endpoint this gateway talks to.</summary>
    public SubstrateEndpoint Endpoint => _endpoint;

    /// <summary>
    /// Creates an HTTP client for a gateway: no automatic redirects, no cookies and a 10 second time-out. The caller owns the
    /// client and disposes it, which also disposes its handler.
    /// </summary>
    /// <returns>The client.</returns>
    public static HttpClient CreateHttpClient()
    {
        SocketsHttpHandler? handler = new()
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
        };
        try
        {
            var client = new HttpClient(handler, disposeHandler: true) { Timeout = RequestTimeout };
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <summary>Reads <c>GET /healthz</c>. The route needs no bearer token.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>What the server reported.</returns>
    /// <exception cref="SubstrateUnavailableException">The server did not answer with a 2xx.</exception>
    public async Task<SubstrateHealth> HealthAsync(CancellationToken cancellationToken)
    {
        var reply = await SendAsync(HttpMethod.Get, "healthz", null, JsonAccept, cancellationToken).ConfigureAwait(false);
        var dto = SubstrateWire.TryParse(reply.Body, SubstrateJsonContext.Default.HealthDto);
        return new SubstrateHealth
        {
            IndexReachable = dto?.Index,
            LedgerReachable = dto?.Greptime,
            EventsWritable = dto?.EventsWritable,
            CheckedAt = _timeProvider.GetUtcNow(),
        };
    }

    /// <summary>Reads the brief through <c>POST /brief</c>.</summary>
    /// <param name="request">What to brief on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The brief text and how complete it is.</returns>
    /// <exception cref="SubstrateUnavailableException">The server did not answer with a 2xx.</exception>
    public async Task<SubstrateBrief> GetBriefAsync(SubstrateBriefRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var dto = new SubstrateWire.BriefRequestDto
        {
            Repo = request.Repo,
            Branch = request.Branch,
            GraphId = request.GraphId,
            Surface = request.Surface,
            Profile = request.Profile,
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(dto, SubstrateJsonContext.Default.BriefRequestDto);
        var reply = await SendAsync(HttpMethod.Post, "brief", body, BriefAccept, cancellationToken).ConfigureAwait(false);
        var state = string.IsNullOrWhiteSpace(reply.BriefState) ? "unknown" : SubstrateText.Sanitize(reply.BriefState, 20);
        return new SubstrateBrief
        {
            Text = Encoding.UTF8.GetString(reply.Body),
            State = state.Length == 0 ? "unknown" : state,
            FailedSources = ParseSources(reply.BriefFailed),
        };
    }

    /// <summary>
    /// Calls one MCP tool through <c>POST /mcp</c> as a stateless JSON-RPC <c>tools/call</c>. The server answers as JSON or as a
    /// single server-sent event; both are read.
    /// </summary>
    /// <param name="name">The tool name, for example <c>graph_get</c>.</param>
    /// <param name="arguments">The tool arguments as a JSON object.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The first text block of the reply and the tool-error flag.</returns>
    /// <exception cref="SubstrateUnavailableException">The server did not answer with a 2xx, or answered with a JSON-RPC error or an unreadable reply.</exception>
    public async Task<SubstrateToolResult> CallToolAsync(string name, JsonElement arguments, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var body = BuildToolCall(name, arguments);
        var reply = await SendAsync(HttpMethod.Post, "mcp", body, McpAccept, cancellationToken).ConfigureAwait(false);
        var envelope = ReadEnvelope(reply);
        if (envelope is null)
        {
            throw Fail("POST /mcp", SubstrateFailureReason.InvalidResponse, reply.StatusCode);
        }

        if (envelope.Error is not null)
        {
            throw Fail("POST /mcp", SubstrateFailureReason.ProtocolError, reply.StatusCode);
        }

        var blocks = envelope.Result?.Content;
        var text = blocks?.FirstOrDefault(static block => block is { Text: not null } && string.Equals(block.Type, "text", StringComparison.Ordinal))?.Text ?? string.Empty;
        return new SubstrateToolResult { Text = text, IsError = envelope.Result?.IsError ?? false };
    }

    /// <summary>Appends one event to the ledger through <c>POST /events</c>.</summary>
    /// <param name="emit">The event.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the ledger stored the event.</returns>
    /// <exception cref="SubstrateUnavailableException">The server did not answer with a 2xx, for example because the ledger is not configured.</exception>
    public async Task<SubstrateEmitResult> EmitEventAsync(SubstrateEventEmit emit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emit);
        if (string.IsNullOrWhiteSpace(emit.Kind) || string.IsNullOrWhiteSpace(emit.Summary))
        {
            throw new ArgumentException("The event needs a kind and a summary.", nameof(emit));
        }

        var payload = emit.Payload is null
            ? null
            : new SubstrateWire.EmitPayloadDto
            {
                Action = emit.Payload.Action,
                Item = emit.Payload.Item,
                Outcome = emit.Payload.Outcome,
                Mode = emit.Payload.Mode,
                Length = emit.Payload.Length,
            };
        var dto = new SubstrateWire.EmitRequestDto
        {
            Kind = emit.Kind,
            Summary = emit.Summary,
            SessionId = emit.SessionId,
            GraphId = emit.GraphId,
            Surface = emit.Surface,
            Actor = emit.Actor,
            Payload = payload,
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(dto, SubstrateJsonContext.Default.EmitRequestDto);
        var reply = await SendAsync(HttpMethod.Post, "events", body, JsonAccept, cancellationToken).ConfigureAwait(false);
        var parsed = SubstrateWire.TryParse(reply.Body, SubstrateJsonContext.Default.EmitReplyDto)
            ?? throw Fail("POST /events", SubstrateFailureReason.InvalidResponse, reply.StatusCode);
        return new SubstrateEmitResult { Id = SubstrateWire.AsString(parsed.Id), Stored = parsed.Stored ?? false };
    }

    /// <summary>
    /// Reads recent ledger events through <c>POST /events/query</c>, newest first. The query must name a graph or a session. Only
    /// the time, kind, actor and the declared payload action, item and outcome of each event are returned.
    /// </summary>
    /// <param name="query">The scoped query.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The events.</returns>
    /// <exception cref="ArgumentException">The query names neither a graph nor a session, or its limit is not between 1 and 100.</exception>
    /// <exception cref="SubstrateUnavailableException">The server did not answer with a 2xx, for example because the ledger is not configured.</exception>
    public async Task<SubstrateEventPage> QueryEventsAsync(SubstrateEventQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.GraphId) && string.IsNullOrWhiteSpace(query.SessionId))
        {
            throw new ArgumentException("An event query must name a graph or a session.", nameof(query));
        }

        if (query.Limit is < 1 or > 100)
        {
            throw new ArgumentException("The event query limit must be between 1 and 100.", nameof(query));
        }

        var dto = new SubstrateWire.QueryRequestDto
        {
            SessionId = string.IsNullOrWhiteSpace(query.SessionId) ? null : query.SessionId,
            GraphId = string.IsNullOrWhiteSpace(query.GraphId) ? null : query.GraphId,
            Limit = query.Limit,
            Order = "desc",
            IncludePayload = true,
        };
        var body = JsonSerializer.SerializeToUtf8Bytes(dto, SubstrateJsonContext.Default.QueryRequestDto);
        var reply = await SendAsync(HttpMethod.Post, "events/query", body, JsonAccept, cancellationToken).ConfigureAwait(false);
        var parsed = SubstrateWire.TryParse(reply.Body, SubstrateJsonContext.Default.QueryReplyDto)
            ?? throw Fail("POST /events/query", SubstrateFailureReason.InvalidResponse, reply.StatusCode);
        var events = new List<SubstrateLedgerEvent>();
        foreach (var raw in parsed.Events ?? [])
        {
            var id = SubstrateWire.AsString(raw?.Id);
            if (raw is null || string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            events.Add(new SubstrateLedgerEvent
            {
                Id = id,
                Timestamp = ParseTimestamp(SubstrateWire.AsString(raw.Ts)),
                Kind = SubstrateWire.AsString(raw.Kind),
                Actor = SubstrateWire.AsString(raw.Actor),
                Action = SubstrateWire.AsString(raw.Payload?.Action),
                Item = SubstrateWire.AsString(raw.Payload?.Item),
                Outcome = SubstrateWire.AsString(raw.Payload?.Outcome),
            });
        }

        return new SubstrateEventPage { Events = events, Truncated = parsed.Truncated ?? false };
    }

    private static DateTimeOffset? ParseTimestamp(string? text) =>
        DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var value) ? value : null;

    private static List<string> ParseSources(string? csv)
    {
        var sources = new List<string>();
        if (string.IsNullOrWhiteSpace(csv))
        {
            return sources;
        }

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var clean = SubstrateText.Sanitize(part, 40);
            if (clean.Length > 0 && sources.Count < MaxReportedSources)
            {
                sources.Add(clean);
            }
        }

        return sources;
    }

    private static byte[] BuildToolCall(string name, JsonElement arguments)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("jsonrpc", "2.0");
            writer.WriteNumber("id", 1);
            writer.WriteString("method", "tools/call");
            writer.WriteStartObject("params");
            writer.WriteString("name", name);
            writer.WritePropertyName("arguments");
            if (arguments.ValueKind == JsonValueKind.Object)
            {
                arguments.WriteTo(writer);
            }
            else
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return stream.ToArray();
    }

    /// <summary>Reads the JSON-RPC envelope from a JSON reply or from the first server-sent event that carries a result or an error.</summary>
    private static SubstrateWire.RpcEnvelopeDto? ReadEnvelope(GatewayReply reply)
    {
        if (!reply.IsEventStream)
        {
            return SubstrateWire.TryParse(reply.Body, SubstrateJsonContext.Default.RpcEnvelopeDto);
        }

        foreach (var data in EventStreamData(Encoding.UTF8.GetString(reply.Body)))
        {
            var envelope = SubstrateWire.TryParse(data, SubstrateJsonContext.Default.RpcEnvelopeDto);
            if (envelope is { Result: not null } or { Error: not null })
            {
                return envelope;
            }
        }

        return null;
    }

    /// <summary>Yields the joined <c>data:</c> lines of each server-sent event, per the SSE rules for multi-line data.</summary>
    private static IEnumerable<string> EventStreamData(string text)
    {
        var data = new StringBuilder();
        var has = false;
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            if (line.Length == 0)
            {
                if (has)
                {
                    yield return data.ToString();
                    data.Clear();
                    has = false;
                }

                continue;
            }

            if (line.StartsWith("data:", StringComparison.Ordinal))
            {
                var value = line[5..];
                if (value.StartsWith(' '))
                {
                    value = value[1..];
                }

                if (has)
                {
                    data.Append('\n');
                }

                data.Append(value);
                has = true;
            }
        }

        if (has)
        {
            yield return data.ToString();
        }
    }

    private static bool IsValidToken(string token)
    {
        foreach (var c in token)
        {
            if (c is < '\u0021' or > '\u007e')
            {
                return false;
            }
        }

        return true;
    }

    private SubstrateUnavailableException Fail(string operation, SubstrateFailureReason reason, int? statusCode)
    {
        RequestFailed(_logger, operation, reason, statusCode ?? 0, null);
        return new SubstrateUnavailableException(operation, reason, statusCode);
    }

    private async Task<GatewayReply> SendAsync(HttpMethod method, string path, byte[]? body, string accept, CancellationToken cancellationToken)
    {
        var operation = $"{method.Method} /{path}";
        using var request = new HttpRequestMessage(method, _endpoint.Resolve(path));
        request.Headers.Accept.ParseAdd(accept);
        ByteArrayContent? content = null;
        try
        {
            if (body is not null)
            {
                content = new ByteArrayContent(body);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Content = content;
            }

            var token = _tokenProvider();
            if (!string.IsNullOrEmpty(token))
            {
                if (!IsValidToken(token))
                {
                    throw Fail(operation, SubstrateFailureReason.InvalidCredential, null);
                }

                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(RequestTimeout);
            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linked.Token).ConfigureAwait(false);
                return await ReadReplyAsync(operation, response, linked.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                throw Fail(operation, SubstrateFailureReason.Timeout, null);
            }
            catch (HttpRequestException)
            {
                throw Fail(operation, SubstrateFailureReason.Transport, null);
            }
            catch (IOException)
            {
                throw Fail(operation, SubstrateFailureReason.Transport, null);
            }
        }
        finally
        {
            content?.Dispose();
        }
    }

    private async Task<GatewayReply> ReadReplyAsync(string operation, HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var status = (int)response.StatusCode;
        if (status is >= 300 and < 400)
        {
            throw Fail(operation, SubstrateFailureReason.Redirected, status);
        }

        var origin = response.RequestMessage?.RequestUri;
        if (origin is not null && !_endpoint.IsSameOrigin(origin))
        {
            throw Fail(operation, SubstrateFailureReason.OtherOrigin, status);
        }

        if (response.StatusCode is < HttpStatusCode.OK or >= HttpStatusCode.MultipleChoices)
        {
            throw Fail(operation, SubstrateFailureReason.HttpStatus, status);
        }

        if (response.Content.Headers.ContentLength is > MaxResponseBytes)
        {
            throw Fail(operation, SubstrateFailureReason.ResponseTooLarge, status);
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            while (true)
            {
                var read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                if (buffer.Length + read > MaxResponseBytes)
                {
                    throw Fail(operation, SubstrateFailureReason.ResponseTooLarge, status);
                }

                buffer.Write(chunk.AsSpan(0, read));
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            return new GatewayReply(
                status,
                buffer.ToArray(),
                string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase),
                FirstHeader(response, "X-Substrate-Brief"),
                FirstHeader(response, "X-Substrate-Brief-Failed"));
        }
    }

    private static string? FirstHeader(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var values) ? values.FirstOrDefault() : null;

    private sealed record GatewayReply(int StatusCode, byte[] Body, bool IsEventStream, string? BriefState, string? BriefFailed);
}
