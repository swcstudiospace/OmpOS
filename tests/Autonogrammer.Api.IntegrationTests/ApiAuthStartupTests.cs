using System.Net;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests;

/// <summary>
/// OD-03 fail-closed startup: outside Development a host refuses to start without an HTTPS authority and an audience, and refuses
/// the development principal. A host with a complete configuration starts and answers protected requests with 401.
/// </summary>
public sealed class ApiAuthStartupTests
{
    /// <summary>Verifies a production host without Api:Auth:Authority refuses to start and states the configured message.</summary>
    [Fact]
    public void Production_WithoutAuthority_FailsStartupWithConfiguredMessage()
    {
        var config = JwtTestHost.ProductionConfig();
        config.Remove("Api:Auth:Authority");

        StartupFailureOf(config, "Production").ShouldContain("Api:Auth:Authority is required outside the Development environment.");
    }

    /// <summary>Verifies a production host without Api:Auth:Audience refuses to start and states the configured message.</summary>
    [Fact]
    public void Production_WithoutAudience_FailsStartupWithConfiguredMessage()
    {
        var config = JwtTestHost.ProductionConfig();
        config.Remove("Api:Auth:Audience");

        StartupFailureOf(config, "Production").ShouldContain("Api:Auth:Audience is required outside the Development environment.");
    }

    /// <summary>Verifies an authority that is not an absolute https URI stops startup and names the key.</summary>
    [Fact]
    public void Production_NonHttpsAuthority_FailsStartupNamingKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Api:Auth:Authority"] = "http://issuer.test.invalid/";

        StartupFailureOf(config, "Production").ShouldContain("Api:Auth:Authority must be an absolute https URI.");
    }

    /// <summary>Verifies the development principal opt-in is refused outside Development and names the key.</summary>
    [Fact]
    public void Production_DevelopmentPrincipalOptIn_FailsStartupNamingKey()
    {
        var config = JwtTestHost.ProductionConfig();
        config["Api:Auth:AllowDevelopmentPrincipal"] = "true";

        StartupFailureOf(config, "Production").ShouldContain("Api:Auth:AllowDevelopmentPrincipal is allowed only in the Development environment.");
    }

    /// <summary>Verifies a production host with an authority and audience starts, answers health, and rejects anonymous protected requests.</summary>
    [Fact]
    public async Task Production_WithAuthorityAndAudience_StartsAndRequiresTokens()
    {
        using var factory = new ApiFactory(JwtTestHost.ProductionConfig(), environment: "Production");
        using var client = factory.CreateClient();
        var testToken = TestContext.Current.CancellationToken;

        using var live = await client.GetAsync(new Uri("/health/live", UriKind.Relative), testToken).ConfigureAwait(true);
        using var sessions = await client.GetAsync(new Uri("/sessions", UriKind.Relative), testToken).ConfigureAwait(true);
        using var openApi = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), testToken).ConfigureAwait(true);

        live.StatusCode.ShouldBe(HttpStatusCode.OK);
        sessions.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        openApi.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string StartupFailureOf(Dictionary<string, string?> config, string environment)
    {
        using var factory = new ApiFactory(config, environment: environment);

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        return StartupFailureText.Of(caught);
    }
}
