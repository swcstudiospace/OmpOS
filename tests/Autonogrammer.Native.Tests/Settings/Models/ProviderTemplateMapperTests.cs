using Autonogrammer.Harness.Providers;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>The templates the Models page offers are the rows of the provider catalogue, with their source, date and plan quotas.</summary>
public sealed class ProviderTemplateMapperTests
{
    [Fact]
    public void EveryCatalogueRowBecomesATemplateInCatalogueOrderWithANameSourceAndDate()
    {
        var templates = ProviderTemplateMapper.FromCatalogue();

        templates.Select(template => template.Id).ShouldBe(ProviderCatalogue.All.Select(entry => entry.Id));
        foreach (var template in templates)
        {
            var entry = ProviderCatalogue.Find(template.Id)!;
            template.DisplayName.ShouldNotBe(template.Id, StringComparer.Ordinal, "every catalogue id has a name; add one to the mapper when the catalogue grows");
            template.SourceUrl.ShouldBe(entry.SourceUrl);
            template.SourceUrl.ShouldNotBeNull().IsAbsoluteUri.ShouldBeTrue();
            template.RetrievedOn.ShouldBe(entry.RetrievedOn);
            template.RetrievedOn.ShouldNotBeNull().ShouldBeGreaterThan(default);
            template.BaseEndpoint.ShouldBe(entry.BaseEndpoint);
            entry.AuthHeaderName.ShouldBeNull("a template cannot carry an authentication header name, because omp's models.yml cannot");
        }
    }

    [Fact]
    public void TheGroupFollowsTheKindOfTheRow()
    {
        var byId = ProviderTemplateMapper.FromCatalogue().ToDictionary(static template => template.Id, StringComparer.Ordinal);

        byId["ollama-local"].Group.ShouldBe(ProviderGroup.Local);
        byId["docker-model-runner-local"].Group.ShouldBe(ProviderGroup.Local);
        byId["ollama-cloud"].Group.ShouldBe(ProviderGroup.Cloud);
        byId["perplexity"].Group.ShouldBe(ProviderGroup.Cloud);
        byId["iflytek-astron-coding-openai"].Group.ShouldBe(ProviderGroup.CodingPlan);
        byId["minimax-m-plan-anthropic"].Group.ShouldBe(ProviderGroup.CodingPlan);
        byId.Values.Count(static template => template.Plan is not null).ShouldBe(ProviderCatalogue.CodingPlans.Sum(static plan => plan.ProviderIds.Count));
    }

    [Fact]
    public void ProtocolAuthenticationAndRoutesAreCarriedOver()
    {
        var byId = ProviderTemplateMapper.FromCatalogue().ToDictionary(static template => template.Id, StringComparer.Ordinal);

        byId["lm-studio-local"].Auth.ShouldBe(CustomProviderAuth.None);
        byId["lm-studio-local"].Capabilities.ShouldBe(CustomProviderCapabilities.Chat | CustomProviderCapabilities.Responses | CustomProviderCapabilities.Embeddings);
        byId["ollama-local"].Auth.ShouldBe(CustomProviderAuth.ApiKey);
        byId["ollama-cloud"].Auth.ShouldBe(CustomProviderAuth.Bearer);
        byId["deepseek-anthropic"].Protocol.ShouldBe(CustomProviderProtocol.AnthropicCompatible);
        byId["ollama-local"].Protocol.ShouldBe(CustomProviderProtocol.OpenAiCompatible);
        byId["perplexity"].Capabilities.ShouldBe(CustomProviderCapabilities.Responses);
    }

    [Fact]
    public void ACodingPlanRowCarriesItsTiersQuotasValidityAndPlanPage()
    {
        var plan = ProviderTemplateMapper.FromCatalogue().Single(static template => string.Equals(template.Id, "iflytek-astron-coding-openai", StringComparison.Ordinal)).Plan.ShouldNotBeNull();

        plan.ValidityDays.ShouldBe(31);
        plan.PlanUrl.ShouldBe(ProviderCatalogue.IFlytekAstronPlan.PlanUrl);
        plan.Tiers.Count.ShouldBe(2);
        plan.Tiers[0].QuotaText.ShouldBe(
            "about 6,000 requests per 5 hours; about 45,000 requests per week; about 90,000 requests per subscription month");
        plan.Tiers[1].QuotaText.ShouldBe("about 30,000 requests per subscription month");
    }

    [Fact]
    public void APlanThatPublishesNoLimitHasEmptyQuotaTextNotAnInventedOne()
    {
        var plan = ProviderTemplateMapper.FromCatalogue().Single(static template => string.Equals(template.Id, "minimax-m-plan-anthropic", StringComparison.Ordinal)).Plan.ShouldNotBeNull();

        plan.Tiers.Select(static tier => tier.Name).ShouldBe(["Go", "Explore", "Build"]);
        plan.Tiers.ShouldAllBe(static tier => tier.QuotaText.Length == 0);
        plan.ValidityDays.ShouldBeNull();
    }

    [Fact]
    public void NotesTellWhatTheUserNeedsToKnow()
    {
        var byId = ProviderTemplateMapper.FromCatalogue().ToDictionary(static template => template.Id, StringComparer.Ordinal);

        byId["ollama-local"].Note.ShouldBe("Start the server before you test the provider.");
        byId["minimax-m-plan-anthropic"].Note.ShouldNotBeNull().ShouldContain("own key", Case.Sensitive);
        byId["minimax-m-plan-anthropic"].Note.ShouldNotBeNull().ShouldContain("interactive coding tools", Case.Sensitive);
        byId["perplexity"].Note.ShouldBeNull();
    }
}
