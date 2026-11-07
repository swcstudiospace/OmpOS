using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Autonogrammer.Contracts.Accounts;
using Autonogrammer.Harness.Secrets;
using Microsoft.Extensions.Logging;

namespace Autonogrammer.Accounts;

/// <summary>
/// A plain <see cref="HttpClient"/> client of the six auth service calls the product needs. Every call sends the publishable key as <c>apikey</c>;
/// only user calls add the access token as bearer, and the publishable key is never a bearer. Expected failures come back as an
/// <see cref="AuthFailure"/> and never throw; programming errors throw. Nothing here retries: <c>otp</c>, <c>verify</c> and <c>pkce</c> are not idempotent,
/// and the one retry of a lost refresh belongs to <see cref="AccountSessionManager"/>.
/// </summary>
public sealed class GoTrueClient
{
    /// <summary>The time one request may take before it counts as a network failure.</summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    /// <summary>The OAuth providers this client starts, by the names the service registers: <c>google</c>, and <c>x</c> for X API v2 (not the legacy <c>twitter</c> provider).</summary>
    public static readonly IReadOnlySet<string> OAuthProviders = new HashSet<string>(StringComparer.Ordinal) { "google", "x" };

    private const string EthereumChain = "ethereum";

    private const int MinCodeLength = 6;
    private const int MaxCodeLength = 10;

    private static readonly Action<ILogger, string, int, Exception?> RequestAnswered =
        LoggerMessage.Define<string, int>(
            LogLevel.Debug,
            new EventId(3000, "GoTrueRequestAnswered"),
            "The auth service answered {Endpoint} with status {Status}.");

    private static readonly Action<ILogger, string, int, AuthErrorKind, string, Exception?> RequestRefused =
        LoggerMessage.Define<string, int, AuthErrorKind, string>(
            LogLevel.Information,
            new EventId(3001, "GoTrueRequestRefused"),
            "The auth service refused {Endpoint} with status {Status}, kind {Kind}, code {ServerCode}.");

    private static readonly Action<ILogger, string, AuthErrorKind, Exception?> RequestUnreachable =
        LoggerMessage.Define<string, AuthErrorKind>(
            LogLevel.Information,
            new EventId(3002, "GoTrueRequestUnreachable"),
            "The auth service could not be reached for {Endpoint}: {Kind}.");

    private static readonly Action<ILogger, string, Exception?> ResponseUnreadable =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(3003, "GoTrueResponseUnreadable"),
            "The auth service answer to {Endpoint} could not be read.");

    private readonly HttpClient _http;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<GoTrueClient>? _logger;

    /// <summary>Creates a client.</summary>
    /// <param name="http">The HTTP client. It needs no base address and no default headers; the caller owns it.</param>
    /// <param name="options">The validated connection settings.</param>
    /// <param name="timeProvider">The clock that bounds each request to <see cref="RequestTimeout"/>.</param>
    /// <param name="logger">An optional logger. It never receives an email address, code, token, verifier or query.</param>
    public GoTrueClient(HttpClient http, GoTrueOptions options, TimeProvider timeProvider, ILogger<GoTrueClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _http = http;
        Options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    private enum Endpoint
    {
        Otp,
        Verify,
        Pkce,
        Refresh,
        Logout,
        User,
        Web3,
    }

    /// <summary>Gets the connection settings this client uses.</summary>
    public GoTrueOptions Options { get; }

    /// <summary>
    /// Normalises a code a person typed: spaces and hyphens are removed, and the rest must be 6 to 10 ASCII digits. No length is assumed, because the live
    /// code has 8 digits although the documentation says 6.
    /// </summary>
    /// <param name="code">The text the person typed.</param>
    /// <returns>The digits, or null when the text is not a plausible code.</returns>
    public static string? NormalizeCode(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        Span<char> buffer = stackalloc char[code.Length];
        int length = 0;
        foreach (char c in code)
        {
            if (c == '-' || char.IsWhiteSpace(c))
            {
                continue;
            }

            if (c is < '0' or > '9')
            {
                return null;
            }

            buffer[length++] = c;
        }

        return length is >= MinCodeLength and <= MaxCodeLength ? new string(buffer[..length]) : null;
    }

    /// <summary>Asks the service to email a one-time code, and a magic link when a challenge is supplied.</summary>
    /// <param name="email">The address. An unknown address becomes a user.</param>
    /// <param name="codeChallenge">The S256 challenge of a verifier for the link flow, or null for the code only.</param>
    /// <param name="redirectTo">Where the link returns to, or null for the code only. It is given exactly when a challenge is.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, or why the email was not sent.</returns>
    public async Task<AuthResult<AuthUnit>> SendEmailOtpAsync(string email, string? codeChallenge, Uri? redirectTo, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if ((codeChallenge is null) != (redirectTo is null))
        {
            throw new ArgumentException("A code challenge and a redirect address are given together or not at all.", nameof(codeChallenge));
        }

        var body = new OtpRequest(email, CreateUser: true, codeChallenge, codeChallenge is null ? null : Pkce.ChallengeMethod);
        string path = redirectTo is null ? "otp" : "otp?redirect_to=" + Uri.EscapeDataString(redirectTo.AbsoluteUri);
        return await SendAsync(
            Endpoint.Otp,
            HttpMethod.Post,
            path,
            () => JsonContent.Create(body, GoTrueJsonContext.Default.OtpRequest),
            bearer: null,
            static (_, _) => Task.FromResult(AuthResult.Success(AuthUnit.Value)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Checks the code a person typed and returns the session it opens.</summary>
    /// <param name="email">The address the code was sent to.</param>
    /// <param name="code">The code as typed; spaces and hyphens are removed first.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session, or why the code was not accepted. A malformed code is <see cref="AuthErrorKind.CodeInvalidOrExpired"/> without a request.</returns>
    public async Task<AuthResult<GoTrueSession>> VerifyEmailOtpAsync(string email, string code, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentNullException.ThrowIfNull(code);
        string? digits = NormalizeCode(code);
        if (digits is null)
        {
            return AuthResult.Fail<GoTrueSession>(AuthErrorKind.CodeInvalidOrExpired);
        }

        var body = new VerifyRequest("email", email, digits);
        return await SendAsync(
            Endpoint.Verify,
            HttpMethod.Post,
            "verify",
            () => JsonContent.Create(body, GoTrueJsonContext.Default.VerifyRequest),
            bearer: null,
            ReadSessionAsync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Exchanges the code a magic link callback carried for a session.</summary>
    /// <param name="authCode">The code from the callback. It lives about five minutes and works once.</param>
    /// <param name="codeVerifier">The verifier whose challenge was sent with the request for the link.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session, or why the exchange failed.</returns>
    public async Task<AuthResult<GoTrueSession>> ExchangeCodeAsync(SecretString authCode, SecretString codeVerifier, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(authCode);
        ArgumentNullException.ThrowIfNull(codeVerifier);
        var body = new PkceTokenRequest(authCode.Reveal(), codeVerifier.Reveal());
        return await SendAsync(
            Endpoint.Pkce,
            HttpMethod.Post,
            "token?grant_type=pkce",
            () => JsonContent.Create(body, GoTrueJsonContext.Default.PkceTokenRequest),
            bearer: null,
            ReadSessionAsync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Spends a refresh token for the next session.</summary>
    /// <param name="refreshToken">The refresh token. It works once, with a short reuse interval on the service.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new session, or why the refresh failed. A revoked, used or unknown token is <see cref="AuthErrorKind.SessionEnded"/>.</returns>
    public async Task<AuthResult<GoTrueSession>> RefreshAsync(SecretString refreshToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        var body = new RefreshTokenRequest(refreshToken.Reveal());
        return await SendAsync(
            Endpoint.Refresh,
            HttpMethod.Post,
            "token?grant_type=refresh_token",
            () => JsonContent.Create(body, GoTrueJsonContext.Default.RefreshTokenRequest),
            bearer: null,
            ReadSessionAsync,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Ends sessions on the service.</summary>
    /// <param name="accessToken">The access token of the session to end.</param>
    /// <param name="scope">How far the sign-out reaches. It is required; the service default would also sign the website out.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Success, or why the service did not confirm the sign-out.</returns>
    public async Task<AuthResult<AuthUnit>> SignOutAsync(SecretString accessToken, SignOutScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        string name = scope switch
        {
            SignOutScope.Local => "local",
            SignOutScope.Global => "global",
            SignOutScope.Others => "others",
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Unknown sign-out scope."),
        };

        return await SendAsync(
            Endpoint.Logout,
            HttpMethod.Post,
            "logout?scope=" + name,
            contentFactory: null,
            accessToken,
            static (_, _) => Task.FromResult(AuthResult.Success(AuthUnit.Value)),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads the identity behind an access token. Only tests and the live check use it.</summary>
    /// <param name="accessToken">The access token.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The identity, or why it could not be read.</returns>
    public async Task<AuthResult<GoTrueUser>> GetUserAsync(SecretString accessToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(accessToken);
        return await SendAsync(
            Endpoint.User,
            HttpMethod.Get,
            "user",
            contentFactory: null,
            accessToken,
            async (response, ct) =>
            {
                UserResponse? user = await response.Content.ReadFromJsonAsync(GoTrueJsonContext.Default.UserResponse, ct).ConfigureAwait(false);
                return string.IsNullOrEmpty(user?.Id)
                    ? AuthResult.Fail<GoTrueUser>(new AuthFailure(AuthErrorKind.Unexpected, HttpStatus: (int)response.StatusCode))
                    : AuthResult.Success(new GoTrueUser(user.Id, user.Email));
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the URL that starts a PKCE sign-in with a third-party provider. The browser is sent there, and the service returns the person to <paramref name="redirectTo"/> with a code.</summary>
    /// <param name="provider">The provider name the service registers: <c>google</c> or <c>x</c>.</param>
    /// <param name="redirectTo">Where the service returns the person. It must be https, or http to a loopback address.</param>
    /// <param name="codeChallenge">The S256 challenge of the verifier that the exchange reveals. The verifier never leaves this client.</param>
    /// <returns>The authorize URL. It carries no secret.</returns>
    public Uri BuildProviderAuthorizeUri(string provider, Uri redirectTo, string codeChallenge)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentNullException.ThrowIfNull(redirectTo);
        ArgumentException.ThrowIfNullOrWhiteSpace(codeChallenge);
        if (!OAuthProviders.Contains(provider))
        {
            throw new ArgumentException("The provider is not one this client starts.", nameof(provider));
        }

        if (!IsAllowedRedirect(redirectTo))
        {
            throw new ArgumentException("The redirect must be https, or http to a loopback address.", nameof(redirectTo));
        }

        string query = "provider=" + Uri.EscapeDataString(provider)
            + "&redirect_to=" + Uri.EscapeDataString(redirectTo.AbsoluteUri)
            + "&code_challenge=" + Uri.EscapeDataString(codeChallenge)
            + "&code_challenge_method=" + Pkce.ChallengeMethod;
        return new Uri(Options.AuthBaseUri, "authorize?" + query);
    }

    /// <summary>Signs in with a wallet. The service verifies the signed Sign-In-With-Ethereum message and returns a session for its address.</summary>
    /// <param name="message">The EIP-4361 message exactly as the wallet signed it (see <see cref="SiweMessage"/>).</param>
    /// <param name="signature">The hexadecimal EIP-191 signature of the message.</param>
    /// <param name="captchaToken">The CAPTCHA token the person solved, when the project requires one; otherwise null.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The session, or why the wallet was not accepted.</returns>
    public async Task<AuthResult<GoTrueSession>> SignInWithWeb3Async(string message, string signature, string? captchaToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(signature);
        GoTrueSecurity? security = string.IsNullOrWhiteSpace(captchaToken) ? null : new GoTrueSecurity(captchaToken);
        var body = new Web3TokenRequest(message, signature, EthereumChain, security);
        return await SendAsync(
            Endpoint.Web3,
            HttpMethod.Post,
            "token?grant_type=web3",
            () => JsonContent.Create(body, GoTrueJsonContext.Default.Web3TokenRequest),
            bearer: null,
            ReadSessionAsync,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsAllowedRedirect(Uri redirectTo) =>
        redirectTo.IsAbsoluteUri
        && (string.Equals(redirectTo.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal) || (string.Equals(redirectTo.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) && redirectTo.IsLoopback));

    private static string Name(Endpoint endpoint) => endpoint switch
    {
        Endpoint.Otp => "otp",
        Endpoint.Verify => "verify",
        Endpoint.Pkce => "token/pkce",
        Endpoint.Refresh => "token/refresh",
        Endpoint.Logout => "logout",
        Endpoint.Web3 => "token/web3",
        _ => "user",
    };

    private static AuthErrorKind Classify(Endpoint endpoint, HttpStatusCode status, string? code)
    {
        AuthErrorKind? byCode = code switch
        {
            "over_email_send_rate_limit" => AuthErrorKind.EmailRateLimited,
            "over_request_rate_limit" => AuthErrorKind.TooManyRequests,
            "otp_expired" => AuthErrorKind.CodeInvalidOrExpired,
            "bad_code_verifier" or "flow_state_not_found" or "flow_state_expired" => AuthErrorKind.LinkInvalidOrExpired,
            "email_address_invalid" => AuthErrorKind.EmailInvalid,
            "validation_failed" => endpoint == Endpoint.Pkce ? AuthErrorKind.LinkInvalidOrExpired : AuthErrorKind.EmailInvalid,
            "signup_disabled" => AuthErrorKind.SignUpsClosed,
            "captcha_failed" => AuthErrorKind.CaptchaRequired,
            "user_banned" => AuthErrorKind.AccountBlocked,
            "otp_disabled" or "email_provider_disabled" or "feature_disabled" => AuthErrorKind.MethodUnavailable,
            "refresh_token_not_found" or "refresh_token_already_used" or "session_not_found" or "session_expired" => AuthErrorKind.SessionEnded,
            "hook_timeout" => AuthErrorKind.ServiceUnavailable,
            _ => null,
        };
        if (byCode is { } kind)
        {
            return kind;
        }

        if (status == HttpStatusCode.TooManyRequests)
        {
            return AuthErrorKind.TooManyRequests;
        }

        if ((int)status >= 500)
        {
            return AuthErrorKind.ServiceUnavailable;
        }

        return endpoint is Endpoint.Logout or Endpoint.User && status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? AuthErrorKind.SessionEnded
            : AuthErrorKind.Unexpected;
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response, TimeProvider time)
    {
        RetryConditionHeaderValue? header = response.Headers.RetryAfter;
        if (header?.Delta is { } delta)
        {
            return delta < TimeSpan.Zero ? TimeSpan.Zero : delta;
        }

        if (header?.Date is { } date)
        {
            TimeSpan wait = date - time.GetUtcNow();
            return wait < TimeSpan.Zero ? TimeSpan.Zero : wait;
        }

        return null;
    }

    private async Task<AuthResult<GoTrueSession>> ReadSessionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        TokenResponse? token = await response.Content.ReadFromJsonAsync(GoTrueJsonContext.Default.TokenResponse, cancellationToken).ConfigureAwait(false);
        if (token is null
            || string.IsNullOrEmpty(token.AccessToken)
            || string.IsNullOrEmpty(token.RefreshToken)
            || string.IsNullOrEmpty(token.User?.Id)
            || token.ExpiresIn <= 0)
        {
            return AuthResult.Fail<GoTrueSession>(new AuthFailure(AuthErrorKind.Unexpected, HttpStatus: (int)response.StatusCode));
        }

        var lifetime = TimeSpan.FromSeconds(token.ExpiresIn);
        return AuthResult.Success(new GoTrueSession(
            new SecretString(token.AccessToken),
            new SecretString(token.RefreshToken),
            _timeProvider.GetUtcNow() + lifetime,
            lifetime,
            token.User.Id,
            token.User.Email));
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            ErrorResponse? error = await response.Content.ReadFromJsonAsync(GoTrueJsonContext.Default.ErrorResponse, cancellationToken).ConfigureAwait(false);
            return error?.ErrorCode;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private async Task<AuthResult<T>> SendAsync<T>(
        Endpoint endpoint,
        HttpMethod method,
        string path,
        Func<HttpContent?>? contentFactory,
        SecretString? bearer,
        Func<HttpResponseMessage, CancellationToken, Task<AuthResult<T>>> onSuccess,
        CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(RequestTimeout, _timeProvider);
        using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        string name = Name(endpoint);
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(Options.AuthBaseUri, path)) { Content = contentFactory?.Invoke() };
            request.Headers.Add("apikey", Options.PublishableKey);
            if (bearer is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer.Reveal());
            }

            using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, linked.Token).ConfigureAwait(false);
            int status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
            {
                AuthResult<T> result = await onSuccess(response, linked.Token).ConfigureAwait(false);
                if (_logger is not null)
                {
                    RequestAnswered(_logger, name, status, null);
                }

                return result;
            }

            string? rawCode = await ReadErrorCodeAsync(response, linked.Token).ConfigureAwait(false);
            AuthErrorKind kind = Classify(endpoint, response.StatusCode, rawCode);
            string? serverCode = AuthServerCodes.Normalize(rawCode);
            if (_logger is not null)
            {
                RequestRefused(_logger, name, status, kind, serverCode ?? "none", null);
            }

            return AuthResult.Fail<T>(new AuthFailure(kind, ReadRetryAfter(response, _timeProvider), status, serverCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogUnreachable(name);
            return AuthResult.Fail<T>(AuthErrorKind.NetworkUnavailable);
        }
        catch (HttpRequestException)
        {
            LogUnreachable(name);
            return AuthResult.Fail<T>(AuthErrorKind.NetworkUnavailable);
        }
        catch (JsonException)
        {
            return Unreadable<T>(name);
        }
        catch (NotSupportedException)
        {
            return Unreadable<T>(name);
        }
    }

    private AuthResult<T> Unreadable<T>(string endpoint)
    {
        if (_logger is not null)
        {
            ResponseUnreadable(_logger, endpoint, null);
        }

        return AuthResult.Fail<T>(AuthErrorKind.Unexpected);
    }

    private void LogUnreachable(string endpoint)
    {
        if (_logger is not null)
        {
            RequestUnreachable(_logger, endpoint, AuthErrorKind.NetworkUnavailable, null);
        }
    }
}
