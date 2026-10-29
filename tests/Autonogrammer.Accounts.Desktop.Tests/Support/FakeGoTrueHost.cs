using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts.Desktop.Tests.Support;

/// <summary>
/// A loopback HTTP/1.1 fake of the six GoTrue routes over raw <see cref="TcpListener"/> sockets (one request per connection, <c>Connection: close</c>). It is
/// not a Kestrel host: this test project has no ASP.NET reference (D-61). <see cref="GoTrueOptions.Create(string?, string?)"/> keeps requiring https, so the
/// client from <see cref="CreateClient"/> rewrites the scheme and authority of each request to this socket before a real <see cref="SocketsHttpHandler"/>
/// sends it. State is scripted the way the in-process fake of the Accounts tests does: one-time codes, S256 checking, refresh rotation with a reuse interval
/// on the supplied clock, lost responses, held refreshes and one-shot errors.
/// </summary>
internal sealed class FakeGoTrueHost : IAsyncDisposable
{
    /// <summary>The https project address the options carry.</summary>
    public const string ProjectAddress = "https://fake-project.supabase.co";

    private const string PublishableKey = "sb_publishable_FakeGoTrueHostKey0000000";
    private const int MaxRequestBytes = 64 * 1024;

    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly TimeProvider _time;
    private readonly CancellationTokenSource _stop = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, string> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Flow> _flows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Flow> _lastFlowByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RefreshRecord> _refreshTokens = new(StringComparer.Ordinal);
    private readonly List<string> _calls = [];
    private readonly List<string> _presentedRefreshTokens = [];
    private readonly List<(string Challenge, string RedirectTo)> _linkRequests = [];
    private readonly Queue<(string Route, int Status, string ErrorCode)> _scriptedErrors = new();
    private readonly Task _acceptLoop;
    private TaskCompletionSource? _refreshGate;
    private int _dropRefreshResponses;
    private int _counter;

    /// <summary>Initializes a new instance of the <see cref="FakeGoTrueHost"/> class and starts listening.</summary>
    /// <param name="time">The clock that decides code, token and reuse expiry.</param>
    /// <param name="magicLink">Whether the project offers the magic link next to the code.</param>
    public FakeGoTrueHost(TimeProvider time, bool magicLink = true)
    {
        _time = time;
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        Options = GoTrueOptions.Create(new AuthConfigResponse(
            "oidc",
            ProjectAddress + "/auth/v1",
            "authenticated",
            new Uri(ProjectAddress),
            PublishableKey,
            new AuthMethods(EmailCode: true, MagicLink: magicLink),
            new AuthEmailCode(ExpirySeconds: 3600, ResendCooldownSeconds: 60),
            SignupEmailHasCode: false)).Options!;
        _acceptLoop = Task.Run(AcceptLoopAsync);
    }

    /// <summary>Gets the ephemeral loopback port of the fake.</summary>
    public int Port { get; }

    /// <summary>Gets the options a client of this fake uses.</summary>
    public GoTrueOptions Options { get; }

    /// <summary>Gets or sets how long a consumed refresh token may be presented again and still be answered with its successor.</summary>
    public TimeSpan ReuseInterval { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Gets the refresh tokens clients presented, in order.</summary>
    public IReadOnlyList<string> PresentedRefreshTokens
    {
        get
        {
            lock (_gate)
            {
                return [.. _presentedRefreshTokens];
            }
        }
    }

    /// <summary>Gets the code challenge and <c>redirect_to</c> of every request for a link, in order.</summary>
    public IReadOnlyList<(string Challenge, string RedirectTo)> LinkRequests
    {
        get
        {
            lock (_gate)
            {
                return [.. _linkRequests];
            }
        }
    }

    /// <summary>Creates an HTTP client that sends to this fake. Dispose it with the test.</summary>
    /// <returns>The client.</returns>
    public HttpClient CreateClient()
    {
        RewriteHandler? handler = new(Port);
        try
        {
            var client = new HttpClient(handler, disposeHandler: true);
            handler = null;
            return client;
        }
        finally
        {
            handler?.Dispose();
        }
    }

    /// <summary>Counts the requests seen for a route: <c>otp</c>, <c>verify</c>, <c>token:pkce</c>, <c>token:refresh</c>, <c>logout</c> or <c>user</c>.</summary>
    /// <param name="route">The route.</param>
    /// <returns>The count.</returns>
    public int Count(string route)
    {
        lock (_gate)
        {
            return _calls.Count(call => string.Equals(call, route, StringComparison.Ordinal));
        }
    }

    /// <summary>Creates a user with a live session, as a sign-in on another device would.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The session as the service would issue it.</returns>
    public GoTrueSession IssueSession(string email)
    {
        lock (_gate)
        {
            return ToSession(NewSession(email));
        }
    }

    /// <summary>Reads the code of the last email to an address.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The code.</returns>
    public string LastCode(string email)
    {
        lock (_gate)
        {
            return _codes[email];
        }
    }

    /// <summary>Gives the address a person's click on the emailed link would open: the <c>redirect_to</c> of the last link request plus the auth code.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The callback address.</returns>
    public Uri LinkFor(string email)
    {
        lock (_gate)
        {
            Flow flow = _lastFlowByEmail[email];
            return new Uri(flow.RedirectTo + "?code=" + Uri.EscapeDataString(flow.AuthCode), UriKind.Absolute);
        }
    }

    /// <summary>Answers the next request to a route with an error body instead of handling it.</summary>
    /// <param name="route">The route.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="errorCode">The <c>error_code</c> of the body.</param>
    public void FailNext(string route, int status, string errorCode)
    {
        lock (_gate)
        {
            _scriptedErrors.Enqueue((route, status, errorCode));
        }
    }

    /// <summary>Makes the next refresh be handled (the token is spent) but closes the connection without a response.</summary>
    public void DropNextRefreshResponse() => _ = Interlocked.Increment(ref _dropRefreshResponses);

    /// <summary>Holds every refresh request, after it was counted and before it is handled, until <see cref="ReleaseRefresh"/>.</summary>
    public void HoldRefresh()
    {
        lock (_gate)
        {
            _refreshGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Lets the held refresh requests go on.</summary>
    public void ReleaseRefresh()
    {
        TaskCompletionSource? gate;
        lock (_gate)
        {
            gate = _refreshGate;
            _refreshGate = null;
        }

        _ = gate?.TrySetResult();
    }

    /// <summary>Stops listening.</summary>
    /// <returns>A task that completes once the accept loop ended.</returns>
    public async ValueTask DisposeAsync()
    {
        ReleaseRefresh();
        await _stop.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        _listener.Dispose();
        await _acceptLoop.WaitAsync(CancellationToken.None).ConfigureAwait(false);
        _stop.Dispose();
    }

    private static string Base64UrlSha256(string value) => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(value))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string RouteOf(string path, string query)
    {
        string name = path[(path.LastIndexOf('/') + 1)..];
        return string.Equals(name, "token", StringComparison.Ordinal) ? "token:" + (query.Contains("grant_type=pkce", StringComparison.Ordinal) ? "pkce" : "refresh") : name;
    }

    private static string Quote(string value) => "\"" + JsonEncodedText.Encode(value).ToString() + "\"";

    private static Answer Error(int status, string errorCode) =>
        new(status, "{\"code\":" + status.ToString(CultureInfo.InvariantCulture) + ",\"error_code\":" + Quote(errorCode) + ",\"msg\":\"fake\"}");

    private static string QueryValue(string query, string key)
    {
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0 && string.Equals(pair[..equals], key, StringComparison.Ordinal))
            {
                return Uri.UnescapeDataString(pair[(equals + 1)..]);
            }
        }

        return string.Empty;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(_stop.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            _ = ServeSafeAsync(client);
        }
    }

    private async Task ServeSafeAsync(TcpClient client)
    {
        try
        {
            using (client)
            {
                await ServeAsync(client).ConfigureAwait(false);
            }
        }
        catch (IOException)
        {
            // The client went away.
        }
        catch (SocketException)
        {
            // The client went away.
        }
        catch (OperationCanceledException)
        {
            // The fake is shutting down.
        }
        catch (ObjectDisposedException)
        {
            // The fake is shutting down.
        }
    }

    private async Task ServeAsync(TcpClient client)
    {
        NetworkStream stream = client.GetStream();
        (string method, string target, string body) = await ReadRequestAsync(stream).ConfigureAwait(false);
        int queryStart = target.IndexOf('?', StringComparison.Ordinal);
        string path = queryStart < 0 ? target : target[..queryStart];
        string query = queryStart < 0 ? string.Empty : target[(queryStart + 1)..];
        string route = RouteOf(path, query);

        TaskCompletionSource? gate;
        lock (_gate)
        {
            _calls.Add(route);
            gate = string.Equals(route, "token:refresh", StringComparison.Ordinal) ? _refreshGate : null;
        }

        if (gate is not null)
        {
            await gate.Task.WaitAsync(_stop.Token).ConfigureAwait(false);
        }

        Answer answer = Handle(method, route, query, body);
        if (string.Equals(route, "token:refresh", StringComparison.Ordinal) && TryTakeDrop())
        {
            return;
        }

        byte[] payload = Encoding.UTF8.GetBytes(answer.Body);
        string head = string.Create(
            CultureInfo.InvariantCulture,
            $"HTTP/1.1 {answer.Status} Fake\r\nContent-Type: application/json\r\nContent-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head), _stop.Token).ConfigureAwait(false);
        await stream.WriteAsync(payload, _stop.Token).ConfigureAwait(false);
        await stream.FlushAsync(_stop.Token).ConfigureAwait(false);
    }

    private bool TryTakeDrop()
    {
        while (true)
        {
            int pending = Volatile.Read(ref _dropRefreshResponses);
            if (pending <= 0)
            {
                return false;
            }

            if (Interlocked.CompareExchange(ref _dropRefreshResponses, pending - 1, pending) == pending)
            {
                return true;
            }
        }
    }

    private static string? HeaderValue(string head, string name)
    {
        foreach (string line in head.Split("\r\n").Skip(1))
        {
            int colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon > 0 && string.Equals(line[..colon], name, StringComparison.OrdinalIgnoreCase))
            {
                return line[(colon + 1)..].Trim();
            }
        }

        return null;
    }

    private static bool TryReadBody(string head, ReadOnlySpan<byte> received, out string body)
    {
        if (HeaderValue(head, "Transfer-Encoding") is { } encoding && encoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            return TryDecodeChunked(received, out body);
        }

        int expected = HeaderValue(head, "Content-Length") is { } text ? int.Parse(text, CultureInfo.InvariantCulture) : 0;
        if (received.Length < expected)
        {
            body = string.Empty;
            return false;
        }

        body = Encoding.UTF8.GetString(received[..expected]);
        return true;
    }

    private static bool TryDecodeChunked(ReadOnlySpan<byte> data, out string body)
    {
        using var decoded = new MemoryStream();
        int position = 0;
        while (true)
        {
            int lineEnd = data[position..].IndexOf("\r\n"u8);
            if (lineEnd < 0)
            {
                break;
            }

            string sizeText = Encoding.ASCII.GetString(data.Slice(position, lineEnd));
            int semicolon = sizeText.IndexOf(';', StringComparison.Ordinal);
            int size = int.Parse(semicolon < 0 ? sizeText : sizeText[..semicolon], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            position += lineEnd + 2;
            if (size == 0)
            {
                if (data.Length - position >= 2)
                {
                    body = Encoding.UTF8.GetString(decoded.ToArray());
                    return true;
                }

                break;
            }

            if (data.Length - position < size + 2)
            {
                break;
            }

            decoded.Write(data.Slice(position, size));
            position += size + 2;
        }

        body = string.Empty;
        return false;
    }

    private async Task<(string Method, string Target, string Body)> ReadRequestAsync(NetworkStream stream)
    {
        byte[] buffer = new byte[MaxRequestBytes];
        int length = 0;
        int headEnd = -1;
        while (length < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(length), _stop.Token).ConfigureAwait(false);
            if (read == 0)
            {
                throw new IOException("The client closed early.");
            }

            length += read;
            if (headEnd < 0)
            {
                headEnd = buffer.AsSpan(0, length).IndexOf("\r\n\r\n"u8);
            }

            if (headEnd < 0)
            {
                continue;
            }

            string head = Encoding.ASCII.GetString(buffer, 0, headEnd);
            int bodyStart = headEnd + 4;
            if (TryReadBody(head, buffer.AsSpan(bodyStart, length - bodyStart), out string body))
            {
                string[] requestLine = head.Split("\r\n")[0].Split(' ');
                return (requestLine[0], requestLine[1], body);
            }
        }

        throw new IOException("The request is too large.");
    }

    private Answer Handle(string method, string route, string query, string body)
    {
        lock (_gate)
        {
            if (_scriptedErrors.Count > 0 && string.Equals(_scriptedErrors.Peek().Route, route, StringComparison.Ordinal))
            {
                (_, int status, string code) = _scriptedErrors.Dequeue();
                return Error(status, code);
            }

            if (string.Equals(method, "GET", StringComparison.Ordinal) && string.Equals(route, "user", StringComparison.Ordinal))
            {
                return new Answer(200, "{\"id\":\"user\",\"email\":\"user@example.com\"}");
            }

            if (string.Equals(route, "logout", StringComparison.Ordinal))
            {
                return new Answer(204, string.Empty);
            }

            using JsonDocument document = JsonDocument.Parse(body);
            JsonElement root = document.RootElement;
            return route switch
            {
                "otp" => HandleOtp(root, query),
                "verify" => HandleVerify(root),
                "token:pkce" => HandlePkce(root),
                "token:refresh" => HandleRefresh(root),
                _ => Error(404, "not_found"),
            };
        }
    }

    private Answer HandleOtp(JsonElement root, string query)
    {
        string email = root.GetProperty("email").GetString()!;
        _codes[email] = (41230000 + Interlocked.Increment(ref _counter)).ToString(CultureInfo.InvariantCulture);
        if (root.TryGetProperty("code_challenge", out JsonElement challenge) && challenge.ValueKind == JsonValueKind.String)
        {
            var flow = new Flow(email, challenge.GetString()!, QueryValue(query, "redirect_to"), "auth-code-" + Interlocked.Increment(ref _counter).ToString(CultureInfo.InvariantCulture));
            _flows[flow.AuthCode] = flow;
            _lastFlowByEmail[email] = flow;
            _linkRequests.Add((flow.Challenge, flow.RedirectTo));
        }

        return new Answer(200, "{}");
    }

    private Answer HandleVerify(JsonElement root)
    {
        string email = root.GetProperty("email").GetString()!;
        string token = root.GetProperty("token").GetString()!;
        return _codes.TryGetValue(email, out string? code) && string.Equals(code, token, StringComparison.Ordinal)
            ? new Answer(200, SessionJson(NewSession(email), accessToken: null))
            : Error(403, "otp_expired");
    }

    private Answer HandlePkce(JsonElement root)
    {
        string authCode = root.GetProperty("auth_code").GetString()!;
        string verifier = root.GetProperty("code_verifier").GetString()!;
        if (!_flows.TryGetValue(authCode, out Flow? flow))
        {
            return Error(404, "flow_state_not_found");
        }

        if (!string.Equals(Base64UrlSha256(verifier), flow.Challenge, StringComparison.Ordinal))
        {
            return Error(400, "bad_code_verifier");
        }

        _ = _flows.Remove(authCode);
        return new Answer(200, SessionJson(NewSession(flow.Email), accessToken: null));
    }

    private Answer HandleRefresh(JsonElement root)
    {
        string token = root.GetProperty("refresh_token").GetString()!;
        _presentedRefreshTokens.Add(token);
        if (!_refreshTokens.TryGetValue(token, out RefreshRecord? record))
        {
            return Error(400, "refresh_token_not_found");
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (record.UsedAt is { } usedAt)
        {
            return now - usedAt > ReuseInterval
                ? Error(400, "refresh_token_already_used")
                : new Answer(200, SessionJson(record.Email, record.Successor!, accessToken: null));
        }

        record.UsedAt = now;
        record.Successor = NewRefreshToken(record.Email);
        return new Answer(200, SessionJson(record.Email, record.Successor, accessToken: null));
    }

    private string NewRefreshToken(string email)
    {
        string token = "rt-" + Interlocked.Increment(ref _counter).ToString(CultureInfo.InvariantCulture);
        _refreshTokens[token] = new RefreshRecord(email);
        return token;
    }

    private string NewSession(string email) => NewRefreshToken(email);

    private GoTrueSession ToSession(string refreshToken)
    {
        string email = _refreshTokens[refreshToken].Email;
        return new GoTrueSession(
            new SecretString("at-" + Interlocked.Increment(ref _counter).ToString(CultureInfo.InvariantCulture)),
            new SecretString(refreshToken),
            _time.GetUtcNow() + TimeSpan.FromHours(1),
            TimeSpan.FromHours(1),
            UserId(email),
            email);
    }

    private string SessionJson(string refreshToken, string? accessToken) => SessionJson(_refreshTokens[refreshToken].Email, refreshToken, accessToken);

    private string SessionJson(string email, string refreshToken, string? accessToken)
    {
        string access = accessToken ?? "at-" + Interlocked.Increment(ref _counter).ToString(CultureInfo.InvariantCulture);
        return "{\"access_token\":" + Quote(access) + ",\"token_type\":\"bearer\",\"expires_in\":3600,\"refresh_token\":" + Quote(refreshToken)
            + ",\"user\":{\"id\":" + Quote(UserId(email)) + ",\"email\":" + Quote(email) + "}}";
    }

    private static string UserId(string email) => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(email.ToUpperInvariant()))[..16]).ToString();

    private sealed record Answer(int Status, string Body);

    private sealed record Flow(string Email, string Challenge, string RedirectTo, string AuthCode);

    private sealed class RefreshRecord(string email)
    {
        public string Email { get; } = email;

        public DateTimeOffset? UsedAt { get; set; }

        public string? Successor { get; set; }
    }

    private sealed class RewriteHandler(int port) : HttpMessageHandler
    {
        private readonly SocketsHttpHandler _inner = new() { UseProxy = false };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.RequestUri = new UriBuilder(request.RequestUri!) { Scheme = Uri.UriSchemeHttp, Host = IPAddress.Loopback.ToString(), Port = port }.Uri;
            using var invoker = new HttpMessageInvoker(_inner, disposeHandler: false);
            return await invoker.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
