using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Api.IntegrationTests.Dispatch;

/// <summary>
/// Authenticates a request from test headers: <c>sub</c>, <c>amr</c> and <c>session_id</c>. A request without the subject header is unauthenticated.
/// </summary>
internal sealed class DispatchTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "DispatchTest";

    /// <summary>The header that names the subject.</summary>
    public const string SubjectHeader = "X-Test-Sub";

    /// <summary>The header that carries the <c>session_id</c> claim.</summary>
    public const string SessionHeader = "X-Test-Session";

    /// <summary>The header that carries one <c>amr</c> entry as JSON, for example <c>{"method":"password","timestamp":1}</c>.</summary>
    public const string AmrHeader = "X-Test-Amr";

    /// <summary>Initializes a new instance of the <see cref="DispatchTestAuthHandler"/> class.</summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public DispatchTestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SubjectHeader, out var subject) || string.IsNullOrWhiteSpace(subject))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim> { new("sub", subject.ToString()) };
        if (Request.Headers.TryGetValue(SessionHeader, out var session) && !string.IsNullOrWhiteSpace(session))
        {
            claims.Add(new Claim("session_id", session.ToString()));
        }

        if (Request.Headers.TryGetValue(AmrHeader, out var amr) && !string.IsNullOrWhiteSpace(amr))
        {
            claims.Add(new Claim("amr", amr.ToString()));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }

    /// <summary>Builds a fresh <c>amr</c> header value.</summary>
    /// <param name="method">The method. <c>token_refresh</c> does not count as a full sign-in.</param>
    /// <param name="timestamp">The Unix timestamp.</param>
    /// <returns>The JSON entry.</returns>
    public static string Amr(string method, long timestamp) =>
        "{\"method\":\"" + method + "\",\"timestamp\":" + timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}";
}
