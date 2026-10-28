using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Autonogrammer.Contracts.Providers;
using Autonogrammer.Harness.Providers;
using Shouldly;
using Xunit;
using ProviderKind = Autonogrammer.Contracts.Providers.ProviderKind;

namespace Autonogrammer.Web.Tests.Settings;

/// <summary>
/// The provider routes of the Demo handler: the real catalogue with its EU, US and CN coverage, the filters of the real route, the refused key
/// write, the skipped live test, custom endpoints under the table of the real route, and the default model.
/// </summary>
public sealed class DemoProvidersRouteTests : IDisposable
{
    private const string Canary = "sk-demo-canary-77f3a91c20e4b685";

    private readonly DemoTestHost _host = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>The unfiltered list holds every row of the real catalogue and says what the Demo's operator switched on.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task TheListHoldsEveryRowOfTheRealCatalogue()
    {
        var response = await ListAsync(string.Empty).ConfigureAwait(true);

        response.Total.ShouldBe(CatalogueRows.All.Count);
        response.Providers.Count.ShouldBe(CatalogueRows.All.Count);
        response.Providers.Select(static provider => provider.Id).ShouldBe(CatalogueRows.All.Select(static row => row.Id));
        response.CredentialWritesEnabled.ShouldBeFalse();
        response.ConfigWritesEnabled.ShouldBeTrue();
        response.LiveTestsEnabled.ShouldBeFalse();
        response.OmpInstalled.ShouldBeTrue();
        response.DefaultModel.ShouldBe("anthropic/claude-sonnet-4-5");
    }

    /// <summary>The Demo proves the coverage: the EU, US and CN regions each hold providers and coding plans, with residency notes and plan rule text.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task EuUsAndCnAreCoveredWithCodingPlansAndTheirRuleText()
    {
        var all = await ListAsync(string.Empty).ConfigureAwait(true);

        foreach (var region in new[] { ProviderRegion.Europe, ProviderRegion.UnitedStates, ProviderRegion.China })
        {
            var rows = all.Providers.Where(provider => provider.Region == region).ToList();
            rows.Count(static provider => provider.Kind == ProviderKind.Api).ShouldBeGreaterThan(5, region + " has API providers");
            rows.Count(static provider => provider.Kind == ProviderKind.CodingPlan).ShouldBeGreaterThan(0, region + " has coding plans");
        }

        var plans = all.Providers.Where(static provider => provider.Plan is not null).ToList();
        plans.Count.ShouldBe(CatalogueRows.All.Count(static row => row.Plan is not null));
        plans.ShouldAllBe(static provider => !string.IsNullOrWhiteSpace(provider.Plan!.RuleText));
        all.Providers.Where(static provider => provider.Region == ProviderRegion.Europe).ShouldAllBe(static provider => !string.IsNullOrWhiteSpace(provider.Country));
    }

    /// <summary>The region filter keeps only that region; the kind filter keeps only that kind; both together narrow to the intersection.</summary>
    /// <param name="region">The region wire name.</param>
    /// <param name="kind">The kind wire name.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("europe", "")]
    [InlineData("united-states", "")]
    [InlineData("china", "")]
    [InlineData("china", "coding-plan")]
    [InlineData("europe", "coding-plan")]
    [InlineData("", "aggregator")]
    [InlineData("local", "local")]
    public async Task FiltersNarrowTheList(string region, string kind)
    {
        var all = await ListAsync(string.Empty).ConfigureAwait(true);
        var query = string.Join('&', new[] { region.Length > 0 ? "region=" + region : null, kind.Length > 0 ? "kind=" + kind : null }.Where(static part => part is not null));

        var filtered = await ListAsync(query).ConfigureAwait(true);

        filtered.Providers.Count.ShouldBeGreaterThan(0);
        filtered.Providers.Count.ShouldBeLessThan(all.Providers.Count);
        if (region.Length > 0)
        {
            filtered.Providers.ShouldAllBe(provider => provider.Region == ParseRegion(region));
        }

        if (kind.Length > 0)
        {
            filtered.Providers.ShouldAllBe(provider => provider.Kind == ParseKind(kind));
        }

        filtered.Total.ShouldBe(all.Total, "the total is the catalogue, not the filtered count");
    }

    /// <summary>The CN coding plans are exactly the plan rows of the CN region.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ChinaCodingPlansAreTheCatalogueRowsOfThatRegionAndKind()
    {
        var filtered = await ListAsync("region=china&kind=coding-plan").ConfigureAwait(true);

        filtered.Providers.Select(static provider => provider.Id).ShouldBe(
            CatalogueRows.All.Where(static row => row.Region == CatalogueRegion.Cn && row.Kind == CatalogueKind.CodingPlan).Select(static row => row.Id));
        filtered.Providers.ShouldAllBe(static provider => provider.Plan != null);
    }

    /// <summary>The search matches the id, the name, the country, the note and the route addresses, ignoring case, and the filters combine with it.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task SearchMatchesNamesAndAddressesAndCombinesWithFilters()
    {
        var byName = await ListAsync("search=KIMI").ConfigureAwait(true);
        var byHost = await ListAsync("search=api.mistral.ai").ConfigureAwait(true);
        var combined = await ListAsync("search=kimi&region=europe").ConfigureAwait(true);

        byName.Providers.ShouldContain(static provider => provider.Id == "kimi-code");
        byName.Providers.Count.ShouldBeLessThan(10);
        byHost.Providers.ShouldContain(static provider => provider.Id == "mistral");
        combined.Providers.ShouldAllBe(static provider => provider.Region == ProviderRegion.Europe);
        combined.Providers.Count.ShouldBeLessThan(byName.Providers.Count + 1);
        combined.Providers.ShouldNotContain(static provider => provider.Id == "kimi-code");
    }

    /// <summary>A region or kind that is not a wire name is the 400 problem of the real route.</summary>
    /// <param name="query">The query string.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("region=asia")]
    [InlineData("kind=cheap")]
    public async Task UnknownFiltersAreRefused(string query)
    {
        using var response = await _host.Http.GetAsync(new Uri("providers?" + query, UriKind.Relative), Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response).ConfigureAwait(true)).ShouldBe("invalid_filter");
    }

    /// <summary>One provider reads with its plan and routes; an unknown id is a 404 problem.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task OneProviderReadsAndAnUnknownOneIsNotFound()
    {
        var summary = await _host.Http.GetFromJsonAsync(new Uri("providers/kimi-code", UriKind.Relative), ProvidersJsonContext.Default.ProviderSummary, Token).ConfigureAwait(true);
        using var missing = await _host.Http.GetAsync(new Uri("providers/nope", UriKind.Relative), Token).ConfigureAwait(true);

        summary.ShouldNotBeNull().Region.ShouldBe(ProviderRegion.China);
        summary.Plan.ShouldNotBeNull().RuleText.ShouldNotBeNullOrWhiteSpace();
        summary.Routes.Count.ShouldBeGreaterThan(1);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(missing).ConfigureAwait(true)).ShouldBe("provider_not_found");
    }

    /// <summary>A key write is the 403 of a disabled operator switch, whatever the id or the body, and the key is never echoed back.</summary>
    /// <param name="id">The provider id.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("anthropic")]
    [InlineData("kimi-code")]
    [InlineData("does-not-exist")]
    public async Task KeyWritesAreRefusedAndNeverEchoed(string id)
    {
        using var response = await _host.Http.PutAsJsonAsync(
            new Uri($"providers/{id}/credential", UriKind.Relative),
            new SetCredentialRequest { ApiKey = Canary, PlanRuleAccepted = true },
            ProvidersJsonContext.Default.SetCredentialRequest,
            Token).ConfigureAwait(true);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(response).ConfigureAwait(true)).ShouldBe("credential_writes_disabled");
        var text = await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true);
        text.ShouldNotContain(Canary, Case.Sensitive);
        response.Headers.ToString().ShouldNotContain(Canary, Case.Sensitive);
    }

    /// <summary>A live test is skipped with the reason, for a catalogue provider and for a custom endpoint, and an unknown id is a 404.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task LiveTestsAreSkippedWithAReason()
    {
        _ = await AddCustomAsync(Valid()).ConfigureAwait(true);

        foreach (var id in new[] { "anthropic", "acme-gateway" })
        {
            using var response = await _host.Http.PostAsync(new Uri($"providers/{id}/test", UriKind.Relative), content: null, Token).ConfigureAwait(true);
            var body = await response.Content.ReadFromJsonAsync(ProvidersJsonContext.Default.ProviderTestResponse, Token).ConfigureAwait(true);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            body.ShouldNotBeNull().Status.ShouldBe(ProviderTestStatus.Skipped);
            body.ProviderId.ShouldBe(id);
            body.Reason.ShouldBe(Autonogrammer.Web.Demo.DemoProviders.LiveTestsDisabled);
        }

        using var missing = await _host.Http.PostAsync(new Uri("providers/nope/test", UriKind.Relative), content: null, Token).ConfigureAwait(true);
        missing.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>A custom endpoint is created with the variable an operator would set for its key, listed afterwards, and removed again.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task ACustomEndpointIsCreatedListedAndRemoved()
    {
        var created = await AddCustomAsync(Valid()).ConfigureAwait(true);
        var listed = await ListAsync(string.Empty).ConfigureAwait(true);
        using var removed = await _host.Http.DeleteAsync(new Uri("providers/custom/acme-gateway", UriKind.Relative), Token).ConfigureAwait(true);
        var after = await ListAsync(string.Empty).ConfigureAwait(true);
        using var again = await _host.Http.DeleteAsync(new Uri("providers/custom/acme-gateway", UriKind.Relative), Token).ConfigureAwait(true);

        created.KeyEnvName.ShouldBe("AUTONOGRAMMER_PROVIDER_ACME_GATEWAY_KEY");
        created.BaseAddress.ShouldBe("https://llm.acme.example/v1");
        created.ModelIds.ShouldBe(["acme-large", "acme-small"]);
        listed.Custom.Select(static custom => custom.Id).ShouldBe(["acme-gateway"]);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        after.Custom.ShouldBeEmpty();
        again.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>The address table of the real route: https only except on this machine, no link-local or metadata address, no user information, no query.</summary>
    /// <param name="address">The base URL the caller sent.</param>
    /// <param name="accepted">Whether the Demo accepts it.</param>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Theory]
    [InlineData("https://llm.acme.example/v1", true)]
    [InlineData("https://llm.acme.example/v1/", true)]
    [InlineData("http://localhost:11434/v1", true)]
    [InlineData("http://127.0.0.1:8080/v1", true)]
    [InlineData("http://llm.acme.example/v1", false)]
    [InlineData("ftp://llm.acme.example/v1", false)]
    [InlineData("https://169.254.169.254/latest", false)]
    [InlineData("https://metadata.google.internal/v1", false)]
    [InlineData("https://[fe80::1]/v1", false)]
    [InlineData("https://user:pass@llm.acme.example/v1", false)]
    [InlineData("https://llm.acme.example/v1?key=1", false)]
    [InlineData("https://llm.acme.example/v1#frag", false)]
    [InlineData("https://2130706433/v1", false)]
    [InlineData("https://intranet/v1", false)]
    [InlineData("", false)]
    public async Task CustomAddressesFollowTheTableOfTheRealRoute(string address, bool accepted)
    {
        using var response = await PostCustomAsync(Valid() with { BaseAddress = address }).ConfigureAwait(true);

        response.StatusCode.ShouldBe(accepted ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
        if (!accepted)
        {
            (await ProblemCodeAsync(response).ConfigureAwait(true)).ShouldBe("invalid_custom_provider");
        }
    }

    /// <summary>Ids, protocol and model ids are validated; a catalogue id, a block id and a repeated id are 409.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task CustomIdsProtocolAndModelsAreValidatedAndReservedIdsAreTaken()
    {
        foreach (var request in new[]
        {
            Valid() with { Id = "Upper Case" },
            Valid() with { Id = null },
            Valid() with { Protocol = null },
            Valid() with { ModelIds = [] },
            Valid() with { ModelIds = ["a", "a"] },
            Valid() with { ModelIds = ["bad model"] },
            Valid() with { DisplayName = new string('x', 81) },
        })
        {
            using var refused = await PostCustomAsync(request).ConfigureAwait(true);
            refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest, request.ToString());
        }

        foreach (var taken in new[] { "anthropic", "kimi-code", "kimi-code-global" })
        {
            using var response = await PostCustomAsync(Valid() with { Id = taken }).ConfigureAwait(true);
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict, taken);
            (await ProblemCodeAsync(response).ConfigureAwait(true)).ShouldBe("provider_id_taken");
        }

        _ = await AddCustomAsync(Valid()).ConfigureAwait(true);
        using var repeated = await PostCustomAsync(Valid()).ConfigureAwait(true);
        repeated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    /// <summary>A keyless custom endpoint has no key variable; the default model changes and shows in the next list; a model not written provider/model is a 400.</summary>
    /// <returns>A task that completes when the assertions ran.</returns>
    [Fact]
    public async Task KeylessEndpointsAndTheDefaultModel()
    {
        var keyless = await AddCustomAsync(Valid() with { KeyRequired = false }).ConfigureAwait(true);
        using var set = await _host.Http.PutAsJsonAsync(new Uri("models/default", UriKind.Relative), new SetDefaultModelRequest { Model = "acme-gateway/acme-large" }, ProvidersJsonContext.Default.SetDefaultModelRequest, Token).ConfigureAwait(true);
        var listed = await ListAsync(string.Empty).ConfigureAwait(true);

        keyless.KeyEnvName.ShouldBeNull();
        keyless.Configured.ShouldBe(ProviderConfiguredState.NotRequired);
        set.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await set.Content.ReadFromJsonAsync(ProvidersJsonContext.Default.DefaultModelResponse, Token).ConfigureAwait(true)).ShouldNotBeNull().Model.ShouldBe("acme-gateway/acme-large");
        listed.DefaultModel.ShouldBe("acme-gateway/acme-large");

        foreach (var bad in new[] { "no-slash", "/model", "provider/", "Bad Provider/model", "a b/c", "" })
        {
            using var refused = await _host.Http.PutAsJsonAsync(new Uri("models/default", UriKind.Relative), new SetDefaultModelRequest { Model = bad }, ProvidersJsonContext.Default.SetDefaultModelRequest, Token).ConfigureAwait(true);
            refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest, bad);
            (await ProblemCodeAsync(refused).ConfigureAwait(true)).ShouldBe("invalid_model");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    private static CustomProviderRequest Valid() => new()
    {
        Id = "acme-gateway",
        DisplayName = "Acme gateway",
        Protocol = CustomProviderProtocol.OpenAi,
        BaseAddress = "https://llm.acme.example/v1",
        ModelIds = ["acme-large", "acme-small"],
        KeyRequired = true,
    };

    private static ProviderRegion ParseRegion(string wire) => wire switch
    {
        "europe" => ProviderRegion.Europe,
        "united-states" => ProviderRegion.UnitedStates,
        "china" => ProviderRegion.China,
        "global" => ProviderRegion.Global,
        _ => ProviderRegion.Local,
    };

    private static ProviderKind ParseKind(string wire) => wire switch
    {
        "api" => ProviderKind.Api,
        "aggregator" => ProviderKind.Aggregator,
        "coding-plan" => ProviderKind.CodingPlan,
        _ => ProviderKind.Local,
    };

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Token).ConfigureAwait(true));
        var type = document.RootElement.GetProperty("type").GetString();
        return type?[(type.LastIndexOf('/') + 1)..];
    }

    private async Task<ProvidersResponse> ListAsync(string query)
    {
        var url = query.Length == 0 ? "providers" : "providers?" + query;
        return await _host.Http.GetFromJsonAsync(new Uri(url, UriKind.Relative), ProvidersJsonContext.Default.ProvidersResponse, Token).ConfigureAwait(true)
            ?? throw new InvalidOperationException("The list was empty.");
    }

    private Task<HttpResponseMessage> PostCustomAsync(CustomProviderRequest request) =>
        _host.Http.PostAsJsonAsync(new Uri("providers/custom", UriKind.Relative), request, ProvidersJsonContext.Default.CustomProviderRequest, Token);

    private async Task<CustomProviderInfo> AddCustomAsync(CustomProviderRequest request)
    {
        using var response = await PostCustomAsync(request).ConfigureAwait(true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadFromJsonAsync(ProvidersJsonContext.Default.CustomProviderInfo, Token).ConfigureAwait(true)
            ?? throw new InvalidOperationException("The answer was empty.");
    }
}
