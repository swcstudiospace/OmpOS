using System.Text.Json;
using OmpOS.Contracts.Providers;
using Shouldly;
using Xunit;

namespace OmpOS.Contracts.Tests.Providers;

/// <summary>Verifies the provider shapes: wire names of the enums, optional members omitted, and a lossless round trip.</summary>
public sealed class ProvidersContractJsonTests
{
    private static ProviderSummary Summary() => new()
    {
        Id = "moonshot-cn",
        DisplayName = "Moonshot (CN)",
        Region = ProviderRegion.China,
        Kind = ProviderKind.CodingPlan,
        Status = ProviderVerification.Partial,
        StatusDate = new DateOnly(2026, 10, 27),
        TakesKey = true,
        SignInOnly = false,
        Routes =
        [
            new ProviderRouteInfo
            {
                Id = "openai",
                Api = "openai-completions",
                BaseAddressTemplate = "https://api.moonshot.cn/v1",
                Auth = "Bearer",
                TakesKey = true,
                DiscoversModels = true,
                NeedsUserModels = false,
                DocumentedModels = [],
            },
        ],
        UrlVariables = [new ProviderUrlVariableInfo { Name = "host", Label = "Host" }],
        SourceUrl = new Uri("https://platform.moonshot.cn/docs", UriKind.Absolute),
        Plan = new ProviderPlanInfo
        {
            RuleText = "Interactive use only.",
            InteractiveOnly = true,
            SeparateKey = true,
            PlanUrl = new Uri("https://example.com/plan", UriKind.Absolute),
            Tiers = [new ProviderPlanTierInfo { Name = "Pro", PriceText = "price unverified", PriceVerified = false }],
            ListedTools = ["omp"],
        },
        Configured = ProviderConfiguredState.NotConfigured,
    };

    [Theory]
    [InlineData(ProviderRegion.UnitedStates, "united-states")]
    [InlineData(ProviderRegion.Europe, "europe")]
    public void TheRegionHasItsWireName(ProviderRegion region, string wire)
    {
        JsonSerializer.Serialize(region, ProvidersJsonContext.Default.ProviderRegion).ShouldBe($"\"{wire}\"");
    }

    [Theory]
    [InlineData(ProviderConfiguredState.NotConfigured, "not-configured")]
    [InlineData(ProviderConfiguredState.NotRequired, "not-required")]
    public void TheConfiguredStateHasItsWireName(ProviderConfiguredState state, string wire)
    {
        JsonSerializer.Serialize(state, ProvidersJsonContext.Default.ProviderConfiguredState).ShouldBe($"\"{wire}\"");
    }

    [Fact]
    public void ASummaryRoundTripsAndOmitsWhatIsAbsent()
    {
        var json = JsonSerializer.Serialize(Summary(), ProvidersJsonContext.Default.ProviderSummary);

        json.ShouldContain("\"kind\":\"coding-plan\"", Case.Sensitive);
        json.ShouldContain("\"statusDate\":\"2026-10-27\"", Case.Sensitive);
        json.ShouldNotContain("\"country\"", Case.Sensitive);
        json.ShouldNotContain("\"note\"", Case.Sensitive);
        var back = JsonSerializer.Deserialize(json, ProvidersJsonContext.Default.ProviderSummary).ShouldNotBeNull();
        back.Id.ShouldBe("moonshot-cn");
        back.Plan.ShouldNotBeNull().Tiers.Single().PriceVerified.ShouldBeFalse();
        back.Routes.Single().DocumentedModels.ShouldBeEmpty();
        back.Configured.ShouldBe(ProviderConfiguredState.NotConfigured);
    }

    [Fact]
    public void TheListResponseRoundTripsWithTheOperatorSwitches()
    {
        var response = new ProvidersResponse
        {
            Providers = [Summary()],
            Total = 103,
            Custom = [new CustomProviderInfo { Id = "mine", DisplayName = "Mine", Protocol = CustomProviderProtocol.Anthropic, BaseAddress = "https://llm.example.com/v1", ModelIds = ["m1"], KeyEnvName = "OMPOS_PROVIDER_MINE_KEY", Configured = ProviderConfiguredState.Unknown }],
            OmpInstalled = false,
            CredentialWritesEnabled = false,
            ConfigWritesEnabled = true,
            LiveTestsEnabled = false,
        };

        var json = JsonSerializer.Serialize(response, ProvidersJsonContext.Default.ProvidersResponse);
        var back = JsonSerializer.Deserialize(json, ProvidersJsonContext.Default.ProvidersResponse).ShouldNotBeNull();

        json.ShouldContain("\"protocol\":\"anthropic\"", Case.Sensitive);
        back.Total.ShouldBe(103);
        back.CredentialWritesEnabled.ShouldBeFalse();
        back.ConfigWritesEnabled.ShouldBeTrue();
        back.Custom.Single().KeyEnvName.ShouldBe("OMPOS_PROVIDER_MINE_KEY");
        back.DefaultModel.ShouldBeNull();
    }

    [Fact]
    public void RequestsReadTheirWireNamesAndRefuseAnUnknownProtocol()
    {
        var custom = JsonSerializer.Deserialize("""{"id":"mine","protocol":"openai","baseUrl":"https://llm.example.com/v1","modelIds":["a","b"],"keyRequired":false}""", ProvidersJsonContext.Default.CustomProviderRequest).ShouldNotBeNull();
        custom.Protocol.ShouldBe(CustomProviderProtocol.OpenAi);
        custom.ModelIds.ShouldBe(["a", "b"]);
        custom.KeyRequired.ShouldBe(false);
        JsonSerializer.Deserialize("""{"apiKey":"k","planRuleAccepted":true}""", ProvidersJsonContext.Default.SetCredentialRequest).ShouldNotBeNull().PlanRuleAccepted.ShouldBeTrue();
        JsonSerializer.Deserialize("""{"apiKey":"k"}""", ProvidersJsonContext.Default.SetCredentialRequest).ShouldNotBeNull().PlanRuleAccepted.ShouldBeFalse();
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize("""{"protocol":"grpc"}""", ProvidersJsonContext.Default.CustomProviderRequest));
    }

    [Fact]
    public void ACredentialResponseHasNoKeyMember()
    {
        var json = JsonSerializer.Serialize(new CredentialResponse { ProviderId = "deepseek", Configured = ProviderConfiguredState.Configured }, ProvidersJsonContext.Default.CredentialResponse);

        json.ShouldBe("""{"providerId":"deepseek","configured":"configured"}""");
    }

    [Fact]
    public void AProviderTestResponseCarriesItsReasonOnlyWhenThereIsOne()
    {
        JsonSerializer.Serialize(new ProviderTestResponse { ProviderId = "x", Status = ProviderTestStatus.Reachable }, ProvidersJsonContext.Default.ProviderTestResponse)
            .ShouldBe("""{"providerId":"x","status":"reachable"}""");
        JsonSerializer.Serialize(new ProviderTestResponse { ProviderId = "x", Status = ProviderTestStatus.Skipped, Reason = "off" }, ProvidersJsonContext.Default.ProviderTestResponse)
            .ShouldContain("\"reason\":\"off\"", Case.Sensitive);
    }
}
