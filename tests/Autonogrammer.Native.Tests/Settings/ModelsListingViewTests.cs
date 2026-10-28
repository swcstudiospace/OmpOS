using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The provider listing of the Models page: sections by region and kind, region and kind filters, the verify mark, the plan terms, and the setup form with its masked key.</summary>
public sealed class ModelsListingViewTests
{
    private static readonly string[] ExpectedHeadings = ["Europe: Model APIs", "United States and Canada: Coding plans", "China: Model APIs", "China: Coding plans"];

    private static void WithListings(SettingsFixture fixture) =>
        fixture.ModelsSource.Snapshot = ModelsData.Snapshot() with { Listings = ModelsListingData.All() };

    private static ProviderListingCardView Listing(ModelsPageView view, string id) =>
        view.ListingCardViews.FirstOrDefault(card => string.Equals(card.Card?.Id, id, StringComparison.Ordinal)).ShouldNotBeNull($"no listing card is drawn for {id}");

    /// <summary>Without listings the block is not drawn; with them it appears under the provider cards.</summary>
    [Fact]
    public async Task TheListingBlockIsDrawnOnlyWhenTheSourceOffersListings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using (var plain = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true))
            {
                var view = (ModelsPageView)plain.Show(SettingsSection.Models);
                view.Listing.IsEffectivelyVisible.ShouldBeFalse();
                view.ListingCardViews.ShouldBeEmpty();
            }

            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var listed = (ModelsPageView)fixture.Show(SettingsSection.Models);
            listed.Listing.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(listed).ShouldContain(ProviderListingCopy.SectionTitle, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The cards are drawn section by section under their region and kind headings.</summary>
    [Fact]
    public async Task ListingCardsAreDrawnSectionBySectionUnderTheirHeadings()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            view.ListingCardViews.Select(static card => card.Card!.Id).ShouldBe(["mistral", "infomaniak-ai-tools", "anthropic-subscription", "baichuan", "zai"]);
            var texts = SettingsTree.Texts(view);
            foreach (var heading in ExpectedHeadings)
            {
                texts.ShouldContain(heading, StringComparer.Ordinal);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An unverified listing carries the verify mark, a verified one does not; a plan shows the vendor rule, its tags and the cloud sentence.</summary>
    [Fact]
    public async Task TheVerifyMarkAndThePlanTermsAreShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            Listing(view, "baichuan").Verify.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(Listing(view, "baichuan")).ShouldContain(ProviderListingCopy.VerifyBeforeUse, StringComparer.Ordinal);
            Listing(view, "mistral").Verify.IsEffectivelyVisible.ShouldBeFalse();

            var plan = Listing(view, "zai");
            plan.PlanRule.Text.ShouldBe("Vendor rule: Only in officially supported tools; bulk or automated usage and resale are banned.");
            var texts = SettingsTree.Texts(plan);
            texts.ShouldContain("Interactive use only", StringComparer.Ordinal);
            texts.ShouldContain(ProviderListingCopy.CloudAgentsSentence, StringComparer.Ordinal);
            texts.ShouldContain("Lite: $18 per month; 2,000 credits per 5 hours", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The region and kind filters narrow the drawn cards and carry accessible names.</summary>
    [Fact]
    public async Task TheRegionAndKindFiltersNarrowTheCards()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            AutomationProperties.GetName(view.RegionChoice).ShouldBe(ProviderListingCopy.RegionFilterLabel);
            AutomationProperties.GetName(view.KindChoice).ShouldBe(ProviderListingCopy.KindFilterLabel);

            view.RegionChoice.SelectedIndex = 3;
            fixture.Settle();
            view.ListingCardViews.Select(static card => card.Card!.Id).ShouldBe(["baichuan", "zai"]);

            view.KindChoice.SelectedIndex = 3;
            fixture.Settle();
            view.ListingCardViews.Select(static card => card.Card!.Id).ShouldBe(["zai"]);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A sign-in listing has no Set up action and no key field.</summary>
    [Fact]
    public async Task ASignInListingOffersNoSetupAndNoKeyField()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            var card = Listing(view, "anthropic-subscription");
            card.Setup.IsEffectivelyVisible.ShouldBeFalse();
            SettingsTree.Visible<TextBox>(card).ShouldBeEmpty();
            SettingsTree.Texts(card).ShouldContain(ProviderListingCopy.SignInText("/login anthropic"), StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Set up opens a form with a masked key entry; saving hands the key to omp once, empties the entry and leaves the key in no control or name.</summary>
    [Fact]
    public async Task SettingUpAnOmpNativeProviderHandsTheKeyToOmpAndLeavesNoTrace()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var card = Listing(view, "mistral");
            AutomationProperties.GetName(card.Setup).ShouldBe(ProviderListingCopy.SetupActionName("Mistral AI La Plateforme"));
            SettingsTree.Click(fixture.Window, card.Setup);
            card = Listing(view, "mistral");

            var key = SettingsTree.Visible<TextBox>(card).Single(static box => box.PasswordChar != '\0');
            key.PasswordChar.ShouldBe('\u2022');
            key.RevealPassword.ShouldBeFalse();
            AutomationProperties.GetName(key).ShouldBe(ModelsCopy.ApiKeyFieldLabel("Mistral AI La Plateforme"));
            key.Text = ModelsData.FakeKey;
            fixture.Settle();
            SettingsTree.Click(fixture.Window, card.Add);
            await Task.Yield();
            fixture.Settle();

            fixture.ModelsSource.Keys.ShouldBe([("mistral", ModelsData.FakeKey)]);
            SettingsTree.Everything(fixture.Window).Where(static text => text.Contains(ModelsData.FakeKey, StringComparison.Ordinal)).ShouldBeEmpty();
            foreach (var box in SettingsTree.Visible<TextBox>(fixture.Window))
            {
                (box.Text ?? string.Empty).ShouldNotContain(ModelsData.FakeKey);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A head without catalogue support says so on the card, empties the key and shows the key nowhere; every control of an open form is named.</summary>
    [Fact]
    public async Task AHeadWithoutCatalogueSupportSaysSoAndEveryControlOfTheOpenFormIsNamed()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, WithListings).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var card = Listing(view, "infomaniak-ai-tools");
            SettingsTree.Click(fixture.Window, card.Setup);
            card = Listing(view, "infomaniak-ai-tools");
            SettingsTree.Unnamed(view).ShouldBeEmpty();

            var inputs = SettingsTree.Visible<TextBox>(card);
            inputs.Count(static box => box.PasswordChar != '\0').ShouldBe(1);
            var product = inputs.First(static box => box.PasswordChar == '\0' && !string.Equals(box.Name, "ModelIdsBox", StringComparison.Ordinal));
            AutomationProperties.GetName(product).ShouldBe(ProviderListingCopy.VariableFieldName("Infomaniak AI Tools", "Product ID"));
            product.Text = "424242";
            inputs.Single(static box => box.PasswordChar != '\0').Text = ModelsData.FakeKey;
            fixture.Settle();
            SettingsTree.Click(fixture.Window, card.Add);
            await Task.Yield();
            fixture.Settle();

            card.Error.IsEffectivelyVisible.ShouldBeTrue();
            card.Error.Text.ShouldBe(ProviderListingCopy.NotSupportedText);
            SettingsTree.Everything(fixture.Window).Where(static text => text.Contains(ModelsData.FakeKey, StringComparison.Ordinal)).ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }
}
