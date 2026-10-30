using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Security;

/// <summary>The anonymous <c>GET /auth/config</c> endpoint (contract C1).</summary>
public sealed class AuthConfigEndpointTests
{
    /// <summary>Verifies owner-token mode answers its mode with null authority and audience, uncached and without secrets.</summary>
    [Fact]
    public async Task OwnerTokenMode_AnswersModeWithoutSecrets()
    {
        using var factory = OwnerTokenTestHost.CreateFactory();
        using var client = factory.CreateClient();

        var (response, text, json) = await GetAsync(client).ConfigureAwait(true);
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
            json.GetProperty("mode").GetString().ShouldBe("owner-token");
            json.GetProperty("authority").ValueKind.ShouldBe(JsonValueKind.Null);
            json.GetProperty("audience").ValueKind.ShouldBe(JsonValueKind.Null);
            text.ShouldNotContain(OwnerTokenTestHost.Token);
            text.ShouldNotContain(OwnerTokenTestHost.Digest(OwnerTokenTestHost.Token));
        }
    }

    /// <summary>Verifies a Development host with the development principal answers <c>development</c>.</summary>
    [Fact]
    public async Task DevelopmentPrincipal_AnswersDevelopment()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var (response, _, json) = await GetAsync(client).ConfigureAwait(true);
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            json.GetProperty("mode").GetString().ShouldBe("development");
        }
    }

    /// <summary>Verifies an OIDC host answers its authority and audience and requires no credential.</summary>
    [Fact]
    public async Task OidcMode_AnswersAuthorityAndAudienceAnonymously()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:Authority"] = JwtTestHost.Issuer;
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();

        var (response, _, json) = await GetAsync(client).ConfigureAwait(true);
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.CacheControl!.NoStore.ShouldBeTrue();
            json.GetProperty("mode").GetString().ShouldBe("oidc");
            json.GetProperty("authority").GetString().ShouldBe(JwtTestHost.Issuer);
            json.GetProperty("audience").GetString().ShouldBe(JwtTestHost.Audience);
            json.TryGetProperty("projectUrl", out _).ShouldBeFalse();
        }
    }

    /// <summary>Verifies an OIDC host with a client block serves the public sign-in settings and caches them.</summary>
    [Fact]
    public async Task OidcMode_WithClientBlock_AnswersThePublicSettingsAndCachesThem()
    {
        var config = JwtTestHost.DevelopmentConfig();
        config["Api:Auth:Authority"] = JwtTestHost.Issuer;
        config["Api:Auth:Client:ProjectUrl"] = "https://project.example";
        config["Api:Auth:Client:PublishableKey"] = "sb_publishable_test";
        config["Api:Auth:Client:MagicLinkEnabled"] = "true";
        config["Api:Auth:Client:EmailCodeEnabled"] = "true";
        config["Api:Auth:Client:SignupEmailHasCode"] = "true";
        using var factory = new ApiFactory(config, JwtTestHost.InjectStaticMetadata);
        using var client = factory.CreateClient();

        var (response, _, json) = await GetAsync(client).ConfigureAwait(true);
        using (response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            response.Headers.CacheControl!.ToString().ShouldContain("public");
            response.Headers.CacheControl.MaxAge.ShouldBe(TimeSpan.FromSeconds(300));
            json.GetProperty("mode").GetString().ShouldBe("oidc");
            json.GetProperty("authority").GetString().ShouldBe(JwtTestHost.Issuer);
            json.GetProperty("audience").GetString().ShouldBe(JwtTestHost.Audience);
            json.GetProperty("projectUrl").GetString().ShouldBe("https://project.example");
            json.GetProperty("publishableKey").GetString().ShouldBe("sb_publishable_test");
            json.GetProperty("methods").GetProperty("emailCode").GetBoolean().ShouldBeTrue();
            json.GetProperty("methods").GetProperty("magicLink").GetBoolean().ShouldBeTrue();
            json.GetProperty("emailCode").GetProperty("expirySeconds").GetInt32().ShouldBe(3600);
            json.GetProperty("emailCode").GetProperty("resendCooldownSeconds").GetInt32().ShouldBe(60);
            json.GetProperty("signupEmailHasCode").GetBoolean().ShouldBeTrue();
        }
    }

    private static async Task<(HttpResponseMessage Response, string Text, JsonElement Json)> GetAsync(HttpClient client)
    {
        var testToken = TestContext.Current.CancellationToken;
        var response = await client.GetAsync(new Uri("/auth/config", UriKind.Relative), testToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(testToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(text);
        return (response, text, document.RootElement.Clone());
    }
}
