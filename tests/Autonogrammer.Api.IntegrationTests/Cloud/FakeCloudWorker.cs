using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>
/// A minimal in-process agent worker: a loopback WebSocket server that speaks the bridge protocol (one JSON-RPC message per text frame), checks the
/// bearer token the way a worker does (ES256, issuer, audience), and plays an ACP agent: <c>initialize</c>, <c>session/new</c>, <c>session/load</c>,
/// <c>session/prompt</c> with streamed <c>agent_message_chunk</c> updates, <c>session/cancel</c> and <c>session/close</c>. It records what it saw.
/// It does not reference the real worker project.
/// </summary>
internal sealed class FakeCloudWorker : IAsyncDisposable
{
    private const string InitializeResult =
        "{\"protocolVersion\":1,\"agentInfo\":{\"name\":\"omp\",\"title\":\"omp\",\"version\":\"18.12.3\"},\"agentCapabilities\":{\"loadSession\":true,\"promptCapabilities\":{\"embeddedContext\":true}}}";

    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly SecurityKey _verificationKey;
    private readonly TimeProvider _clock;
    private readonly ConcurrentBag<Task> _connections = [];
    private readonly Task _acceptLoop;
    private int _sessionCounter;
    private WebSocket? _heldSocket;
    private JsonNode? _heldId;
    private string? _heldSession;
    private string? _heldPrompt;
    private readonly TaskCompletionSource _promptHeld = NewSignal();

    /// <summary>Initializes a new instance of the <see cref="FakeCloudWorker"/> class and starts listening on a free loopback port.</summary>
    /// <param name="verificationKey">The public key worker tokens must verify with.</param>
    /// <param name="clock">The clock the tokens were issued by, so their lifetime is judged on the same time.</param>
    public FakeCloudWorker(SecurityKey verificationKey, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(verificationKey);
        ArgumentNullException.ThrowIfNull(clock);
        _verificationKey = verificationKey;
        _clock = clock;
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        Port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_stop.Token));
    }

    /// <summary>Gets the loopback port.</summary>
    public int Port { get; }

    /// <summary>Gets the <c>ws</c> endpoint of the bridge.</summary>
    public Uri Endpoint => new($"ws://127.0.0.1:{Port}/acp");

    /// <summary>Gets or sets how the worker answers a prompt: the text chunks it streams. Defaults to one chunk that echoes the prompt.</summary>
    public Func<string, IReadOnlyList<string>> OnPrompt { get; set; } = static prompt => ["echo: " + prompt];

    /// <summary>Gets or sets extra raw <c>session/update</c> payloads (the <c>update</c> member) sent after the text chunks.</summary>
    public Func<string, IReadOnlyList<string>>? OnPromptUpdates { get; set; }

    /// <summary>Gets or sets whether <c>session/load</c> is refused, as when the worker's volume no longer has the engine session.</summary>
    public bool RejectLoad { get; set; }

    /// <summary>Gets or sets whether a prompt is held until <see cref="ReleaseHeldPromptAsync"/> or a cancel arrives.</summary>
    public bool HoldPrompts { get; set; }

    /// <summary>Gets the <c>Authorization</c> header of every connection attempt, in order.</summary>
    public ConcurrentQueue<string> Authorizations { get; } = new();

    /// <summary>Gets the validated claims of every accepted token, in order.</summary>
    public ConcurrentQueue<IDictionary<string, object>> TokenClaims { get; } = new();

    /// <summary>Gets the prompts received, in order.</summary>
    public ConcurrentQueue<string> Prompts { get; } = new();

    /// <summary>Gets the identifiers of the engine sessions this worker created.</summary>
    public ConcurrentQueue<string> NewSessions { get; } = new();

    /// <summary>Gets the engine session identifiers the client asked to load.</summary>
    public ConcurrentQueue<string> LoadedSessions { get; } = new();

    /// <summary>Gets the engine sessions a cancel was received for.</summary>
    public ConcurrentQueue<string> Cancels { get; } = new();

    /// <summary>Gets how many connection attempts were served, accepted or refused.</summary>
    public int Connections => _connections.Count;

    /// <summary>Gets a task that completes when a held prompt has arrived.</summary>
    public Task PromptHeld => _promptHeld.Task.WaitAsync(CancellationToken.None);

    /// <summary>Answers the held prompt as if the model finished: streams the answer and ends the turn.</summary>
    /// <param name="cancellationToken">Cancels the send.</param>
    /// <returns>A task that completes when the answer is sent.</returns>
    public async Task ReleaseHeldPromptAsync(CancellationToken cancellationToken)
    {
        var socket = _heldSocket ?? throw new InvalidOperationException("No prompt is held.");
        var id = _heldId;
        var session = _heldSession!;
        var prompt = _heldPrompt!;
        _heldSocket = null;
        await StreamAnswerAsync(socket, session, prompt, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, Reply(id, "{\"stopReason\":\"end_turn\"}"), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Close();
        try
        {
            await _acceptLoop.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
            // The listener was closed under the accept loop: that is the shutdown.
        }

        foreach (var connection in _connections)
        {
            try
            {
                await connection.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or WebSocketException or OperationCanceledException or ObjectDisposedException or IOException)
            {
                // The connection ended with the listener.
            }
        }

        _stop.Dispose();
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static string Reply(JsonNode? id, string resultJson) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = JsonNode.Parse(resultJson) }.ToJsonString();

    private static string ReplyError(JsonNode? id, int code, string message) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["error"] = new JsonObject { ["code"] = code, ["message"] = message } }.ToJsonString();

    private static string Update(string session, JsonNode update) =>
        new JsonObject { ["jsonrpc"] = "2.0", ["method"] = "session/update", ["params"] = new JsonObject { ["sessionId"] = session, ["update"] = update } }.ToJsonString();

    private static Task SendAsync(WebSocket socket, string text, CancellationToken cancellationToken) =>
        socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, endOfMessage: true, cancellationToken);

    private static async Task<string?> ReceiveAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[16 * 1024];
        using var collected = new MemoryStream();
        while (true)
        {
            var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return null;
            }

            await collected.WriteAsync(buffer.AsMemory(0, result.Count), cancellationToken).ConfigureAwait(false);
            if (result.EndOfMessage)
            {
                return Encoding.UTF8.GetString(collected.ToArray());
            }
        }
    }

    private static string PromptText(JsonNode? parameters)
    {
        var blocks = parameters?["prompt"]?.AsArray();
        return blocks is null ? string.Empty : string.Concat(blocks.Select(static block => block?["text"]?.GetValue<string>()));
    }

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var context = await _listener.GetContextAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
            _connections.Add(Task.Run(() => ServeAsync(context, cancellationToken), CancellationToken.None));
        }
    }

    private async Task ServeAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var authorization = context.Request.Headers["Authorization"] ?? string.Empty;
        Authorizations.Enqueue(authorization);
        if (!context.Request.IsWebSocketRequest || !string.Equals(context.Request.Url?.AbsolutePath, "/acp", StringComparison.Ordinal))
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        var token = authorization.StartsWith("Bearer ", StringComparison.Ordinal) ? authorization["Bearer ".Length..] : string.Empty;
        var validated = await new JsonWebTokenHandler().ValidateTokenAsync(
            token,
            new TokenValidationParameters
            {
                ValidIssuer = "autonogrammer-api",
                ValidAudience = "autonogrammer-worker",
                IssuerSigningKey = _verificationKey,
                ValidAlgorithms = [SecurityAlgorithms.EcdsaSha256],
                LifetimeValidator = TestTokenLifetime.Of(_clock),
            }).ConfigureAwait(false);
        if (!validated.IsValid)
        {
            context.Response.StatusCode = 401;
            context.Response.Close();
            return;
        }

        TokenClaims.Enqueue(validated.Claims);
        var socketContext = await context.AcceptWebSocketAsync(subProtocol: null).ConfigureAwait(false);
        using var socket = socketContext.WebSocket;
        try
        {
            await ConversationAsync(socket, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // The client dropped or the worker is stopping.
        }
    }

    private async Task ConversationAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        JsonNode? pendingId = null;
        while (socket.State == WebSocketState.Open)
        {
            var text = await ReceiveAsync(socket, cancellationToken).ConfigureAwait(false);
            if (text is null)
            {
                return;
            }

            var node = JsonNode.Parse(text)!;
            var method = node["method"]?.GetValue<string>();
            var id = node["id"];
            var parameters = node["params"];
            switch (method)
            {
                case "initialize":
                    await SendAsync(socket, Reply(id, InitializeResult), cancellationToken).ConfigureAwait(false);
                    break;
                case "session/new":
                    var created = "acp-" + Interlocked.Increment(ref _sessionCounter).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    NewSessions.Enqueue(created);
                    await SendAsync(socket, Reply(id, "{\"sessionId\":\"" + created + "\"}"), cancellationToken).ConfigureAwait(false);
                    break;
                case "session/load":
                    LoadedSessions.Enqueue(parameters?["sessionId"]?.GetValue<string>() ?? string.Empty);
                    await SendAsync(socket, RejectLoad ? ReplyError(id, -32602, "Session not found") : Reply(id, "{}"), cancellationToken).ConfigureAwait(false);
                    break;
                case "session/prompt":
                    pendingId = await OnPromptAsync(socket, id, parameters, cancellationToken).ConfigureAwait(false);
                    break;
                case "session/cancel":
                    Cancels.Enqueue(parameters?["sessionId"]?.GetValue<string>() ?? string.Empty);
                    if (pendingId is not null)
                    {
                        _heldSocket = null;
                        await SendAsync(socket, Reply(pendingId, "{\"stopReason\":\"cancelled\"}"), cancellationToken).ConfigureAwait(false);
                        pendingId = null;
                    }

                    break;
                default:
                    if (id is not null)
                    {
                        await SendAsync(socket, Reply(id, "{}"), cancellationToken).ConfigureAwait(false);
                    }

                    break;
            }
        }
    }

    private async Task<JsonNode?> OnPromptAsync(WebSocket socket, JsonNode? id, JsonNode? parameters, CancellationToken cancellationToken)
    {
        var session = parameters?["sessionId"]?.GetValue<string>() ?? string.Empty;
        var prompt = PromptText(parameters);
        Prompts.Enqueue(prompt);
        if (HoldPrompts)
        {
            _heldSocket = socket;
            _heldId = id?.DeepClone();
            _heldSession = session;
            _heldPrompt = prompt;
            _promptHeld.TrySetResult();
            return id?.DeepClone();
        }

        await StreamAnswerAsync(socket, session, prompt, cancellationToken).ConfigureAwait(false);
        await SendAsync(socket, Reply(id, "{\"stopReason\":\"end_turn\"}"), cancellationToken).ConfigureAwait(false);
        return null;
    }

    private async Task StreamAnswerAsync(WebSocket socket, string session, string prompt, CancellationToken cancellationToken)
    {
        foreach (var chunk in OnPrompt(prompt))
        {
            var update = new JsonObject
            {
                ["sessionUpdate"] = "agent_message_chunk",
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = chunk },
            };
            await SendAsync(socket, Update(session, update), cancellationToken).ConfigureAwait(false);
        }

        foreach (var raw in OnPromptUpdates?.Invoke(prompt) ?? [])
        {
            await SendAsync(socket, Update(session, JsonNode.Parse(raw)!), cancellationToken).ConfigureAwait(false);
        }
    }
}
