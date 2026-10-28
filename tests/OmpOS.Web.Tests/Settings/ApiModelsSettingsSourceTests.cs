using System.Net;
using System.Text;
using System.Text.Json;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;
using OmpOS.Web.Services.Settings;
using Shouldly;
using Xunit;
using Wire = OmpOS.Contracts.Providers;

namespace OmpOS.Web.Tests.Settings;

/// <summary>
/// <see cref="ApiModelsSettingsSource"/> over a scripted handler: the provider list becomes a snapshot with listings and cards, every write goes
/// to its endpoint with the shape the API documents, a problem of the API becomes a failed result with its own sentence, and when the operator
/// disabled key writes the source says so and sends no key.
/// </summary>
public sealed class ApiModelsSettingsSourceTests
{
    private const string Canary = "sk-canary-web-key-4f19c2d8e7a35b61";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task TheProviderListBecomesListingsConfiguredCardsAndCustomCards()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var queued = Json(HttpStatusCode.OK, Listing(credentialWrites: true));
        handler.Enqueue(queued);

        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);

        handler.Requests.Single().Method.ShouldBe(HttpMethod.Get);
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/providers");
        snapshot.IsComplete.ShouldBeTrue();
        snapshot.DefaultModel.ShouldBe("kimi-code/k2");
        var listings = snapshot.Listings.ShouldNotBeNull();
        listings.Select(static listing => listing.Id).ShouldBe(["kimi-global", "ark"]);
        listings[0].Routes[0].AuthText.ShouldBe("Bearer");
        listings[1].Plan.ShouldNotBeNull().InteractiveOnly.ShouldBeTrue();
        var configured = snapshot.Providers.Single(static provider => provider.Group != ProviderGroup.Custom);
        configured.Id.ShouldBe("kimi-code");
        configured.Credential.ShouldBe(CredentialState.Present);
        var custom = snapshot.Providers.Single(static provider => provider.Group == ProviderGroup.Custom);
        custom.Id.ShouldBe("mine");
        custom.Definition.ShouldNotBeNull().Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
        custom.Models.ShouldBe(["mine/m1"]);
        snapshot.Warning.ShouldBeNull();
    }

    [Fact]
    public async Task WhenTheOperatorDisabledKeyWritesTheSourceSaysSoAndSendsNoKey()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var queued = Json(HttpStatusCode.OK, Listing(credentialWrites: false));
        handler.Enqueue(queued);
        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);

        var result = await source.SaveApiKeyAsync("kimi-code", Canary, Token).ConfigureAwait(true);

        source.CredentialWritesDisabledReason.ShouldBe(snapshot.KeyWritesDisabledReason);
        source.CredentialWritesDisabledReason.ShouldBe(ModelsApiCopy.CredentialWritesDisabled);
        snapshot.Warning.ShouldBeNull("the disabled key is the page's key field state, not a configuration warning");
        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldBe(source.CredentialWritesDisabledReason);
        handler.Requests.Count.ShouldBe(1, "only the list was requested; the key never left the page");
    }

    [Fact]
    public async Task InTheDemoTheDisabledKeySentenceNamesTheDemo()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http, isDemo: true);
        using var queued = Json(HttpStatusCode.OK, Listing(credentialWrites: false));
        handler.Enqueue(queued);

        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.KeyWritesDisabledReason.ShouldBe(ModelsApiCopy.CredentialWritesDisabledInDemo);
        snapshot.KeyWritesDisabledReason.ShouldNotBeNull().ShouldContain("disabled by the operator in this Demo", Case.Sensitive);
    }

    [Fact]
    public async Task WhenKeyWritesAreEnabledThereIsNoDisabledReason()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var queued = Json(HttpStatusCode.OK, Listing(credentialWrites: true));
        handler.Enqueue(queued);

        var snapshot = await source.LoadAsync(Token).ConfigureAwait(true);

        snapshot.KeyWritesDisabledReason.ShouldBeNull();
        source.CredentialWritesDisabledReason.ShouldBeNull();
    }

    [Fact]
    public async Task AKeyForACodingPlanCarriesThePlanRuleTickTheCardAlreadyDemanded()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        var plan = new Wire.ProviderPlanInfo { RuleText = "Interactive only.", InteractiveOnly = true, SeparateKey = true, PlanUrl = new Uri("https://example.com/plan", UriKind.Absolute), Tiers = [], ListedTools = [] };
        var response = new Wire.ProvidersResponse
        {
            Providers = [Summary("zai-plan", "zai-code", Wire.ProviderKind.CodingPlan, plan, Wire.ProviderConfiguredState.NotConfigured), Summary("plain", "plain-api", Wire.ProviderKind.Api, plan: null, Wire.ProviderConfiguredState.NotConfigured)],
            Total = 2,
            Custom = [],
            OmpInstalled = true,
            CredentialWritesEnabled = true,
            ConfigWritesEnabled = true,
            LiveTestsEnabled = false,
        };
        using var list = Json(HttpStatusCode.OK, response);
        using var okPlan = new HttpResponseMessage(HttpStatusCode.OK);
        using var okPlain = new HttpResponseMessage(HttpStatusCode.OK);
        handler.Enqueue(list);
        handler.Enqueue(okPlan);
        handler.Enqueue(okPlain);
        _ = await source.LoadAsync(Token).ConfigureAwait(true);

        _ = await source.SaveApiKeyAsync("zai-code", Canary, Token).ConfigureAwait(true);
        _ = await source.SaveApiKeyAsync("plain-api", Canary, Token).ConfigureAwait(true);

        using var planBody = JsonDocument.Parse(await handler.Requests[1].Content!.ReadAsStringAsync(Token).ConfigureAwait(true));
        using var plainBody = JsonDocument.Parse(await handler.Requests[2].Content!.ReadAsStringAsync(Token).ConfigureAwait(true));
        handler.Requests[1].RequestUri!.AbsolutePath.ShouldBe("/providers/zai-plan/credential");
        planBody.RootElement.GetProperty("planRuleAccepted").GetBoolean().ShouldBeTrue();
        plainBody.RootElement.GetProperty("planRuleAccepted").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task AKeyForAnOmpIdGoesToTheCatalogueIdOfItsListingOnce()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var list = Json(HttpStatusCode.OK, Listing(credentialWrites: true));
        using var ok = new HttpResponseMessage(HttpStatusCode.OK);
        handler.Enqueue(list);
        handler.Enqueue(ok);
        _ = await source.LoadAsync(Token).ConfigureAwait(true);

        var result = await source.SaveApiKeyAsync("kimi-code", Canary, planRuleAccepted: true, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeTrue();
        result.Message.ShouldBeNull();
        var request = handler.Requests[1];
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.AbsolutePath.ShouldBe("/providers/kimi-global/credential");
        var body = await request.Content!.ReadAsStringAsync(Token).ConfigureAwait(true);
        using var document = JsonDocument.Parse(body);
        document.RootElement.GetProperty("apiKey").GetString().ShouldBe(Canary);
        document.RootElement.GetProperty("planRuleAccepted").GetBoolean().ShouldBeTrue();
        request.RequestUri.Query.ShouldBeEmpty();
        request.RequestUri.AbsoluteUri.ShouldNotContain(Canary, Case.Sensitive);
    }

    [Theory]
    [InlineData("")]
    [InlineData("two words")]
    public async Task AKeyThatIsBlankOrHasWhitespaceIsRefusedBeforeAnyRequest(string key)
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var list = Json(HttpStatusCode.OK, Listing(credentialWrites: true));
        handler.Enqueue(list);
        _ = await source.LoadAsync(Token).ConfigureAwait(true);

        var result = await source.SaveApiKeyAsync("kimi-code", key, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AProviderThatTheLastListDidNotHoldIsRefusedWithoutARequest()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);

        var result = await source.SaveApiKeyAsync("deepseek", Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldNotContain(Canary, Case.Sensitive);
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(403, "credential_writes_disabled", "ignored", "disabled by the operator")]
    [InlineData(422, "credential_rejected", "omp refused the key.", "omp refused the key.")]
    [InlineData(409, "credential_operator_managed", "Set it in OMPOS_PROVIDER_X_KEY.", "OMPOS_PROVIDER_X_KEY")]
    public async Task AProblemOfTheApiBecomesAFailedResultWithItsOwnSentence(int status, string code, string detail, string expected)
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var list = Json(HttpStatusCode.OK, Listing(credentialWrites: true));
        using var problem = Problem((HttpStatusCode)status, code, detail);
        handler.Enqueue(list);
        handler.Enqueue(problem);
        _ = await source.LoadAsync(Token).ConfigureAwait(true);

        var result = await source.SaveApiKeyAsync("kimi-code", Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNull().ShouldContain(expected, Case.Sensitive);
        result.Message.ShouldNotBeNull().ShouldNotContain(Canary, Case.Sensitive);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Sign in")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many requests")]
    public async Task AnAuthOrRateProblemHasAFixedSentence(HttpStatusCode status, string expected)
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var response = new HttpResponseMessage(status);
        handler.Enqueue(response);

        var thrown = await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);

        thrown.Message.ShouldContain(expected, Case.Sensitive);
    }

    [Fact]
    public async Task AnUnreachableServiceIsASettingsExceptionOnLoadAndAFailureOnAWrite()
    {
        using var handler = new ThrowingHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);

        await Should.ThrowAsync<SettingsSourceException>(() => source.LoadAsync(Token)).ConfigureAwait(true);
        var result = await source.SetDefaultModelAsync("a/b", Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ADefaultModelAndACustomProviderGoToTheirEndpointsAndARemovalToItsUrl()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        using var one = new HttpResponseMessage(HttpStatusCode.OK);
        using var two = new HttpResponseMessage(HttpStatusCode.OK);
        using var three = new HttpResponseMessage(HttpStatusCode.NoContent);
        handler.Enqueue(one);
        handler.Enqueue(two);
        handler.Enqueue(three);
        var form = new CustomProviderForm(
            "Mine",
            "mine",
            CustomProviderProtocol.AnthropicCompatible,
            new Uri("https://llm.example.com/v1"),
            CustomProviderAuth.None,
            ["m1", "m2"],
            CustomProviderCapabilities.Chat,
            AllowRemotePlainHttp: false);

        (await source.SetDefaultModelAsync("mine/m1", Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await source.SaveCustomProviderAsync(form, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await source.RemoveCustomProviderAsync("mine", Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        handler.Requests[0].Method.ShouldBe(HttpMethod.Put);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/models/default");
        (await handler.Requests[0].Content!.ReadAsStringAsync(Token).ConfigureAwait(true)).ShouldBe("""{"model":"mine/m1"}""");
        handler.Requests[1].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[1].RequestUri!.AbsolutePath.ShouldBe("/providers/custom");
        using var custom = JsonDocument.Parse(await handler.Requests[1].Content!.ReadAsStringAsync(Token).ConfigureAwait(true));
        custom.RootElement.GetProperty("protocol").GetString().ShouldBe("anthropic");
        custom.RootElement.GetProperty("keyRequired").GetBoolean().ShouldBeFalse();
        custom.RootElement.GetProperty("baseUrl").GetString().ShouldBe("https://llm.example.com/v1");
        custom.RootElement.GetProperty("modelIds").EnumerateArray().Select(static id => id.GetString()).ShouldBe(["m1", "m2"]);
        handler.Requests[2].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[2].RequestUri!.AbsolutePath.ShouldBe("/providers/custom/mine");
    }

    [Theory]
    [InlineData(Wire.ProviderTestStatus.Reachable, ProviderTestStatus.Reachable)]
    [InlineData(Wire.ProviderTestStatus.Unauthorized, ProviderTestStatus.Unauthorized)]
    [InlineData(Wire.ProviderTestStatus.Unreachable, ProviderTestStatus.NotRunning)]
    [InlineData(Wire.ProviderTestStatus.Skipped, ProviderTestStatus.NotTestable)]
    [InlineData(Wire.ProviderTestStatus.Blocked, ProviderTestStatus.NotTestable)]
    public async Task ATestAnswerMapsToThePageOutcome(Wire.ProviderTestStatus wire, ProviderTestStatus expected)
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);
        var json = JsonSerializer.Serialize(new Wire.ProviderTestResponse { ProviderId = "x", Status = wire, Reason = "why" }, Wire.ProvidersJsonContext.Default.ProviderTestResponse);
        using var queued = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        handler.Enqueue(queued);

        var outcome = await source.TestAsync("x", Token).ConfigureAwait(true);

        outcome.Status.ShouldBe(expected);
        outcome.Message.ShouldBe("why");
        handler.Requests.Single().Method.ShouldBe(HttpMethod.Post);
        handler.Requests.Single().RequestUri!.AbsolutePath.ShouldBe("/providers/x/test");
    }

    [Fact]
    public async Task ACatalogueProviderOmpDoesNotKnowIsRefusedWithAnExplanation()
    {
        using var handler = new ScriptedHandler();
        using var http = ClientHttp(handler);
        var source = new ApiModelsSettingsSource(http);

        var result = await source.AddCatalogueProviderAsync(new CatalogueProviderRequest("x", "openai", new Dictionary<string, string>(StringComparer.Ordinal), []), Canary, Token).ConfigureAwait(true);

        result.Succeeded.ShouldBeFalse();
        result.Message.ShouldNotBeNullOrWhiteSpace();
        handler.Requests.ShouldBeEmpty();
    }

    private static Wire.ProvidersResponse Listing(bool credentialWrites) => new()
    {
        Providers =
        [
            Summary("kimi-global", "kimi-code", Wire.ProviderKind.Api, plan: null, Wire.ProviderConfiguredState.Configured),
            Summary("ark", null, Wire.ProviderKind.CodingPlan, new Wire.ProviderPlanInfo { RuleText = "Interactive only.", InteractiveOnly = true, SeparateKey = true, PlanUrl = new Uri("https://example.com/plan", UriKind.Absolute), Tiers = [], ListedTools = [] }, Wire.ProviderConfiguredState.NotConfigured),
        ],
        Total = 2,
        Custom = [new Wire.CustomProviderInfo { Id = "mine", DisplayName = "Mine", Protocol = Wire.CustomProviderProtocol.Anthropic, BaseAddress = "https://llm.example.com/v1", ModelIds = ["m1"], KeyEnvName = "OMPOS_PROVIDER_MINE_KEY", Configured = Wire.ProviderConfiguredState.NotConfigured }],
        OmpInstalled = true,
        CredentialWritesEnabled = credentialWrites,
        ConfigWritesEnabled = true,
        LiveTestsEnabled = false,
        DefaultModel = "kimi-code/k2",
    };

    private static Wire.ProviderSummary Summary(string id, string? ompId, Wire.ProviderKind kind, Wire.ProviderPlanInfo? plan, Wire.ProviderConfiguredState configured) => new()
    {
        Id = id,
        DisplayName = id,
        Region = Wire.ProviderRegion.Global,
        Kind = kind,
        Status = Wire.ProviderVerification.Verified,
        StatusDate = new DateOnly(2026, 10, 27),
        OmpProviderId = ompId,
        TakesKey = true,
        SignInOnly = false,
        Routes = [new Wire.ProviderRouteInfo { Id = "openai", Api = "openai-completions", BaseAddressTemplate = "https://api.example.com/v1", Auth = "Bearer", TakesKey = true, DiscoversModels = false, NeedsUserModels = true, DocumentedModels = [] }],
        UrlVariables = [],
        SourceUrl = new Uri("https://example.com/docs", UriKind.Absolute),
        Plan = plan,
        Configured = configured,
    };

    private static HttpResponseMessage Json(HttpStatusCode status, Wire.ProvidersResponse value) =>
        new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value, Wire.ProvidersJsonContext.Default.ProvidersResponse), Encoding.UTF8, "application/json"),
        };

    private static HttpResponseMessage Problem(HttpStatusCode status, string code, string detail) =>
        new(status)
        {
            Content = new StringContent(
                $$"""{"type":"/problems/{{code}}","detail":"{{detail}}"}""",
                Encoding.UTF8,
                "application/problem+json"),
        };

    private static HttpClient ClientHttp(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://127.0.0.1:5187/", UriKind.Absolute) };
}
