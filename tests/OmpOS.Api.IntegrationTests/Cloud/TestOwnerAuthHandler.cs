using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OmpOS.Api.IntegrationTests.Cloud;

/// <summary>Authenticates a request as the owner named in the <c>X-Test-Owner</c> header, with that name as the <c>sub</c> claim the API reads. A request without the header is unauthenticated.</summary>
internal sealed class TestOwnerAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    /// <summary>The scheme name.</summary>
    public const string SchemeName = "TestOwner";

    /// <summary>The header that names the owner.</summary>
    public const string Header = "X-Test-Owner";

    /// <summary>Initializes a new instance of the <see cref="TestOwnerAuthHandler"/> class.</summary>
    /// <param name="options">The scheme options.</param>
    /// <param name="logger">The logger factory.</param>
    /// <param name="encoder">The URL encoder.</param>
    public TestOwnerAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Header, out var owner) || string.IsNullOrWhiteSpace(owner))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity([new Claim("sub", owner.ToString())], SchemeName);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}
