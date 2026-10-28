using System.Net;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Autonogrammer.Api.IntegrationTests.Providers;

/// <summary>What the host guarantees around the provider endpoints: unusable <c>Providers:*</c> settings stop startup, requests are rate limited, and the OpenAPI document describes every route and overrides no authentication.</summary>
public sealed class ProvidersHostContractTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("Providers:RequestsPerMinute", "0")]
    [InlineData("Providers:RequestsPerMinute", "100001")]
    [InlineData("Providers:TestTimeoutSeconds", "0")]
    [InlineData("Providers:TestTimeoutSeconds", "31")]
    [InlineData("Providers:StateCacheSeconds", "-1")]
    [InlineData("Providers:OmpPath", "relative/omp")]
    [InlineData("Providers:AgentDirectory", "relative/agent")]
    public void AnUnusableSettingFailsStartupNamingItsKey(string key, string value)
    {
        using var factory = new ApiFactory(new Dictionary<string, string?>(StringComparer.Ordinal) { [key] = value });

        var caught = Assert.ThrowsAny<Exception>(() =>
        {
            using var client = factory.CreateClient();
        });

        StartupFailureText.Of(caught).ShouldContain(key);
    }

    [Fact]
    public async Task EveryOperatorSwitchIsOffWhenNothingIsConfigured()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("credentialWritesEnabled").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("configWritesEnabled").GetBoolean().ShouldBeFalse();
        body.RootElement.GetProperty("liveTestsEnabled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task TheProviderEndpointsAreRateLimitedWithARetryHint()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), new Dictionary<string, string?>(StringComparer.Ordinal) { ["Providers:RequestsPerMinute"] = "1" });
        using var client = factory.CreateClient();

        using var first = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var second = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        second.Headers.RetryAfter.ShouldNotBeNull();
    }

    [Fact]
    public async Task TheOpenApiDocumentListsEveryProviderRouteAndNoneOptsOutOfAuthentication()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/openapi/v1.json", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        var paths = document.RootElement.GetProperty("paths");
        AssertOperation(paths, "/providers", "get", ["200", "400", "429"]);
        AssertOperation(paths, "/providers/{id}", "get", ["200", "404"]);
        AssertOperation(paths, "/providers/{id}/credential", "put", ["200", "400", "403", "404", "409", "422", "503"]);
        AssertOperation(paths, "/models/default", "put", ["200", "400", "403", "422"]);
        AssertOperation(paths, "/providers/custom", "post", ["200", "400", "403", "409", "422"]);
        AssertOperation(paths, "/providers/custom/{id}", "delete", ["204", "403", "404"]);
        AssertOperation(paths, "/providers/{id}/test", "post", ["200", "404"]);
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        schemas.TryGetProperty("SetCredentialRequest", out _).ShouldBeTrue();
        schemas.TryGetProperty("ProviderSummary", out _).ShouldBeTrue();
        schemas.GetProperty("SetCredentialRequest").GetProperty("properties").EnumerateObject().Select(static property => property.Name)
            .Order(StringComparer.Ordinal).ShouldBe(["apiKey", "planRuleAccepted"]);
    }

    private static void AssertOperation(JsonElement paths, string path, string method, string[] statuses)
    {
        paths.TryGetProperty(path, out var item).ShouldBeTrue(path);
        item.TryGetProperty(method, out var operation).ShouldBeTrue($"{method} {path}");
        operation.TryGetProperty("security", out var security).ShouldBeFalse($"{method} {path} must not override the default authentication with {security}");
        var responses = operation.GetProperty("responses");
        foreach (var status in statuses)
        {
            responses.TryGetProperty(status, out _).ShouldBeTrue($"{method} {path} lists {status}");
        }
    }
}
