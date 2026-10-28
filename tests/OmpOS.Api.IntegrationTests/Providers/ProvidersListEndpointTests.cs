using System.Net;
using System.Text.Json;
using OmpOS.Api.Services.Providers;
using OmpOS.Harness.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Api.IntegrationTests.Providers;

/// <summary><c>GET /providers</c> and <c>GET /providers/{id}</c>: the catalogue with filters, the configured state of each row, the operator switches, and no key.</summary>
public sealed class ProvidersListEndpointTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheListHoldsEveryCatalogueRowInOrderWithTheOperatorSwitchesOff()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers", UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var root = body.RootElement;
        root.GetProperty("total").GetInt32().ShouldBe(CatalogueRows.All.Count);
        root.GetProperty("providers").EnumerateArray().Select(static provider => provider.GetProperty("id").GetString()).ShouldBe(CatalogueRows.All.Select(static row => row.Id));
        root.GetProperty("ompInstalled").GetBoolean().ShouldBeTrue();
        root.GetProperty("credentialWritesEnabled").GetBoolean().ShouldBeFalse();
        root.GetProperty("configWritesEnabled").GetBoolean().ShouldBeFalse();
        root.GetProperty("liveTestsEnabled").GetBoolean().ShouldBeFalse();
        root.GetProperty("defaultModel").GetString().ShouldBe("deepseek/deepseek-chat");
    }

    [Fact]
    public async Task TheOperatorSwitchesAreReportedWhenTurnedOn()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), ProvidersTestHost.AllOn());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers?search=zzzz-no-such-provider", UriKind.Relative), Token).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("providers").GetArrayLength().ShouldBe(0);
        body.RootElement.GetProperty("total").GetInt32().ShouldBe(CatalogueRows.All.Count);
        body.RootElement.GetProperty("credentialWritesEnabled").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("configWritesEnabled").GetBoolean().ShouldBeTrue();
        body.RootElement.GetProperty("liveTestsEnabled").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData("region=china", "china")]
    [InlineData("region=EUROPE", "europe")]
    [InlineData("region=united-states", "united-states")]
    [InlineData("region=local", "local")]
    [InlineData("region=global", "global")]
    public async Task TheRegionFilterKeepsOnlyThatRegion(string query, string region)
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers?" + query, UriKind.Relative), Token).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var providers = body.RootElement.GetProperty("providers").EnumerateArray().ToArray();
        providers.ShouldNotBeEmpty();
        providers.ShouldAllBe(provider => string.Equals(provider.GetProperty("region").GetString(), region, StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheKindFilterCombinesWithTheRegionFilter()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();
        var expected = CatalogueRows.All.Count(static row => row.Kind == CatalogueKind.CodingPlan && row.Region == CatalogueRegion.Cn);
        expected.ShouldBeGreaterThan(0);

        using var response = await client.GetAsync(new Uri("/providers?kind=coding-plan&region=china", UriKind.Relative), Token).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var providers = body.RootElement.GetProperty("providers").EnumerateArray().ToArray();
        providers.Length.ShouldBe(expected);
        providers.ShouldAllBe(static provider => string.Equals(provider.GetProperty("kind").GetString(), "coding-plan", StringComparison.Ordinal));
    }

    [Fact]
    public async Task TheSearchFilterIgnoresCaseAndMatchesTheIdTheNameAndAnAddress()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var byId = await client.GetAsync(new Uri("/providers?search=MOONSHOT", UriKind.Relative), Token).ConfigureAwait(true);
        using var byAddress = await client.GetAsync(new Uri("/providers?search=api.moonshot.cn", UriKind.Relative), Token).ConfigureAwait(true);

        using var idBody = await WebAppTestHost.ReadJsonAsync(byId, Token).ConfigureAwait(true);
        using var addressBody = await WebAppTestHost.ReadJsonAsync(byAddress, Token).ConfigureAwait(true);
        var ids = idBody.RootElement.GetProperty("providers").EnumerateArray().Select(static provider => provider.GetProperty("id").GetString()).ToArray();
        ids.ShouldContain("moonshot-cn", StringComparer.Ordinal);
        addressBody.RootElement.GetProperty("providers").EnumerateArray().Select(static provider => provider.GetProperty("id").GetString()).ShouldContain("moonshot-cn", StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("region=mars")]
    [InlineData("kind=free")]
    public async Task AnUnknownFilterValueIsAProblem(string query)
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers?" + query, UriKind.Relative), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_filter", Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task AnOverlongSearchTextIsAProblem()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers?search=" + new string('a', 101), UriKind.Relative), Token).ConfigureAwait(true);

        await WebAppTestHost.ShouldBeProblemAsync(response, HttpStatusCode.BadRequest, "invalid_filter", Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task TheConfiguredStateFollowsOmpAndTheEnvironment()
    {
        var backend = new FakeProviderBackend
        {
            State = FakeProviderBackend.Installed(
                credentialed: ["deepseek", "openai-codex"],
                blocks: new Dictionary<string, string?>(StringComparer.Ordinal) { ["moonshot-cn"] = "OMPOS_PROVIDER_MOONSHOT_CN_KEY", ["zai-api"] = "OMPOS_PROVIDER_ZAI_API_KEY" }),
        };
        using var factory = ProvidersTestHost.Dev(backend, environment: new FakeProviderEnvironment("OMPOS_PROVIDER_MOONSHOT_CN_KEY"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers", UriKind.Relative), Token).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        var states = body.RootElement.GetProperty("providers").EnumerateArray()
            .ToDictionary(static provider => provider.GetProperty("id").GetString()!, static provider => provider.GetProperty("configured").GetString()!, StringComparer.Ordinal);
        states["deepseek"].ShouldBe("configured");
        states["openai-codex"].ShouldBe("configured");
        states["moonshot-cn"].ShouldBe("configured");
        states["zai-api"].ShouldBe("not-configured");
        states["bigmodel"].ShouldBe("not-configured");
        states["ollama"].ShouldBe("not-required");
    }

    [Fact]
    public async Task WithoutOmpTheStateOfAKeyedProviderIsUnknownAndNotConfigured()
    {
        var backend = new FakeProviderBackend { State = ProviderBackendState.NoOmp };
        using var factory = ProvidersTestHost.Dev(backend);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers/deepseek", UriKind.Relative), Token).ConfigureAwait(true);

        using var body = await WebAppTestHost.ReadJsonAsync(response, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("configured").GetString().ShouldBe("unknown");
        using var list = await client.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);
        using var listBody = await WebAppTestHost.ReadJsonAsync(list, Token).ConfigureAwait(true);
        listBody.RootElement.GetProperty("ompInstalled").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task OneProviderIsReadByIdAndAnUnknownIdIs404()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();

        using var found = await client.GetAsync(new Uri("/providers/moonshot-cn", UriKind.Relative), Token).ConfigureAwait(true);
        using var missing = await client.GetAsync(new Uri("/providers/no-such-provider", UriKind.Relative), Token).ConfigureAwait(true);

        found.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = await WebAppTestHost.ReadJsonAsync(found, Token).ConfigureAwait(true);
        body.RootElement.GetProperty("displayName").GetString().ShouldBe(CatalogueRows.Find("moonshot-cn")!.DisplayName);
        body.RootElement.GetProperty("routes").GetArrayLength().ShouldBe(CatalogueRows.Find("moonshot-cn")!.Routes.Count);
        await WebAppTestHost.ShouldBeProblemAsync(missing, HttpStatusCode.NotFound, "provider_not_found", Token).ConfigureAwait(true);
    }

    [Fact]
    public async Task ACodingPlanRowCarriesItsRuleTextAndASignInRowItsLoginCommand()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend());
        using var client = factory.CreateClient();
        var plan = CatalogueRows.All.First(static row => row.Plan is not null && row.Routes.Count > 0);
        var signIn = CatalogueRows.All.First(static row => row.Routes.Count == 0);

        using var planResponse = await client.GetAsync(new Uri("/providers/" + plan.Id, UriKind.Relative), Token).ConfigureAwait(true);
        using var signInResponse = await client.GetAsync(new Uri("/providers/" + signIn.Id, UriKind.Relative), Token).ConfigureAwait(true);

        using var planBody = await WebAppTestHost.ReadJsonAsync(planResponse, Token).ConfigureAwait(true);
        planBody.RootElement.GetProperty("plan").GetProperty("ruleText").GetString().ShouldBe(plan.Plan!.RuleText);
        using var signInBody = await WebAppTestHost.ReadJsonAsync(signInResponse, Token).ConfigureAwait(true);
        signInBody.RootElement.GetProperty("signInOnly").GetBoolean().ShouldBeTrue();
        signInBody.RootElement.GetProperty("takesKey").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task NoResponseHoldsAValueOfAKeyVariable()
    {
        using var factory = ProvidersTestHost.Dev(new FakeProviderBackend(), environment: new FakeProviderEnvironment("DEEPSEEK_API_KEY"));
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/providers", UriKind.Relative), Token).ConfigureAwait(true);

        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(text);
        document.RootElement.GetProperty("providers").EnumerateArray()
            .Single(static provider => string.Equals(provider.GetProperty("id").GetString(), "deepseek", StringComparison.Ordinal))
            .GetProperty("apiKeyEnv").GetString().ShouldBe("DEEPSEEK_API_KEY");
        text.ShouldNotContain("\"apiKey\"", Case.Sensitive);
        text.ShouldNotContain("\"secret\"", Case.Sensitive);
    }

    [Fact]
    public async Task AJwtHostAnswers401WithoutAToken()
    {
        using var factory = ProvidersTestHost.Jwt(new FakeProviderBackend());
        using var anonymous = factory.CreateClient();
        using var signedIn = WebAppTestHost.AsPrincipal(factory, "alice");

        using var denied = await anonymous.GetAsync(new Uri("/providers", UriKind.Relative), Token).ConfigureAwait(true);
        using var allowed = await signedIn.GetAsync(new Uri("/providers?search=zzzz", UriKind.Relative), Token).ConfigureAwait(true);

        denied.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
