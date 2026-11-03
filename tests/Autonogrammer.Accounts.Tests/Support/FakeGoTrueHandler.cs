using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Autonogrammer.Harness.Secrets;

namespace Autonogrammer.Accounts.Tests.Support;

/// <summary>
/// An in-process fake of the six auth service routes: OTP issue and expiry on a <see cref="ManualTimeProvider"/>, S256 verifier checking, single-use
/// refresh tokens with a 10 second reuse interval, scope-aware logout, scripted errors and transport failures, and 429 with and without
/// <c>Retry-After</c>. It never touches the network.
/// </summary>
internal sealed class FakeGoTrueHandler : HttpMessageHandler
{
    /// <summary>The project URL the fake answers for.</summary>
    public const string ProjectUrl = "https://fake-project.supabase.co";

    /// <summary>The publishable key the fake requires.</summary>
    public const string PublishableKey = "sb_publishable_fake_key_for_tests";

    /// <summary>The marker text of every error message the fake sends; no test may ever see it again in a failure or a log.</summary>
    public const string ServerMessageText = "SERVER-MESSAGE-TEXT-MUST-NEVER-LEAK";

    private static readonly TimeSpan ReuseInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(5);

    private readonly ManualTimeProvider _time;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FakeUser> _users = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, OtpRecord> _codes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FlowRecord> _flows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, AccessRecord> _accessTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, RefreshRecord> _refreshTokens = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Queue<Scripted>> _scripts = new(StringComparer.Ordinal);
    private readonly List<RecordedRequest> _requests = [];
    private readonly List<SentEmail> _outbox = [];
    private int _counter;
    private int _codeCounter = 41230000;
    private TaskCompletionSource? _refreshGate;

    /// <summary>Creates a fake on a manual clock.</summary>
    /// <param name="time">The clock that decides code, link, token and reuse expiry.</param>
    public FakeGoTrueHandler(ManualTimeProvider time)
    {
        _time = time;
    }

    /// <summary>Gets or sets how long an access token lives.</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets how long an email code lives.</summary>
    public TimeSpan CodeLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Gets or sets a value indicating whether the first-time signup email shows a code (owner action O4).</summary>
    public bool SignupEmailHasCode { get; set; }

    /// <summary>Gets or sets a callback run when a logout request arrives, before it is answered. Tests read state in it.</summary>
    public Action? OnLogout { get; set; }

    /// <summary>Gets a snapshot of the requests seen so far.</summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Gets a snapshot of the emails sent so far.</summary>
    public IReadOnlyList<SentEmail> Outbox
    {
        get
        {
            lock (_gate)
            {
                return [.. _outbox];
            }
        }
    }

    /// <summary>Counts the requests to one route.</summary>
    /// <param name="route">One of <c>otp</c>, <c>verify</c>, <c>token:pkce</c>, <c>token:refresh</c>, <c>logout</c>, <c>user</c>.</param>
    /// <returns>How many requests reached it.</returns>
    public int Count(string route)
    {
        lock (_gate)
        {
            return _requests.Count(request => string.Equals(RouteOf(request.Path, request.Query), route, StringComparison.Ordinal));
        }
    }

    /// <summary>Makes the next requests to a route fail with an error body.</summary>
    /// <param name="route">The route name.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="errorCode">The <c>error_code</c> member, or null for none.</param>
    /// <param name="retryAfterSeconds">The <c>Retry-After</c> header in seconds, or null for none.</param>
    /// <param name="times">How many requests fail.</param>
    public void ScriptError(string route, int status, string? errorCode, int? retryAfterSeconds = null, int times = 1)
    {
        Enqueue(route, new Scripted(status, errorCode, retryAfterSeconds, Transport: false), times);
    }

    /// <summary>Makes the next requests to a route fail like a dropped connection.</summary>
    /// <param name="route">The route name.</param>
    /// <param name="times">How many requests fail.</param>
    public void ScriptTransportFailure(string route, int times = 1)
    {
        Enqueue(route, new Scripted(0, null, null, Transport: true), times);
    }

    /// <summary>Makes the next requests to a route reach the service and be handled, but loses the answer like a dropped connection would.</summary>
    /// <param name="route">The route name.</param>
    /// <param name="times">How many answers are lost.</param>
    public void ScriptLostResponse(string route, int times = 1)
    {
        Enqueue(route, new Scripted(0, null, null, Transport: true, LostResponse: true), times);
    }

    /// <summary>Holds every refresh request until <see cref="ReleaseRefresh"/>.</summary>
    public void HoldRefresh()
    {
        lock (_gate)
        {
            _refreshGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    /// <summary>Lets held refresh requests through.</summary>
    public void ReleaseRefresh()
    {
        TaskCompletionSource? gate;
        lock (_gate)
        {
            gate = _refreshGate;
            _refreshGate = null;
        }

        gate?.TrySetResult();
    }

    /// <summary>Marks a user as confirmed, as if the person opened the signup link. The user is created when unknown.</summary>
    /// <param name="email">The address.</param>
    public void ConfirmSignup(string email)
    {
        lock (_gate)
        {
            GetOrCreateUser(email).Confirmed = true;
        }
    }

    /// <summary>Follows the magic link of the last email to an address, as the person's mail client would: a signup link confirms the user.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The code the redirect carries, to hand to <see cref="SignInCoordinator.CompleteLinkAsync"/>.</returns>
    public string FollowLink(string email)
    {
        lock (_gate)
        {
            SentEmail last = _outbox.Last(mail => string.Equals(mail.Email, email, StringComparison.OrdinalIgnoreCase));
            if (last.LinkAuthCode is null)
            {
                throw new InvalidOperationException("The last email carries no link.");
            }

            if (string.Equals(last.Kind, "signup", StringComparison.Ordinal))
            {
                GetOrCreateUser(email).Confirmed = true;
            }

            return last.LinkAuthCode;
        }
    }

    /// <summary>Creates a confirmed user with a live session, as a sign-in on another device would.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The session as the service would issue it.</returns>
    public GoTrueSession IssueSession(string email)
    {
        lock (_gate)
        {
            FakeUser user = GetOrCreateUser(email);
            user.Confirmed = true;
            return ToSession(NewSession(user));
        }
    }

    /// <summary>Reads the code of the last email to an address.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The code.</returns>
    public string LastCode(string email)
    {
        lock (_gate)
        {
            return _outbox.Last(mail => string.Equals(mail.Email, email, StringComparison.OrdinalIgnoreCase)).Code
                ?? throw new InvalidOperationException("The last email carries no code.");
        }
    }

    /// <summary>Tells whether a session of the user is still alive on the service.</summary>
    /// <param name="email">The address.</param>
    /// <returns>The number of live sessions.</returns>
    public int LiveSessions(string email)
    {
        lock (_gate)
        {
            string id = _users[email].Id;
            return _sessions.Values.Count(session => !session.Revoked && string.Equals(session.UserId, id, StringComparison.Ordinal));
        }
    }

    /// <summary>Tells whether an access token still names a live session.</summary>
    /// <param name="accessToken">The token text.</param>
    /// <returns>True when the session is live.</returns>
    public bool IsAccessTokenLive(string accessToken)
    {
        lock (_gate)
        {
            return _accessTokens.TryGetValue(accessToken, out AccessRecord? record) && !_sessions[record.SessionId].Revoked;
        }
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Uri uri = request.RequestUri ?? throw new InvalidOperationException("The request has no address.");
        const string Prefix = "/auth/v1/";
        string path = uri.AbsolutePath.StartsWith(Prefix, StringComparison.Ordinal) ? uri.AbsolutePath[Prefix.Length..] : uri.AbsolutePath;
        string query = uri.Query.TrimStart('?');
        string? apiKey = request.Headers.TryGetValues("apikey", out IEnumerable<string>? keys) ? keys.FirstOrDefault() : null;
        string? bearer = string.Equals(request.Headers.Authorization?.Scheme, "Bearer", StringComparison.Ordinal) ? request.Headers.Authorization!.Parameter : null;

        string route = RouteOf(path, query);
        (Scripted? scripted, TaskCompletionSource? gate) = Record(new RecordedRequest(request.Method.Method, path, query, body, apiKey, bearer), route);
        if (gate is not null)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (scripted is { Transport: true, LostResponse: false })
        {
            throw new HttpRequestException("The fake connection was dropped.");
        }

        bool lostResponse = scripted is { LostResponse: true };
        if (scripted is not null && !lostResponse)
        {
            return Render(Error(scripted.Status, scripted.ErrorCode, scripted.RetryAfterSeconds));
        }

        if (!string.Equals(apiKey, PublishableKey, StringComparison.Ordinal))
        {
            return Render(Error(401, "no_authorization", null));
        }

        Answer answer = Dispatch(route, query, body, bearer);
        if (lostResponse)
        {
            throw new HttpRequestException("The fake connection was dropped after the request was handled.");
        }

        return Render(answer);
    }

    private (Scripted? Scripted, TaskCompletionSource? Gate) Record(RecordedRequest seen, string route)
    {
        lock (_gate)
        {
            _requests.Add(seen);
            Scripted? scripted = _scripts.TryGetValue(route, out Queue<Scripted>? queue) && queue.Count > 0 ? queue.Dequeue() : null;
            TaskCompletionSource? gate = string.Equals(route, "token:refresh", StringComparison.Ordinal) ? _refreshGate : null;
            return (scripted, gate);
        }
    }

    private Answer Dispatch(string route, string query, string body, string? bearer)
    {
        lock (_gate)
        {
            return route switch
            {
                "otp" => HandleOtp(query, body),
                "verify" => HandleVerify(body),
                "token:pkce" => HandlePkce(body),
                "token:refresh" => HandleRefresh(body),
                "logout" => HandleLogout(query, bearer),
                "user" => HandleUser(bearer),
                _ => Error(404, "no_route", null),
            };
        }
    }

    private static string RouteOf(string path, string query) => path switch
    {
        "otp" => "otp",
        "verify" => "verify",
        "logout" => "logout",
        "user" => "user",
        "token" when query.Contains("grant_type=pkce", StringComparison.Ordinal) => "token:pkce",
        "token" when query.Contains("grant_type=refresh_token", StringComparison.Ordinal) => "token:refresh",
        "token" when query.Contains("grant_type=web3", StringComparison.Ordinal) => "token:web3",
        _ => path,
    };

    private static string Base64UrlSha256(string text) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(text))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static Answer Error(int status, string? errorCode, int? retryAfterSeconds)
    {
        string json = errorCode is null
            ? $$"""{"code":{{status}},"msg":"{{ServerMessageText}}"}"""
            : $$"""{"code":{{status}},"error_code":"{{errorCode}}","msg":"{{ServerMessageText}}"}""";
        return new Answer(status, json, retryAfterSeconds);
    }

    private static Answer Json(HttpStatusCode status, string json) => new((int)status, json, null);

    private static HttpResponseMessage Render(Answer answer)
    {
        var response = new HttpResponseMessage((HttpStatusCode)answer.Status);
        if (answer.Json is not null)
        {
            response.Content = new StringContent(answer.Json, Encoding.UTF8, "application/json");
        }

        if (answer.RetryAfterSeconds is { } seconds)
        {
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(seconds));
        }

        return response;
    }

    private static string Escape(string value) => JsonEncodedText.Encode(value).ToString();

    private void Enqueue(string route, Scripted scripted, int times)
    {
        lock (_gate)
        {
            if (!_scripts.TryGetValue(route, out Queue<Scripted>? queue))
            {
                queue = new Queue<Scripted>();
                _scripts[route] = queue;
            }

            for (int i = 0; i < times; i++)
            {
                queue.Enqueue(scripted);
            }
        }
    }

    private FakeUser GetOrCreateUser(string email)
    {
        if (!_users.TryGetValue(email, out FakeUser? user))
        {
            user = new FakeUser($"user-{++_counter}", email);
            _users[email] = user;
        }

        return user;
    }

    private string NextCode() => (++_codeCounter).ToString(CultureInfo.InvariantCulture);

    private Answer HandleOtp(string query, string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        string email = document.RootElement.GetProperty("email").GetString()!;
        string? challenge = document.RootElement.TryGetProperty("code_challenge", out JsonElement c) ? c.GetString() : null;
        string? redirect = null;
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            if (pair.StartsWith("redirect_to=", StringComparison.Ordinal))
            {
                redirect = Uri.UnescapeDataString(pair["redirect_to=".Length..]);
            }
        }

        bool known = _users.TryGetValue(email, out FakeUser? user);
        user ??= GetOrCreateUser(email);
        string kind = known && user.Confirmed ? "magiclink" : "signup";
        bool hasCode = string.Equals(kind, "magiclink", StringComparison.Ordinal) || SignupEmailHasCode;
        string? code = hasCode ? NextCode() : null;
        string? linkAuthCode = null;
        if (challenge is not null)
        {
            linkAuthCode = "link-" + (++_counter).ToString(CultureInfo.InvariantCulture);
            _flows[linkAuthCode] = new FlowRecord(email, challenge, _time.GetUtcNow() + LinkLifetime);
        }

        // A newer email invalidates the older code of the same address.
        _ = _codes.Remove(email);
        if (code is not null)
        {
            _codes[email] = new OtpRecord(code, _time.GetUtcNow() + CodeLifetime);
        }

        _outbox.Add(new SentEmail(email, kind, code, linkAuthCode, redirect));
        return Json(HttpStatusCode.OK, "{}");
    }

    private Answer HandleVerify(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        string email = document.RootElement.GetProperty("email").GetString()!;
        string token = document.RootElement.GetProperty("token").GetString()!;
        if (!_codes.TryGetValue(email, out OtpRecord? record)
            || _time.GetUtcNow() >= record.ExpiresAt
            || !string.Equals(record.Code, token, StringComparison.Ordinal))
        {
            return Error(403, "otp_expired", null);
        }

        _ = _codes.Remove(email);
        FakeUser user = _users[email];
        user.Confirmed = true;
        return Json(HttpStatusCode.OK, SessionJson(NewSession(user)));
    }

    private Answer HandlePkce(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        string authCode = document.RootElement.GetProperty("auth_code").GetString()!;
        string verifier = document.RootElement.GetProperty("code_verifier").GetString()!;
        if (!_flows.TryGetValue(authCode, out FlowRecord? flow))
        {
            return Error(404, "flow_state_not_found", null);
        }

        if (_time.GetUtcNow() >= flow.ExpiresAt)
        {
            _ = _flows.Remove(authCode);
            return Error(403, "flow_state_expired", null);
        }

        if (!string.Equals(Base64UrlSha256(verifier), flow.Challenge, StringComparison.Ordinal))
        {
            return Error(400, "bad_code_verifier", null);
        }

        _ = _flows.Remove(authCode);
        FakeUser user = GetOrCreateUser(flow.Email);
        user.Confirmed = true;
        return Json(HttpStatusCode.OK, SessionJson(NewSession(user)));
    }

    private Answer HandleRefresh(string body)
    {
        using JsonDocument document = JsonDocument.Parse(body);
        string token = document.RootElement.GetProperty("refresh_token").GetString()!;
        if (!_refreshTokens.TryGetValue(token, out RefreshRecord? record))
        {
            return Error(400, "refresh_token_not_found", null);
        }

        SessionRecord session = _sessions[record.SessionId];
        if (session.Revoked)
        {
            return Error(400, "session_not_found", null);
        }

        DateTimeOffset now = _time.GetUtcNow();
        if (record.UsedAt is { } usedAt)
        {
            if (now - usedAt > ReuseInterval)
            {
                session.Revoked = true;
                return Error(400, "refresh_token_already_used", null);
            }

            // Inside the reuse interval the service answers again with the refresh token it already issued.
            return Json(HttpStatusCode.OK, SessionJson(IssueAccess(session, record.Successor!)));
        }

        record.UsedAt = now;
        string successor = NewRefresh(session);
        record.Successor = successor;
        return Json(HttpStatusCode.OK, SessionJson(IssueAccess(session, successor)));
    }

    private Answer HandleLogout(string query, string? bearer)
    {
        OnLogout?.Invoke();
        Answer? refusal = Authorize(bearer, out AccessRecord? access);
        if (refusal is not null)
        {
            return refusal;
        }

        string scope = query.Split('&').FirstOrDefault(part => part.StartsWith("scope=", StringComparison.Ordinal))?["scope=".Length..] ?? "global";
        SessionRecord current = _sessions[access!.SessionId];
        foreach (SessionRecord session in _sessions.Values.Where(s => string.Equals(s.UserId, current.UserId, StringComparison.Ordinal)))
        {
            bool isCurrent = ReferenceEquals(session, current);
            if (string.Equals(scope, "global", StringComparison.Ordinal)
                || (string.Equals(scope, "local", StringComparison.Ordinal) && isCurrent)
                || (string.Equals(scope, "others", StringComparison.Ordinal) && !isCurrent))
            {
                session.Revoked = true;
            }
        }

        return new Answer((int)HttpStatusCode.NoContent, null, null);
    }

    private Answer HandleUser(string? bearer)
    {
        Answer? refusal = Authorize(bearer, out AccessRecord? access);
        if (refusal is not null)
        {
            return refusal;
        }

        FakeUser user = _users.Values.First(u => string.Equals(u.Id, _sessions[access!.SessionId].UserId, StringComparison.Ordinal));
        return Json(HttpStatusCode.OK, $$"""{"id":"{{user.Id}}","email":"{{Escape(user.Email)}}","aud":"authenticated"}""");
    }

    private Answer? Authorize(string? bearer, out AccessRecord? access)
    {
        access = null;
        if (bearer is null || string.Equals(bearer, PublishableKey, StringComparison.Ordinal) || !_accessTokens.TryGetValue(bearer, out access))
        {
            return Error(401, "bad_jwt", null);
        }

        if (_time.GetUtcNow() >= access.ExpiresAt)
        {
            return Error(401, "bad_jwt", null);
        }

        return _sessions[access.SessionId].Revoked ? Error(403, "session_not_found", null) : null;
    }

    private SessionRecord NewSession(FakeUser user)
    {
        var session = new SessionRecord($"session-{++_counter}", user.Id, user.Email);
        _sessions[session.Id] = session;
        session.FirstRefresh = NewRefresh(session);
        return session;
    }

    private string NewRefresh(SessionRecord session)
    {
        string token = $"refresh-{++_counter}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";
        _refreshTokens[token] = new RefreshRecord(session.Id);
        session.LatestRefresh = token;
        return token;
    }

    private IssuedTokens IssueAccess(SessionRecord session, string refresh)
    {
        string access = $"access-{++_counter}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(6))}";
        DateTimeOffset expiresAt = _time.GetUtcNow() + AccessTokenLifetime;
        _accessTokens[access] = new AccessRecord(session.Id, expiresAt);
        return new IssuedTokens(session, access, refresh, (int)AccessTokenLifetime.TotalSeconds, expiresAt);
    }

    private IssuedTokens NewSession(SessionRecord session) => IssueAccess(session, session.FirstRefresh!);

    private string SessionJson(SessionRecord session) => SessionJson(NewSession(session));

    private static string SessionJson(IssuedTokens tokens) =>
        $$$"""
        {"access_token":"{{{tokens.Access}}}","token_type":"bearer","expires_in":{{{tokens.ExpiresIn}}},"expires_at":{{{tokens.ExpiresAt.ToUnixTimeSeconds()}}},"refresh_token":"{{{tokens.Refresh}}}","user":{"id":"{{{tokens.Session.UserId}}}","email":"{{{Escape(tokens.Session.Email)}}}","aud":"authenticated","role":"authenticated","extra_member":{"ignored":true}},"extra_top_level":1}
        """;

    private GoTrueSession ToSession(SessionRecord session)
    {
        IssuedTokens tokens = NewSession(session);
        return new GoTrueSession(
            new SecretString(tokens.Access),
            new SecretString(tokens.Refresh),
            tokens.ExpiresAt,
            AccessTokenLifetime,
            session.UserId,
            session.Email);
    }

    private sealed class FakeUser(string id, string email)
    {
        public string Id { get; } = id;

        public string Email { get; } = email;

        public bool Confirmed { get; set; }
    }

    private sealed class SessionRecord(string id, string userId, string email)
    {
        public string Id { get; } = id;

        public string UserId { get; } = userId;

        public string Email { get; } = email;

        public bool Revoked { get; set; }

        public string? FirstRefresh { get; set; }

        public string? LatestRefresh { get; set; }
    }

    private sealed class RefreshRecord(string sessionId)
    {
        public string SessionId { get; } = sessionId;

        public DateTimeOffset? UsedAt { get; set; }

        public string? Successor { get; set; }
    }

    private sealed record AccessRecord(string SessionId, DateTimeOffset ExpiresAt);

    private sealed record OtpRecord(string Code, DateTimeOffset ExpiresAt);

    private sealed record FlowRecord(string Email, string Challenge, DateTimeOffset ExpiresAt);

    private sealed record Scripted(int Status, string? ErrorCode, int? RetryAfterSeconds, bool Transport, bool LostResponse = false);

    private sealed record Answer(int Status, string? Json, int? RetryAfterSeconds);

    private sealed record IssuedTokens(SessionRecord Session, string Access, string Refresh, int ExpiresIn, DateTimeOffset ExpiresAt);
}
