using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Autonogrammer.Harness.AgentLoop.Hooks;

/// <summary>
/// The per-session endpoint the omp hooks extension posts hook events to. It listens on the IPv4 loopback address only, on an
/// ephemeral port, and answers each request with the decision of an <see cref="IHookBridgeHandler"/>.
/// </summary>
/// <remarks>
/// <para>
/// This assembly references neither ASP.NET Core nor the plugins project, so the server is a <see cref="TcpListener"/> with a
/// minimal, strict HTTP/1.1 reader (<see cref="HookBridgeHttpParser"/> and <see cref="HookBridgeHttpReader"/>): one request
/// per connection, a <c>Content-Length</c> body, no chunked bodies, <c>Connection: close</c>. The host adapts the hook
/// dispatcher to <see cref="IHookBridgeHandler"/>.
/// </para>
/// <para>
/// Each request is checked in this order. The peer must be on the loopback interface (403, before anything is read). The head
/// must be valid and within its cap (400, 431). The bearer token must match, compared in constant time (401, empty body, so
/// nothing is learned without the token). Then the path (404), the method (405), the announced body length (411, 413) and
/// only after those the body is read, up to its announced length, and parsed (400). A request that arrives while the server
/// stops is answered with 503. Error bodies are <c>{"error":"&lt;code&gt;"}</c> and echo nothing the client sent.
/// </para>
/// <para>
/// When the handler throws, returns null or runs past <see cref="HookBridgeOptions.HandlerTimeout"/>, the answer depends on the
/// event: a fail-closed event (<see cref="HookBridgeOptions.FailClosedEvents"/>) is denied, any other is let through with
/// <see cref="BridgeDecision.FailedOpen"/> set. A decision whose permission is <c>ask</c> is resolved here through the
/// <see cref="IHookApprovalPrompt"/> before the answer is written: approved becomes allow, declined, failed or timed out
/// becomes deny, and with no prompt it is deny.
/// </para>
/// <para>
/// The server never logs a request body, a decision or the token. At debug level it logs the event name, the status and the
/// duration of each request. Requests are served concurrently, bounded by <see cref="HookBridgeOptions.MaxConcurrentRequests"/>.
/// </para>
/// </remarks>
public sealed class HookBridgeServer : IAsyncDisposable
{
    /// <summary>The path every hook request is posted to.</summary>
    public const string RequestPath = "/v1/hook";

    private const string PostMethod = "POST";
    private const string BearerPrefix = "Bearer ";
    private const string NoPromptMessage = "approval required but no approval prompt is available";
    private const string DeclinedMessage = "The user declined to approve this action.";
    private const string PromptTimedOutMessage = "The approval prompt did not answer in time, so the action was denied.";
    private const string PromptFailedMessage = "The approval prompt failed, so the action was denied.";
    private const string HandlerFailedMessage = "An Autonogrammer hook failed and this event is configured fail-closed, so the action was blocked.";
    private const string HandlerTimedOutMessage = "An Autonogrammer hook did not answer in time and this event is configured fail-closed, so the action was blocked.";
    private const string UnknownEvent = "-";
    private const string HandlerRole = "handler";
    private const string PromptRole = "approval prompt";

    private const int StateCreated = 0;
    private const int StateStarted = 1;
    private const int StateDisposed = 2;
    private const int MinHeaderBytes = 256;
    private const int MaxLoggedEventLength = 64;
    private const int LingerLimitBytes = 64 * 1024;
    private const UnixFileMode OwnerDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static readonly TimeSpan MaxTimeout = TimeSpan.FromDays(1);
    private static readonly TimeSpan AcceptRetryDelay = TimeSpan.FromMilliseconds(100);

    private static readonly JsonElement EmptyPayload = JsonSerializer.Deserialize("{}"u8, HookBridgeJsonContext.Default.JsonElement);

    private static readonly Action<ILogger, int, Exception?> Started =
        LoggerMessage.Define<int>(
            LogLevel.Information,
            new EventId(1, "HookBridgeStarted"),
            "The hook bridge listens on loopback port {Port}.");

    private static readonly Action<ILogger, string, int, double, Exception?> RequestCompleted =
        LoggerMessage.Define<string, int, double>(
            LogLevel.Debug,
            new EventId(2, "HookBridgeRequest"),
            "Hook bridge request {Event} answered {Status} in {ElapsedMs} ms.");

    private static readonly Action<ILogger, string, string, string, Exception?> GuardFailed =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(3, "HookBridgeGuardFailed"),
            "The hook bridge {Role} failed for event {Event} with {ExceptionType}.");

    private static readonly Action<ILogger, string, string, double, Exception?> GuardTimedOut =
        LoggerMessage.Define<string, string, double>(
            LogLevel.Warning,
            new EventId(4, "HookBridgeGuardTimedOut"),
            "The hook bridge {Role} did not answer event {Event} within {TimeoutMs} ms.");

    private static readonly Action<ILogger, string, Exception?> ConnectionEnded =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(5, "HookBridgeConnectionEnded"),
            "A hook bridge connection ended with {ExceptionType}.");

    private static readonly Action<ILogger, string, Exception?> ConnectionFaulted =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(6, "HookBridgeConnectionFaulted"),
            "A hook bridge connection failed unexpectedly with {ExceptionType}.");

    private static readonly Action<ILogger, string, string, Exception?> DecisionNotSerializable =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(7, "HookBridgeDecisionNotSerializable"),
            "The decision for event {Event} could not be written ({ExceptionType}); the request was answered by the failure rule.");

    private static readonly Action<ILogger, Exception?> AcceptFailed =
        LoggerMessage.Define(
            LogLevel.Warning,
            new EventId(8, "HookBridgeAcceptFailed"),
            "The hook bridge could not accept a connection and will try again.");

    private static readonly Action<ILogger, Exception?> StartupFileNotRemoved =
        LoggerMessage.Define(
            LogLevel.Debug,
            new EventId(9, "HookBridgeStartupFileNotRemoved"),
            "The hook bridge startup file could not be removed.");

    private static readonly Action<ILogger, string, Exception?> AcceptLoopFaulted =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(10, "HookBridgeAcceptLoopFaulted"),
            "The hook bridge accept loop stopped unexpectedly with {ExceptionType}; no further hook requests are served.");

    private readonly IHookBridgeHandler _handler;
    private readonly HookBridgeOptions _options;
    private readonly IHookApprovalPrompt? _prompt;
    private readonly TimeProvider _clock;
    private readonly ILogger<HookBridgeServer> _logger;
    private readonly FrozenSet<string> _failClosed;
    private readonly string? _startupFilePath;
    private readonly CancellationTokenSource _stopping = new();
    private readonly SemaphoreSlim _slots;
    private readonly ConcurrentDictionary<long, Task> _inFlight = new();
    private TcpListener? _listener;
    private Task? _acceptLoop;
    private Uri? _url;
    private string _token = string.Empty;
    private FrozenDictionary<string, string>? _environment;
    private int _state;

    /// <summary>Creates a server. Nothing is bound until <see cref="StartAsync"/> is called.</summary>
    /// <param name="handler">Runs the hooks for each request.</param>
    /// <param name="options">The limits and the startup file contents.</param>
    /// <param name="prompt">Resolves <c>ask</c> decisions. Null denies every <c>ask</c> decision.</param>
    /// <param name="clock">The clock behind the handler, read and write timeouts. The system clock when null.</param>
    /// <param name="logger">Receives request and failure diagnostics, never bodies or the token. Null discards them.</param>
    /// <exception cref="ArgumentOutOfRangeException">A limit or a timeout of the options is out of range.</exception>
    /// <exception cref="ArgumentException">The startup file path of the options is blank.</exception>
    public HookBridgeServer(
        IHookBridgeHandler handler,
        HookBridgeOptions options,
        IHookApprovalPrompt? prompt = null,
        TimeProvider? clock = null,
        ILogger<HookBridgeServer>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxRequestBytes, 1, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxHeaderBytes, MinHeaderBytes, nameof(options));
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxConcurrentRequests, 1, nameof(options));
        ValidateTimeout(options.HandlerTimeout, nameof(options));
        ValidateTimeout(options.ReadTimeout, nameof(options));
        if (options.StartupFilePath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(options.StartupFilePath, nameof(options));
        }

        _handler = handler;
        _options = options;
        _prompt = prompt;
        _clock = clock ?? TimeProvider.System;
        _logger = logger ?? NullLogger<HookBridgeServer>.Instance;
        _failClosed = options.FailClosedEvents.ToFrozenSet(StringComparer.Ordinal);
        _startupFilePath = options.StartupFilePath is null ? null : Path.GetFullPath(options.StartupFilePath);
        _slots = new SemaphoreSlim(options.MaxConcurrentRequests, options.MaxConcurrentRequests);
    }

    /// <summary>Gets the URL the extension posts to, <c>http://127.0.0.1:&lt;port&gt;/v1/hook</c>.</summary>
    /// <exception cref="InvalidOperationException">The server was not started.</exception>
    public Uri Url => _url ?? throw NotStarted();

    /// <summary>
    /// Gets the environment variables to give omp: the URL, the bearer token and, when the options name a startup file, its
    /// path, under the names in <see cref="HookBridgeEnvironment"/>. The dictionary holds the token, so hand it only to the omp
    /// process of this session and never log it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The server was not started.</exception>
    public IReadOnlyDictionary<string, string> Environment => _environment ?? throw NotStarted();

    /// <summary>
    /// Binds the loopback address on an ephemeral port, generates the token, writes the startup file atomically when the options
    /// name one, and begins to accept connections. The server then runs until it is disposed, whatever happens to
    /// <paramref name="cancellationToken"/>, which only bounds the start itself.
    /// </summary>
    /// <param name="cancellationToken">Cancels the start.</param>
    /// <returns>A task that completes when the server accepts connections.</returns>
    /// <exception cref="InvalidOperationException">The server was already started.</exception>
    /// <exception cref="ObjectDisposedException">The server was disposed.</exception>
    /// <exception cref="IOException">The startup file cannot be written. The server is then disposed and cannot be started again.</exception>
    /// <exception cref="SocketException">No loopback port can be bound. The server is then disposed and cannot be started again.</exception>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _state) == StateDisposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.CompareExchange(ref _state, StateStarted, StateCreated) != StateCreated)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _state) == StateDisposed, this);
            throw new InvalidOperationException("The hook bridge was already started.");
        }

        try
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            var listener = _listener;
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var url = new Uri(string.Create(CultureInfo.InvariantCulture, $"http://{IPAddress.Loopback}:{port}{RequestPath}"));
            var token = BridgeToken.Generate();
            if (_startupFilePath is not null)
            {
                await WriteStartupFileAsync(_startupFilePath, cancellationToken).ConfigureAwait(false);
            }

            _token = token;
            _url = url;
            _environment = BuildEnvironment(url, token);
            Started(_logger, port, null);
            _acceptLoop = AcceptLoopAsync(listener);
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Stops accepting connections, cancels every request in flight (the handler's token is cancelled and the client gets a 503),
    /// waits for the request tasks to end and deletes the startup file. Calling it again does nothing.
    /// </summary>
    /// <returns>A task that completes once the server has stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _state, StateDisposed) == StateDisposed)
        {
            return;
        }

        await _stopping.CancelAsync().ConfigureAwait(false);
        _listener?.Dispose();
        if (_acceptLoop is not null)
        {
            await Task.WhenAll(_acceptLoop).WaitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        await Task.WhenAll(_inFlight.Values).ConfigureAwait(false);
        DeleteStartupFile();
        _stopping.Dispose();
        _slots.Dispose();
    }

    /// <summary>Returns whether a peer address is on the loopback interface, including an IPv4 loopback address mapped into IPv6.</summary>
    /// <param name="address">The remote address of a connection, or null when it is unknown.</param>
    /// <returns>True for <c>127.0.0.0/8</c>, <c>::1</c> and <c>::ffff:127.0.0.0/104</c>; false for everything else, including null.</returns>
    internal static bool IsLoopback(IPAddress? address)
    {
        if (address is null)
        {
            return false;
        }

        var unmapped = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        return IPAddress.IsLoopback(unmapped);
    }

    // Names a request's event in a log line only when it looks like an event name, because the text comes from the client.
    private static string SafeEventName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > MaxLoggedEventLength)
        {
            return UnknownEvent;
        }

        foreach (var character in name)
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('_' or '-' or '.' or ':'))
            {
                return UnknownEvent;
            }
        }

        return name;
    }

    private static void ValidateTimeout(TimeSpan timeout, string parameterName)
    {
        if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
        {
            throw new ArgumentOutOfRangeException(parameterName, timeout, "A timeout must be positive and at most one day.");
        }
    }

    private static InvalidOperationException NotStarted() => new("The hook bridge was not started.");

    private static IPAddress? PeerAddress(TcpClient client) => (client.Client.RemoteEndPoint as IPEndPoint)?.Address;

    private static string? PresentedToken(string? authorization) =>
        authorization is not null && authorization.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? authorization[BearerPrefix.Length..]
            : null;

    private static BridgeDecision Deny(BridgeDecision decision, string agentMessage) =>
        decision with { Permission = BridgeDecision.PermissionDeny, AgentMessage = agentMessage };

    private static void EnsureDirectory(string directory)
    {
        if (Directory.Exists(directory))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(directory);
            return;
        }

        Directory.CreateDirectory(directory, OwnerDirectoryMode);
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The file holds event names only, so one that cannot be removed leaks nothing, and cleanup must not throw.
        }
    }

    private static (BridgeRequest? Request, int Status, string? Code) ParseRequest(HookBridgeHttpReader reader)
    {
        BridgeRequest? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(reader.Body.Span, HookBridgeJsonContext.Default.BridgeRequest);
        }
        catch (JsonException)
        {
            return (null, HookBridgeHttpResponse.BadRequest, HookBridgeHttpResponse.InvalidJsonCode);
        }
        finally
        {
            reader.ReleaseBody();
        }

        if (parsed is null || string.IsNullOrWhiteSpace(parsed.Event))
        {
            return (null, HookBridgeHttpResponse.BadRequest, HookBridgeHttpResponse.MissingEventCode);
        }

        var payload = parsed.Payload;
        switch (payload.ValueKind)
        {
            case JsonValueKind.Undefined:
            case JsonValueKind.Null:
                payload = EmptyPayload;
                break;
            case JsonValueKind.Object:
                break;
            default:
                return (null, HookBridgeHttpResponse.BadRequest, HookBridgeHttpResponse.InvalidPayloadCode);
        }

        var kind = string.IsNullOrWhiteSpace(parsed.Kind) ? BridgeRequest.CursorKind : parsed.Kind;
        return (parsed with { Kind = kind, Payload = payload }, HookBridgeHttpResponse.Ok, null);
    }

    private FrozenDictionary<string, string> BuildEnvironment(Uri url, string token)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [HookBridgeEnvironment.Url] = url.AbsoluteUri,
            [HookBridgeEnvironment.Token] = token,
        };
        if (_startupFilePath is not null)
        {
            values[HookBridgeEnvironment.StartupFile] = _startupFilePath;
        }

        return values.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private async Task WriteStartupFileAsync(string path, CancellationToken cancellationToken)
    {
        var document = new HookBridgeStartupFile
        {
            FailClosedEvents = Sorted(_options.FailClosedEvents),
            ActiveEvents = Sorted(_options.ActiveEvents),
            ActiveOmpEvents = Sorted(_options.ActiveOmpEvents),
            RequestTimeoutMs = (int)_options.HandlerTimeout.TotalMilliseconds + HookBridgeStartupFile.RequestTimeoutMarginMs,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(document, HookBridgeJsonContext.Default.HookBridgeStartupFile);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            EnsureDirectory(directory);
        }

        var temporary = path + "." + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".tmp";
        var moved = false;
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous,
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = OwnerFileMode;
            }

            var stream = new FileStream(temporary, options);
            await using (stream.ConfigureAwait(false))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, path, overwrite: true);
            moved = true;
        }
        finally
        {
            if (!moved)
            {
                DeleteQuietly(temporary);
            }
        }
    }

    private static string[] Sorted(IReadOnlySet<string> names) => [.. names.Order(StringComparer.Ordinal)];

    private void DeleteStartupFile()
    {
        if (_startupFilePath is null)
        {
            return;
        }

        try
        {
            File.Delete(_startupFilePath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            StartupFileNotRemoved(_logger, null);
        }
    }

    private async Task AcceptLoopAsync(TcpListener listener)
    {
        var stop = _stopping.Token;
        long sequence = 0;
        try
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    await _slots.WaitAsync(stop).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                if (!await AcceptOneAsync(listener, ++sequence, stop).ConfigureAwait(false))
                {
                    return;
                }
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var typeName = exception.GetType().Name;
            AcceptLoopFaulted(_logger, typeName, null);
        }
    }

    // Accepts one connection and starts serving it, which then owns the slot taken by the caller. Returns false when the
    // listener is closed and the loop must end.
    private async Task<bool> AcceptOneAsync(TcpListener listener, long id, CancellationToken stop)
    {
        TcpClient? client = null;
        try
        {
            client = await listener.AcceptTcpClientAsync(stop).ConfigureAwait(false);
            var accepted = client;
            client = null;
            var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _inFlight[id] = done.Task;
            _ = ServeAsync(accepted, id, done);
            return true;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            _slots.Release();
            return false;
        }
        catch (Exception exception) when (exception is SocketException or ObjectDisposedException or InvalidOperationException)
        {
            _slots.Release();
            if (stop.IsCancellationRequested || exception is not SocketException)
            {
                return false;
            }

            AcceptFailed(_logger, null);
            return await PauseAsync(stop).ConfigureAwait(false);
        }
        finally
        {
            client?.Dispose();
        }
    }

    private async Task<bool> PauseAsync(CancellationToken stop)
    {
        try
        {
            await Task.Delay(AcceptRetryDelay, _clock, stop).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    // Serves one connection from its first byte to its close. It never throws, and it always gives back the slot.
    private async Task ServeAsync(TcpClient client, long id, TaskCompletionSource done)
    {
        var started = _clock.GetTimestamp();
        var outcome = new RequestOutcome(HookBridgeHttpResponse.NoResponse, null);
        try
        {
            using (client)
            using (var reader = new HookBridgeHttpReader(client.GetStream(), _options.MaxHeaderBytes))
            {
                outcome = await ProcessAsync(client, reader).ConfigureAwait(false);
                if (outcome.Status != HookBridgeHttpResponse.NoResponse)
                {
                    await LingerAsync(client, reader).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException or InvalidOperationException)
        {
            var typeName = exception.GetType().Name;
            ConnectionEnded(_logger, typeName, null);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var typeName = exception.GetType().Name;
            ConnectionFaulted(_logger, typeName, null);
        }
        finally
        {
            var elapsed = _clock.GetElapsedTime(started).TotalMilliseconds;
            RequestCompleted(_logger, SafeEventName(outcome.Event), outcome.Status, elapsed, null);
            _inFlight.TryRemove(id, out _);
            _slots.Release();
            done.TrySetResult();
        }
    }

    private async Task<RequestOutcome> ProcessAsync(TcpClient client, HookBridgeHttpReader reader)
    {
        client.NoDelay = true;
        var stream = client.GetStream();
        var stop = _stopping.Token;
        if (stop.IsCancellationRequested)
        {
            return await RejectAsync(stream, HookBridgeHttpResponse.Unavailable, HookBridgeHttpResponse.SessionClosedCode).ConfigureAwait(false);
        }

        if (!IsLoopback(PeerAddress(client)))
        {
            return await RejectAsync(stream, HookBridgeHttpResponse.Forbidden, HookBridgeHttpResponse.ForbiddenCode).ConfigureAwait(false);
        }

        var (request, status, code) = await ReceiveAsync(reader, stop).ConfigureAwait(false);
        if (request is null)
        {
            return status == HookBridgeHttpResponse.NoResponse
                ? new RequestOutcome(HookBridgeHttpResponse.NoResponse, null)
                : await RejectAsync(stream, status, code).ConfigureAwait(false);
        }

        var decision = await DecideAsync(request, stop).ConfigureAwait(false);
        if (decision is null)
        {
            return await RejectAsync(stream, HookBridgeHttpResponse.Unavailable, HookBridgeHttpResponse.SessionClosedCode, request.Event).ConfigureAwait(false);
        }

        await WriteAsync(stream, HookBridgeHttpResponse.Ok, Serialize(request, decision)).ConfigureAwait(false);
        return new RequestOutcome(HookBridgeHttpResponse.Ok, request.Event);
    }

    // Reads, checks and parses one request. A null request carries the status to answer with and, except for a 401, an error
    // code; status NoResponse means the client vanished or was too slow and gets no answer.
    private async Task<(BridgeRequest? Request, int Status, string? Code)> ReceiveAsync(HookBridgeHttpReader reader, CancellationToken stop)
    {
        using var limit = new CancellationTokenSource(_options.ReadTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, limit.Token);
        try
        {
            var headStatus = await reader.ReadHeadAsync(linked.Token).ConfigureAwait(false);
            if (headStatus != HookBridgeHttpResponse.Ok)
            {
                return (null, headStatus, headStatus == HookBridgeHttpResponse.HeadersTooLarge ? HookBridgeHttpResponse.HeadersTooLargeCode : HookBridgeHttpResponse.BadRequestCode);
            }

            var rejection = Check(reader.Head!);
            if (rejection is { } failure)
            {
                return (null, failure.Status, failure.Code);
            }

            var length = (int)reader.Head!.ContentLength!.Value;
            if (!await reader.ReadBodyAsync(length, linked.Token).ConfigureAwait(false))
            {
                return (null, HookBridgeHttpResponse.NoResponse, null);
            }

            return ParseRequest(reader);
        }
        catch (OperationCanceledException)
        {
            return stop.IsCancellationRequested
                ? (null, HookBridgeHttpResponse.Unavailable, HookBridgeHttpResponse.SessionClosedCode)
                : (null, HookBridgeHttpResponse.NoResponse, null);
        }
    }

    // The checks between the head and the body, in the order the type documents. Null means the head is acceptable and the
    // body may be read.
    private (int Status, string? Code)? Check(HookBridgeHttpHead head)
    {
        if (!BridgeToken.Matches(_token, PresentedToken(head.Authorization)))
        {
            return (HookBridgeHttpResponse.Unauthorized, null);
        }

        if (!string.Equals(head.Target, RequestPath, StringComparison.Ordinal))
        {
            return (HookBridgeHttpResponse.NotFound, HookBridgeHttpResponse.NotFoundCode);
        }

        if (!string.Equals(head.Method, PostMethod, StringComparison.Ordinal))
        {
            return (HookBridgeHttpResponse.MethodNotAllowed, HookBridgeHttpResponse.MethodNotAllowedCode);
        }

        if (head.HasTransferEncoding || head.ContentLength is null)
        {
            return (HookBridgeHttpResponse.LengthRequired, HookBridgeHttpResponse.LengthRequiredCode);
        }

        if (head.ContentLength > _options.MaxRequestBytes)
        {
            return (HookBridgeHttpResponse.ContentTooLarge, HookBridgeHttpResponse.ContentTooLargeCode);
        }

        return _stopping.IsCancellationRequested
            ? (HookBridgeHttpResponse.Unavailable, HookBridgeHttpResponse.SessionClosedCode)
            : null;
    }

    // Runs the handler and resolves an ask. Null means the server is stopping and the request must be answered with a 503.
    private async Task<BridgeDecision?> DecideAsync(BridgeRequest request, CancellationToken stop)
    {
        var (state, decision) = await GuardAsync(HandlerRole, request, ct => _handler.HandleAsync(request, ct), stop).ConfigureAwait(false);
        return state switch
        {
            GuardState.Stopped => null,
            GuardState.Completed when decision is not null => await ResolveAskAsync(request, decision, stop).ConfigureAwait(false),
            _ => FailureDecision(request, state == GuardState.TimedOut),
        };
    }

    private async Task<BridgeDecision?> ResolveAskAsync(BridgeRequest request, BridgeDecision decision, CancellationToken stop)
    {
        if (!string.Equals(decision.Permission, BridgeDecision.PermissionAsk, StringComparison.OrdinalIgnoreCase))
        {
            return decision;
        }

        var prompt = _prompt;
        if (prompt is null)
        {
            return Deny(decision, NoPromptMessage);
        }

        var (state, approved) = await GuardAsync(PromptRole, request, ct => prompt.AskAsync(request, decision, ct), stop).ConfigureAwait(false);
        return state switch
        {
            GuardState.Stopped => null,
            GuardState.Completed when approved => decision with { Permission = BridgeDecision.PermissionAllow },
            GuardState.Completed => Deny(decision, DeclinedMessage),
            GuardState.TimedOut => Deny(decision, PromptTimedOutMessage),
            _ => Deny(decision, PromptFailedMessage),
        };
    }

    private BridgeDecision FailureDecision(BridgeRequest request, bool timedOut) =>
        _failClosed.Contains(request.Event)
            ? new BridgeDecision
            {
                Permission = BridgeDecision.PermissionDeny,
                AgentMessage = timedOut ? HandlerTimedOutMessage : HandlerFailedMessage,
            }
            : new BridgeDecision { FailedOpen = true };

    // Runs one piece of host code (the handler or the approval prompt) under the handler timeout and the server's stop token.
    // The wait is abandoned when either fires, even if the code ignores its token, so a stuck handler cannot hold a slot or
    // block disposal.
    private async Task<(GuardState State, T? Value)> GuardAsync<T>(
        string role,
        BridgeRequest request,
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken stop)
    {
        using var timeout = new CancellationTokenSource(_options.HandlerTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, timeout.Token);
        var call = RunGuardedAsync(role, request, work, linked.Token);
        try
        {
            var (completed, value) = await call.WaitAsync(linked.Token).ConfigureAwait(false);
            if (completed)
            {
                return (GuardState.Completed, value);
            }

            // The guarded call ends without a value both when it failed and when its token was cancelled; when the wait itself won the race against
            // the cancellation it did not throw, so the token tells which of the two happened.
            if (!linked.IsCancellationRequested)
            {
                return (GuardState.Failed, default);
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            // Stopped or timed out: decided below.
        }

        if (stop.IsCancellationRequested)
        {
            return (GuardState.Stopped, default);
        }

        var timeoutMs = _options.HandlerTimeout.TotalMilliseconds;
        GuardTimedOut(_logger, role, SafeEventName(request.Event), timeoutMs, null);
        return (GuardState.TimedOut, default);
    }

    // Never throws, so that a call whose wait was abandoned cannot fault later with nobody to observe it.
    private async Task<(bool Completed, T? Value)> RunGuardedAsync<T>(
        string role,
        BridgeRequest request,
        Func<CancellationToken, ValueTask<T>> work,
        CancellationToken cancellationToken)
    {
        try
        {
            return (true, await work(cancellationToken).ConfigureAwait(false));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (false, default);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var typeName = exception.GetType().Name;
            GuardFailed(_logger, role, SafeEventName(request.Event), typeName, null);
            return (false, default);
        }
    }

    private byte[] Serialize(BridgeRequest request, BridgeDecision decision)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(decision, HookBridgeJsonContext.Default.BridgeDecision);
        }
        catch (Exception exception) when (exception is InvalidOperationException or JsonException or NotSupportedException)
        {
            var typeName = exception.GetType().Name;
            DecisionNotSerializable(_logger, SafeEventName(request.Event), typeName, null);
            return JsonSerializer.SerializeToUtf8Bytes(FailureDecision(request, timedOut: false), HookBridgeJsonContext.Default.BridgeDecision);
        }
    }

    private async Task<RequestOutcome> RejectAsync(Stream stream, int status, string? code, string? eventName = null)
    {
        await WriteAsync(stream, status, code is null ? ReadOnlyMemory<byte>.Empty : HookBridgeHttpResponse.ErrorBody(code)).ConfigureAwait(false);
        return new RequestOutcome(status, eventName);
    }

    // Writes an answer under the read timeout, so that a client that does not read cannot hold the connection. A client that
    // went away or stalled has no use for the answer, so a failed write is not an error.
    private async Task WriteAsync(Stream stream, int status, ReadOnlyMemory<byte> body)
    {
        using var limit = new CancellationTokenSource(_options.ReadTimeout, _clock);
        try
        {
            await HookBridgeHttpResponse.WriteAsync(stream, status, body, limit.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The peer is gone or too slow; there is nobody to tell.
        }
    }

    // Closes the sending side and discards what the peer still sends, so that closing the socket does not reset a connection
    // whose peer is still reading the answer (for example after a 413, when the body was never read).
    private async Task LingerAsync(TcpClient client, HookBridgeHttpReader reader)
    {
        using var limit = new CancellationTokenSource(_options.ReadTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token, limit.Token);
        try
        {
            client.Client.Shutdown(SocketShutdown.Send);
            await reader.DrainAsync(LingerLimitBytes, linked.Token).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or SocketException or ObjectDisposedException or OperationCanceledException)
        {
            // The answer was written already, so closing now loses nothing.
        }
    }

    private enum GuardState
    {
        Completed,
        Failed,
        TimedOut,
        Stopped,
    }

    private readonly record struct RequestOutcome(int Status, string? Event);
}
