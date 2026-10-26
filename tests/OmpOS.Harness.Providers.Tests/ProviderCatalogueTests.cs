using Shouldly;
using Xunit;

namespace OmpOS.Harness.Providers.Tests;

/// <summary>
/// Checks the built-in catalogue as a whole. A row is admitted only when a verified research row backs it, so these tests
/// look at every row for its citation, its validity and its identity, and then pin each row that research verified.
/// </summary>
public sealed class ProviderCatalogueTests
{
    private static readonly DateOnly ResearchDate = new(2026, 10, 22);

    [Fact]
    public void EveryCatalogueEntryCarriesSourceAndDate()
    {
        foreach (var entry in ProviderCatalogue.All)
        {
            var source = entry.SourceUrl.ShouldNotBeNull(entry.Id);
            source.IsAbsoluteUri.ShouldBeTrue(entry.Id);
            entry.RetrievedOn.ShouldBe(ResearchDate, entry.Id);
        }

        foreach (var pattern in new[] { ProviderCatalogue.GenericOpenAiCompatible, ProviderCatalogue.GenericAnthropicCompatible })
        {
            pattern.SourceUrl.IsAbsoluteUri.ShouldBeTrue(pattern.Id);
            pattern.RetrievedOn.ShouldBe(ResearchDate, pattern.Id);
        }
    }

    [Fact]
    public void EveryCatalogueEntryPassesTheValidator()
    {
        foreach (var entry in ProviderCatalogue.All)
        {
            ProviderEntryValidator.Validate(entry).Errors.ShouldBeEmpty(entry.Id);
        }
    }

    [Fact]
    public void CatalogueIdsAreUniqueAndFindable()
    {
        ProviderCatalogue.All.Select(entry => entry.Id).Distinct(StringComparer.Ordinal).Count().ShouldBe(ProviderCatalogue.All.Count);
        foreach (var entry in ProviderCatalogue.All)
        {
            ProviderCatalogue.Find(entry.Id).ShouldBeSameAs(entry);
        }

        ProviderCatalogue.Find("no-such-provider").ShouldBeNull();
    }

    [Fact]
    public void EveryLocalEntryListensOnLoopback()
    {
        var local = ProviderCatalogue.All.Where(entry => entry.Kind == ProviderKind.Local).ToList();

        local.Count.ShouldBe(7);
        foreach (var entry in local)
        {
            entry.AllowRemotePlainHttp.ShouldBeFalse(entry.Id);
            ProviderEndpointValidator.Validate(entry.BaseEndpoint, allowRemotePlainHttp: false).IsValid.ShouldBeTrue(entry.Id);
        }
    }

    [Theory]
    [InlineData("ollama-local", "http://localhost:11434/v1/")]
    [InlineData("lm-studio-local", "http://localhost:1234/v1")]
    [InlineData("llama-cpp-local", "http://127.0.0.1:9931")]
    [InlineData("vllm-local", "http://localhost:8000/v1")]
    [InlineData("localai-local", "http://localhost:8080/v1")]
    [InlineData("jan-local", "http://127.0.0.1:1337/v1")]
    [InlineData("docker-model-runner-local", "http://localhost:12434/engines/v1")]
    public void LocalRuntimeBasesMatchTheirResearchRows(string id, string expectedBase)
    {
        // providers-local-us.md:15-21 (Ollama, LM Studio, llama.cpp, vLLM, LocalAI, Jan, Docker Model Runner), all verified.
        var entry = ProviderCatalogue.Find(id).ShouldNotBeNull();

        entry.Kind.ShouldBe(ProviderKind.Local);
        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.BaseEndpoint.ShouldBe(new Uri(expectedBase));
    }

    [Fact]
    public void LmStudioRowRecordsItsThreeDocumentedRoutes()
    {
        // providers-local-us.md:16: /v1/chat/completions, /v1/responses, /v1/embeddings; auth none by default.
        var entry = ProviderCatalogue.LmStudioLocalOpenAi;

        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Responses | ProviderCapabilities.Embeddings);
        entry.AuthType.ShouldBe(ProviderAuthType.None);
        entry.SourceUrl.ShouldBe(new Uri("https://lmstudio.ai/docs/developer/openai-compat"));
    }

    [Fact]
    public void DockerModelRunnerRowRecordsNoRouteTheResearchDoesNotName()
    {
        // providers-local-us.md:21: /engines/v1/chat/completions, embeddings, models. No Responses route is listed.
        var entry = ProviderCatalogue.DockerModelRunnerLocalOpenAi;

        entry.Capabilities.ShouldBe(ProviderCapabilities.ChatCompletions | ProviderCapabilities.Embeddings);
        entry.Capabilities.HasFlag(ProviderCapabilities.Responses).ShouldBeFalse();
    }

    [Fact]
    public void GenericPatternsRecordTheirSchemeAndSource()
    {
        // providers-local-us.md:23-24: user-supplied /v1 root with a Bearer key; Anthropic root with ANTHROPIC_AUTH_TOKEN as Bearer.
        ProviderCatalogue.GenericOpenAiCompatible.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        ProviderCatalogue.GenericOpenAiCompatible.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        ProviderCatalogue.GenericOpenAiCompatible.SourceUrl.ShouldBe(new Uri("https://inference-docs.cerebras.ai/resources/openai"));
        ProviderCatalogue.GenericAnthropicCompatible.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        ProviderCatalogue.GenericAnthropicCompatible.AuthType.ShouldBe(ProviderAuthType.BearerToken);
    }

    [Fact]
    public void DeepSeekEntryMatchesItsVerifiedRow()
    {
        // providers-eu-cn.md:22 (CN-1): Anthropic base https://api.deepseek.com/anthropic; API key, Bearer; pay-as-you-go only.
        var entry = ProviderCatalogue.Find("deepseek-anthropic").ShouldNotBeNull();

        entry.Kind.ShouldBe(ProviderKind.ApiKey);
        entry.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://api.deepseek.com/anthropic"));
        ProviderCatalogue.FindCodingPlan(entry.Id).ShouldBeNull();
    }

    [Fact]
    public void HuggingFaceEntryMatchesItsVerifiedRow()
    {
        // providers-eu-cn.md:19 (EU-7): https://router.huggingface.co/v1; HF token, Bearer; no coding plan.
        var entry = ProviderCatalogue.Find("huggingface-router").ShouldNotBeNull();

        entry.Kind.ShouldBe(ProviderKind.ApiKey);
        entry.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        entry.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        entry.BaseEndpoint.ShouldBe(new Uri("https://router.huggingface.co/v1"));
        ProviderCatalogue.FindCodingPlan(entry.Id).ShouldBeNull();
    }

    [Fact]
    public void MiniMaxEntryMatchesItsVerifiedRowAndPlan()
    {
        // providers-eu-cn.md:26 (CN-5): https://api.minimax.io/anthropic; M Plan key; tiers Go, Explore, Build; caps unpublished.
        var entry = ProviderCatalogue.Find("minimax-m-plan-anthropic").ShouldNotBeNull();
        var plan = ProviderCatalogue.FindCodingPlan(entry.Id).ShouldNotBeNull();

        entry.Kind.ShouldBe(ProviderKind.CodingPlan);
        entry.BaseEndpoint.ShouldBe(new Uri("https://api.minimax.io/anthropic"));
        plan.SeparateKey.ShouldBeTrue();
        plan.Tiers.Select(tier => tier.Name).ShouldBe(["Go", "Explore", "Build"]);
        plan.Tiers.ShouldAllBe(tier => tier.Quotas.Count == 0);
        plan.ValidityDays.ShouldBeNull();
        plan.ListedTools.ShouldContain("Claude Code", StringComparer.Ordinal);
    }

    [Fact]
    public void IFlytekEntriesMatchTheirVerifiedRowAndPlan()
    {
        // providers-eu-cn.md:30 (CN-9) and the primary page https://www.xfyun.cn/doc/spark/CodingPlan.html sections 1.2, 4.1.
        var openAi = ProviderCatalogue.Find("iflytek-astron-coding-openai").ShouldNotBeNull();
        var anthropic = ProviderCatalogue.Find("iflytek-astron-coding-anthropic").ShouldNotBeNull();
        var plan = ProviderCatalogue.FindCodingPlan(openAi.Id).ShouldNotBeNull();

        openAi.Kind.ShouldBe(ProviderKind.CodingPlan);
        openAi.Transport.ShouldBe(ProviderTransport.OpenAiCompatible);
        openAi.BaseEndpoint.ShouldBe(new Uri("https://maas-coding-api.cn-huabei-1.xf-yun.com/v2"));
        anthropic.Transport.ShouldBe(ProviderTransport.AnthropicCompatible);
        anthropic.AuthType.ShouldBe(ProviderAuthType.BearerToken);
        anthropic.BaseEndpoint.ShouldBe(new Uri("https://maas-coding-api.cn-huabei-1.xf-yun.com/anthropic"));
        ProviderCatalogue.FindCodingPlan(anthropic.Id).ShouldBeSameAs(plan);
        plan.ValidityDays.ShouldBe(31);
        plan.SeparateKey.ShouldBeTrue();
        plan.InteractiveToolsOnly.ShouldBeTrue();

        var efficient = plan.Tiers.Single(tier => string.Equals(tier.Name, "高效版", StringComparison.Ordinal));
        efficient.Quotas.ShouldBe(
        [
            new CodingPlanQuota(CodingPlanQuotaWindow.FiveHours, 6_000),
            new CodingPlanQuota(CodingPlanQuotaWindow.Week, 45_000),
            new CodingPlanQuota(CodingPlanQuotaWindow.SubscriptionMonth, 90_000),
        ]);
        plan.Tiers.Single(tier => string.Equals(tier.Name, "速通版", StringComparison.Ordinal)).Quotas.ShouldBe([new CodingPlanQuota(CodingPlanQuotaWindow.SubscriptionMonth, 30_000)]);
    }

    [Fact]
    public void EveryCodingPlanNamesOnlyCatalogueEntriesOfKindCodingPlan()
    {
        ProviderCatalogue.CodingPlans.Count.ShouldBe(2);
        foreach (var plan in ProviderCatalogue.CodingPlans)
        {
            plan.ProviderIds.ShouldNotBeEmpty(plan.Id);
            foreach (var id in plan.ProviderIds)
            {
                ProviderCatalogue.Find(id).ShouldNotBeNull(id).Kind.ShouldBe(ProviderKind.CodingPlan, id);
            }
        }

        ProviderCatalogue.All
            .Where(entry => entry.Kind == ProviderKind.CodingPlan)
            .ShouldAllBe(entry => ProviderCatalogue.FindCodingPlan(entry.Id) != null);
    }
}
