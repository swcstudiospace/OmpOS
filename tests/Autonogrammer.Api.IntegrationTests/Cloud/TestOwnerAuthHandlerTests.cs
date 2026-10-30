using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Cloud;

/// <summary>The test host's authentication: the owner named in the header becomes the <c>sub</c> claim the API reads, and a request without a name is unauthenticated.</summary>
public sealed class TestOwnerAuthHandlerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheNamedOwnerBecomesTheSubjectClaim()
    {
        var result = await AuthenticateAsync("alice").ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        result.Principal.ShouldNotBeNull().FindFirst("sub").ShouldNotBeNull().Value.ShouldBe("alice");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ARequestWithoutAnOwnerIsNotAuthenticated(string? owner)
    {
        var result = await AuthenticateAsync(owner).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.None.ShouldBeTrue();
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(string? owner)
    {
        var handler = new TestOwnerAuthHandler(new FixedOptionsMonitor(), NullLoggerFactory.Instance, UrlEncoder.Default);
        var context = new DefaultHttpContext();
        if (owner is not null)
        {
            context.Request.Headers[TestOwnerAuthHandler.Header] = owner;
        }

        await handler.InitializeAsync(new AuthenticationScheme(TestOwnerAuthHandler.SchemeName, null, typeof(TestOwnerAuthHandler)), context).ConfigureAwait(true);
        return await handler.AuthenticateAsync().ConfigureAwait(true);
    }

    private sealed class FixedOptionsMonitor : IOptionsMonitor<AuthenticationSchemeOptions>
    {
        private readonly AuthenticationSchemeOptions _options = new();

        public AuthenticationSchemeOptions CurrentValue => _options;

        public AuthenticationSchemeOptions Get(string? name) => _options;

        public IDisposable? OnChange(Action<AuthenticationSchemeOptions, string?> listener) => null;
    }
}
