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
