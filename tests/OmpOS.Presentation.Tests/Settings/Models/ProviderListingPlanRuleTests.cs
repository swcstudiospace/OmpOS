using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Settings.Models;

/// <summary>The coding plan tick box ("I have read the vendor's rule text") and the operator's key-entry switch on a provider listing card.</summary>
public sealed class ProviderListingPlanRuleTests
{
    private const string Key = ModelsTestData.FakeKey;
    private const string Reason = "The operator has turned key entry off.";

    private static async Task<(ListingModelsSource Source, ProviderListingCardModel Card)> OpenAsync(string id, string? disabledReason = null)
    {
        var source = new ListingModelsSource(ListingTestData.All());
        var page = new ModelsSettingsPage(disabledReason is null ? source : new ReasonSource(source, disabledReason));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (source, page.Directory.Find(id).ShouldNotBeNull());
    }

    private static void Type(ProviderListingCardModel card) => card.ApiKey.ShouldNotBeNull().Entry = Key;

    /// <summary>A plan card asks for the tick, a plain provider does not, and the tick starts off every time the form opens.</summary>
    [Fact]
    public async Task OnlyAPlanCardAsksForTheTickAndItStartsOff()
    {
        var (_, plan) = await OpenAsync("zai").ConfigureAwait(true);
        var (_, plain) = await OpenAsync("mistral").ConfigureAwait(true);

        plan.RequiresPlanRule.ShouldBeTrue();
        plain.RequiresPlanRule.ShouldBeFalse();
        plan.PlanRuleAcceptLabel.ShouldBe(ProviderListingCopy.PlanRuleAcceptLabel, Case.Sensitive);
        plan.PlanRuleAcceptName.ShouldContain(plan.DisplayName, Case.Sensitive);

        plan.BeginSetup();
        plan.PlanRuleAccepted = true;
        plan.CancelSetup();
        plan.BeginSetup();
        plan.PlanRuleAccepted.ShouldBeFalse();
    }

    /// <summary>Without the tick nothing is sent and the key is not kept; with it the source gets the key once, and the request carries the tick.</summary>
    [Fact]
    public async Task AddingAPlanIsRefusedWithoutTheTickAndSendsItWhenTicked()
    {
        var ct = TestContext.Current.CancellationToken;
        var (source, card) = await OpenAsync("zai").ConfigureAwait(true);
        card.BeginSetup();
        Type(card);

        (await card.SaveAsync(ct).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe(ProviderListingCopy.PlanRuleRequired, Case.Sensitive);
        source.OmpKeys.ShouldBeEmpty();
        source.CatalogueAdds.ShouldBeEmpty();

        card.PlanRuleAccepted = true;
        card.ErrorText.ShouldBeNull();
        (await card.SaveAsync(ct).ConfigureAwait(true)).ShouldBeTrue();

        (source.OmpKeys.Count + source.CatalogueAdds.Count).ShouldBe(1);
        source.CatalogueAdds.ShouldAllBe(add => add.Request.PlanRuleAccepted);
    }

    /// <summary>While the operator has turned key entry off the card says why, is disabled, refuses the save before any source call and keeps no key.</summary>
    [Fact]
    public async Task AnOperatorSwitchDisablesTheKeyFieldAndRefusesTheSave()
    {
        var ct = TestContext.Current.CancellationToken;
        var (source, card) = await OpenAsync("mistral", Reason).ConfigureAwait(true);

        card.IsKeyDisabled.ShouldBeTrue();
        card.IsKeyEnabled.ShouldBeFalse();
        card.KeyDisabledReason.ShouldBe(Reason, Case.Sensitive);

        card.BeginSetup();
        Type(card);
        (await card.SaveAsync(ct).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe(Reason, Case.Sensitive);
        card.ApiKey.ShouldNotBeNull().Entry.ShouldBeEmpty();
        source.OmpKeys.ShouldBeEmpty();
        source.CatalogueAdds.ShouldBeEmpty();
    }

    /// <summary>With no switch the key field is enabled and no reason shows.</summary>
    [Fact]
    public async Task WithoutTheSwitchTheKeyFieldIsEnabled()
    {
        var (_, card) = await OpenAsync("mistral").ConfigureAwait(true);

        card.IsKeyDisabled.ShouldBeFalse();
        card.KeyDisabledReason.ShouldBeNull();
    }

    private sealed class ReasonSource(ListingModelsSource inner, string reason) : IModelsSettingsSource
    {
        public async Task<ModelsSnapshot> LoadAsync(CancellationToken cancellationToken) =>
            (await inner.LoadAsync(cancellationToken).ConfigureAwait(true)) with { KeyWritesDisabledReason = reason };

        public Task<ModelsActionResult> SaveApiKeyAsync(string providerId, string apiKey, CancellationToken cancellationToken) =>
            inner.SaveApiKeyAsync(providerId, apiKey, cancellationToken);

        public Task<ModelsActionResult> SetDefaultModelAsync(string selector, CancellationToken cancellationToken) =>
            inner.SetDefaultModelAsync(selector, cancellationToken);

        public Task<ModelsActionResult> SaveCustomProviderAsync(CustomProviderForm form, CancellationToken cancellationToken) =>
            inner.SaveCustomProviderAsync(form, cancellationToken);

        public Task<ModelsActionResult> RemoveCustomProviderAsync(string providerId, CancellationToken cancellationToken) =>
            inner.RemoveCustomProviderAsync(providerId, cancellationToken);

        public Task<ProviderTestOutcome> TestAsync(string providerId, CancellationToken cancellationToken) =>
            inner.TestAsync(providerId, cancellationToken);

        public Task<ModelsActionResult> AddCatalogueProviderAsync(CatalogueProviderRequest request, string apiKey, CancellationToken cancellationToken) =>
            inner.AddCatalogueProviderAsync(request, apiKey, cancellationToken);
    }
}
