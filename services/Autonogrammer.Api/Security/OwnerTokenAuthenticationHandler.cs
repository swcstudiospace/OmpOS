using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Autonogrammer.Api.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Autonogrammer.Api.Security;

/// <summary>
/// Authenticates the single operator's owner token. The token comes from <c>Authorization: Bearer</c> only (never the query string
/// or a cookie). Its SHA-256 is compared in fixed time with every configured digest and the result is combined without an early
/// exit, so neither the timing nor the response reveals which digest matched. A failure never echoes the token. Failed attempts are
/// counted per client address by <see cref="OwnerTokenFailureLimiter"/>; past the budget the challenge answers 429 with
/// <c>Retry-After</c>. A correct token is never throttled.
/// </summary>
internal sealed class OwnerTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string BearerPrefix = "Bearer";
    private const int AddressHashLength = 8;

    private readonly IOptions<ApiAuthOptions> _auth;
    private readonly OwnerTokenFailureLimiter _limiter;
    private readonly ILogger<OwnerTokenAuthenticationHandler> _logger;

    /// <summary>Initializes a new instance of the <see cref="OwnerTokenAuthenticationHandler"/> class.</summary>
    /// <param name="options">The scheme options monitor.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    /// <param name="auth">The validated API authentication options.</param>
    /// <param name="limiter">The failed-attempt throttle.</param>
    public OwnerTokenAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IOptions<ApiAuthOptions> auth,
        OwnerTokenFailureLimiter limiter)
        : base(options, loggerFactory, encoder)
    {
        _auth = auth;
        _limiter = limiter;
        _logger = loggerFactory.CreateLogger<OwnerTokenAuthenticationHandler>();
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = ReadToken(Request.Headers.Authorization.ToString());
        if (token is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var settings = _auth.Value.OwnerToken;
        if (Matches(token, settings.Sha256))
        {
            var identity = new ClaimsIdentity(
                [new Claim(JwtBearerSetup.SubjectClaimType, settings.Subject)],
                OwnerTokenDefaults.AuthenticationType,
                JwtBearerSetup.SubjectClaimType,
                ClaimsIdentity.DefaultRoleClaimType);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        var addressKey = RateLimitPartitionKey.ForAddress(Context);
        var addressHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(addressKey)))[..AddressHashLength];
        var retryAfter = _limiter.RegisterFailure(addressKey, settings.MaxFailuresPerMinute);
        if (retryAfter is { } wait)
        {
            Context.Items[OwnerTokenDefaults.ThrottledItem] = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
            OwnerTokenLog.Throttled(_logger, addressHash);
        }
        else
        {
            OwnerTokenLog.Rejected(_logger, addressHash);
        }

        return Task.FromResult(AuthenticateResult.Fail("The owner token was not accepted."));
    }

    /// <inheritdoc />
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        if (Context.Items.TryGetValue(OwnerTokenDefaults.ThrottledItem, out var seconds) && seconds is int retryAfter)
        {
            Response.StatusCode = StatusCodes.Status429TooManyRequests;
            Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        }

        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.Headers.Append(HeaderNames.WWWAuthenticate, BearerPrefix);
        return Task.CompletedTask;
    }

    /// <summary>Returns the token of a <c>Bearer</c> header, or null when the header is absent, another scheme, or has no usable token.</summary>
    private static string? ReadToken(string header)
    {
        if (!header.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) || header.Length == BearerPrefix.Length || header[BearerPrefix.Length] != ' ')
        {
            return null;
        }

        var token = header[(BearerPrefix.Length + 1)..].Trim(' ');
        // An empty or malformed token is a failed attempt, not an absent credential: map it to a value that matches no digest.
        return token.Length == 0 || token.Any(char.IsWhiteSpace) ? string.Empty : token;
    }

    private static bool Matches(string token, IList<string> digests)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var matched = false;
        foreach (var digest in digests)
        {
            matched |= CryptographicOperations.FixedTimeEquals(hash, Convert.FromHexString(digest));
        }

        return matched && token.Length > 0;
    }
}
