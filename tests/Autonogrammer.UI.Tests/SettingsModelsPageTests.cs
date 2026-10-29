using Autonogrammer.Presentation.Settings.Models;
using Autonogrammer.UI.Components.Settings;
using Bunit;
using Microsoft.AspNetCore.Components;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The Models and providers page: provider cards, the write-only API key field, the provider directory with its filters and plan cards, URL placeholders, and the custom provider form (PX-09).</summary>
public sealed class SettingsModelsPageTests : UiTestContext
{
    private const string Key = "sk-test-9f8e7d6c5b4a3210";

    /// <summary>Configured providers are grouped, each card shows its status as words only (never a key), lists its models with a named Use as default button, and offers a named key button.</summary>
    [Fact]
    public async Task ProviderCardsShowStatusModelsAndActionsWithoutAnyKey()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Providers.Add(SettingsFixtures.CloudProvider("openai", "OpenAI"));
        source.Providers.Add(SettingsFixtures.CloudProvider("anthropic", "Anthropic"));

        var cut = await RenderPageAsync(source);

        cut.Find("[role='group'][aria-label='" + ModelsCopy.GroupTitle(ProviderGroup.Cloud) + "']").ShouldNotBeNull();
        var card = cut.Find("[data-provider='openai']");
        card.QuerySelector("h5")!.TextContent.ShouldBe("OpenAI");
        card.QuerySelector("[data-credential]")!.TextContent.ShouldBe(ModelsCopy.StatusLine(ProviderAuthKind.ApiKey, CredentialState.Absent));
        card.QuerySelectorAll(".autonogrammer-provider-model .autonogrammer-settings-mono").Select(static model => model.TextContent).ShouldBe(["openai/model-1", "openai/model-2"]);
        card.QuerySelector("button[aria-label='Use openai/model-1 as default']")!.TextContent.ShouldBe(ModelsCopy.UseAsDefaultLabel);
        card.QuerySelector("button[aria-label='Add API key, OpenAI']")!.TextContent.ShouldBe(ModelsCopy.AddApiKeyLabel);
        card.QuerySelector("button[aria-label='Test connection, OpenAI']").ShouldNotBeNull();
        cut.FindAll("input[type='password']").Count.ShouldBe(0, "no key field is drawn until the user asks for one");
    }

    /// <summary>The key field is write-only: a password input with autocomplete off and no value, named by its label; what is typed never reaches the markup; saving hands the key to the source once and the card then shows only its status.</summary>
    [Fact]
    public async Task KeyFieldIsWriteOnlyAndTheSavedKeyIsNeverShownAgain()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Providers.Add(SettingsFixtures.CloudProvider("openai", "OpenAI"));
        var cut = await RenderPageAsync(source);

        await cut.Find("button[aria-label='Add API key, OpenAI']").ClickAsync().ConfigureAwait(true);

        var input = cut.Find("input[type='password']");
        input.GetAttribute("autocomplete").ShouldBe("off");
        input.HasAttribute("value").ShouldBeFalse("the field is never prefilled");
        cut.Find("label[for='" + input.Id + "']").TextContent.ShouldBe(ModelsCopy.ApiKeyFieldLabel("OpenAI"));
        input.GetAttribute("aria-describedby")!.Split(' ').ShouldAllBe(id => cut.FindAll("#" + id).Count == 1);

        await input.InputAsync(new ChangeEventArgs { Value = Key }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-provider='openai'] button.autonogrammer-btn--primary").HasAttribute("disabled").ShouldBeFalse()).ConfigureAwait(true);
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
        cut.Find("input[type='password']").HasAttribute("value").ShouldBeFalse("the typed text must not be echoed into the markup");

        await cut.Find("[data-provider='openai'] button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll("input[type='password']").Count.ShouldBe(0)).ConfigureAwait(true);
        source.Keys.ShouldBe([("openai", Key)]);
        cut.Find("[data-provider='openai'] [data-credential]").TextContent.ShouldBe(ModelsCopy.StatusLine(ProviderAuthKind.ApiKey, CredentialState.Present));
        cut.Find("[data-provider='openai'] button[aria-label='Replace API key, OpenAI']").ShouldNotBeNull();
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
        cut.Find("[data-provider='openai'] .autonogrammer-provider-result").TextContent.ShouldBe(ModelsCopy.KeySaved("OpenAI"));
    }

    /// <summary>A refused key leaves the editor open with a fresh empty input; the error never quotes the key even when the source's message did, and the old input element is gone.</summary>
    [Fact]
    public async Task RefusedKeyIsNeverEchoedAndTheInputIsReplacedByAnEmptyOne()
    {
        var source = new SettingsFixtures.ModelsSource { KeyFailure = "The provider rejected {key}" };
        source.Providers.Add(SettingsFixtures.CloudProvider("openai", "OpenAI"));
        var cut = await RenderPageAsync(source);
        await cut.Find("button[aria-label='Add API key, OpenAI']").ClickAsync().ConfigureAwait(true);
        var typedInto = cut.Find("input[type='password']");
        await typedInto.InputAsync(new ChangeEventArgs { Value = Key }).ConfigureAwait(true);
        await cut.WaitForAssertionAsync(() => cut.Find("[data-provider='openai'] button.autonogrammer-btn--primary").HasAttribute("disabled").ShouldBeFalse()).ConfigureAwait(true);

        await cut.Find("[data-provider='openai'] button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-provider='openai'] [role='alert']").TextContent.ShouldStartWith("The provider rejected")).ConfigureAwait(true);
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
        var after = cut.Find("input[type='password']");
        after.HasAttribute("value").ShouldBeFalse();
        after.ShouldNotBeSameAs(typedInto, "the typed text must not stay in the browser's input after a refusal");
        source.Keys.Count.ShouldBe(1);
    }

    /// <summary>The custom provider form ties each error to its field, keeps Save blocked while an entry is invalid, and closes with Cancel.</summary>
    [Fact]
    public async Task CustomProviderFormShowsFieldErrorsAndCancels()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Providers.Add(SettingsFixtures.CloudProvider("openai", "OpenAI"));
        var cut = await RenderPageAsync(source);
        cut.FindAll(".autonogrammer-settings-footer").Count.ShouldBe(0, "the footer appears with the form's draft");

        await cut.FindAll("button").Single(static button => string.Equals(button.TextContent, ModelsCopy.AddCustomProviderLabel, StringComparison.Ordinal)).ClickAsync().ConfigureAwait(true);

        var form = cut.Find(".autonogrammer-provider-form");
        form.GetAttribute("aria-labelledby").ShouldBe("autonogrammer-custom-provider-title");
        cut.Find("#autonogrammer-custom-provider-title").TextContent.ShouldBe(ModelsCopy.DraftTitleAdd);
        await cut.Find("#autonogrammer-custom-endpoint").InputAsync(new ChangeEventArgs { Value = "ftp://models.test" }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() =>
        {
            var endpoint = cut.Find("#autonogrammer-custom-endpoint");
            endpoint.GetAttribute("aria-invalid").ShouldBe("true");
            endpoint.GetAttribute("aria-describedby")!.Split(' ').ShouldContain("autonogrammer-custom-endpoint-error", StringComparer.Ordinal);
            cut.Find("#autonogrammer-custom-endpoint-error").TextContent.ShouldBe(ModelsCopy.ErrEndpointUnsupportedScheme);
        }).ConfigureAwait(true);
        SaveLine(cut).GetAttribute("data-state").ShouldBe("error");

        await cut.Find("button.autonogrammer-btn--primary").ClickAsync().ConfigureAwait(true);

        source.Forms.ShouldBeEmpty("an invalid form is never sent");
        SaveLine(cut).TextContent.ShouldBe(SettingsWebCopy.FixEntries);

        await cut.Find("button[aria-label='" + SettingsWebCopy.CancelProviderName + "']").ClickAsync().ConfigureAwait(true);

        cut.FindAll(".autonogrammer-provider-form").Count.ShouldBe(0);
        cut.FindAll(".autonogrammer-settings-footer").Count.ShouldBe(0);
    }

    /// <summary>The directory lists coding plans with the vendor's rule, the interactive-use tag, tier lines and links that open in a new tab with names that carry their label.</summary>
    [Fact]
    public async Task ListingCardShowsPlanRuleTagsTiersAndLinks()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        var model = Page.Directory.Find("plan-us")!;

        var card = cut.Find("[data-listing='plan-us']");

        card.QuerySelector("h5")!.TextContent.ShouldBe("Plan US");
        card.QuerySelectorAll(".autonogrammer-provider-card-head .autonogrammer-chip").Select(static chip => chip.TextContent).ShouldBe([model.RegionText, model.KindText]);
        card.QuerySelector(".autonogrammer-provider-rule")!.TextContent.ShouldBe(model.PlanRuleText);
        card.QuerySelector(".autonogrammer-provider-tags")!.TextContent.ShouldContain(ProviderListingCopy.InteractiveOnlyTag);
        card.QuerySelectorAll("[aria-label='Plan US tiers'] li").Select(static line => line.TextContent).ShouldBe(model.PlanTierLines);
        card.QuerySelector(".autonogrammer-provider-status")!.TextContent.ShouldContain(ProviderListingCopy.VerifyBeforeUse);
        foreach (var link in card.QuerySelectorAll("a"))
        {
            link.GetAttribute("target").ShouldBe("_blank");
            link.GetAttribute("rel").ShouldBe("noopener noreferrer");
            link.GetAttribute("aria-label")!.ShouldContain(link.TextContent);
            link.GetAttribute("href")!.ShouldStartWith("https://");
        }

        card.QuerySelectorAll("a").Select(static link => link.TextContent).ShouldContain(ProviderListingCopy.DocsActionLabel, StringComparer.Ordinal);
        card.QuerySelectorAll("a").Select(static link => link.TextContent).ShouldContain(ProviderListingCopy.KeyPageActionLabel, StringComparer.Ordinal);
    }

    /// <summary>The region filter, the kind filter and the search narrow the directory, and each change is announced in a polite status.</summary>
    [Fact]
    public async Task DirectoryFiltersNarrowTheListings()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        source.Listings.Add(SettingsFixtures.PlanListing("plan-eu", "Plan EU", ProviderListingRegion.Europe));
        var cut = await RenderPageAsync(source);
        cut.FindAll("[data-listing]").Select(static card => card.GetAttribute("data-listing")).ShouldBe(["plan-eu", "plan-us"], ignoreOrder: true);

        var region = cut.Find("#autonogrammer-directory-region");
        var europe = region.QuerySelectorAll("option").Single(static option => option.TextContent.StartsWith(ProviderListingCopy.RegionTitle(ProviderListingRegion.Europe), StringComparison.Ordinal));
        await region.ChangeAsync(new ChangeEventArgs { Value = europe.GetAttribute("value") }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() =>
        {
            cut.FindAll("[data-listing]").Select(static card => card.GetAttribute("data-listing")).ShouldBe(["plan-eu"]);
            cut.Find("#autonogrammer-directory-region").QuerySelector("option[selected]")!.TextContent.ShouldStartWith("Europe");
        }).ConfigureAwait(true);
        cut.FindAll("[role='status'][aria-live='polite']").Select(static status => status.TextContent).ShouldContain(ProviderListingCopy.MatchCount(1), StringComparer.Ordinal);

        await cut.Find("#autonogrammer-directory-region").ChangeAsync(new ChangeEventArgs { Value = "0" }).ConfigureAwait(true);
        await cut.Find("#autonogrammer-directory-search").InputAsync(new ChangeEventArgs { Value = "US" }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll("[data-listing]").Select(static card => card.GetAttribute("data-listing")).ShouldBe(["plan-us"])).ConfigureAwait(true);
        cut.Find("label[for='autonogrammer-directory-region']").TextContent.ShouldBe(ProviderListingCopy.RegionFilterLabel);
        cut.Find("label[for='autonogrammer-directory-kind']").TextContent.ShouldBe(ProviderListingCopy.KindFilterLabel);

        await cut.Find("#autonogrammer-directory-search").InputAsync(new ChangeEventArgs { Value = "no such provider" }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll(".autonogrammer-settings-state").Select(static state => state.TextContent).ShouldContain(ProviderListingCopy.NoMatchesText, StringComparer.Ordinal)).ConfigureAwait(true);
    }

    /// <summary>A placeholder in an endpoint becomes a labelled input; typing a value fills the endpoint shown on the card.</summary>
    [Fact]
    public async Task UrlPlaceholderBecomesALabelledInputThatFillsTheEndpoint()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        var card = cut.Find("[data-listing='plan-us']");
        card.QuerySelector(".autonogrammer-provider-endpoint")!.TextContent.ShouldBe("https://{region}.plan.test/v1");
        var input = card.QuerySelector("input[type='text']")!;
        cut.Find("label[for='" + input.Id + "']").TextContent.ShouldBe("Region");
        input.GetAttribute("aria-label")!.ShouldContain("Region");
        input.GetAttribute("placeholder").ShouldBe("eu");

        await input.InputAsync(new ChangeEventArgs { Value = "eu" }).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-listing='plan-us'] .autonogrammer-provider-endpoint").TextContent.ShouldBe("https://eu.plan.test/v1")).ConfigureAwait(true);
    }

    /// <summary>Setting a listing up opens a form with the key as a write-only field; the key goes to the source once with the typed placeholder values, and never into the markup.</summary>
    [Fact]
    public async Task ListingSetupKeepsTheKeyWriteOnlyAndSendsItOnce()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        await cut.Find("[data-listing='plan-us'] input[type='text']").InputAsync(new ChangeEventArgs { Value = "eu" }).ConfigureAwait(true);

        await cut.Find("button[aria-label='" + ProviderListingCopy.SetupActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        var key = cut.Find("[data-listing='plan-us'] input[type='password']");
        key.HasAttribute("value").ShouldBeFalse();
        key.GetAttribute("autocomplete").ShouldBe("off");
        cut.Find("label[for='" + key.Id + "']").TextContent.ShouldBe(ModelsCopy.ApiKeyFieldLabel("Plan US"));
        await key.InputAsync(new ChangeEventArgs { Value = Key }).ConfigureAwait(true);
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
        await cut.Find("[data-listing='plan-us'] input[type='checkbox']").ChangeAsync(new ChangeEventArgs { Value = true }).ConfigureAwait(true);

        await cut.Find("button[aria-label='" + ProviderListingCopy.AddActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => source.Catalogue.Count.ShouldBe(1)).ConfigureAwait(true);
        var (request, sentKey) = source.Catalogue[0];
        sentKey.ShouldBe(Key);
        request.PlanRuleAccepted.ShouldBeTrue("the tick the user gave goes to the source");
        request.ListingId.ShouldBe("plan-us");
        request.UrlValues.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "eu" });
        await cut.WaitForAssertionAsync(() => cut.FindAll("[data-listing='plan-us'] input[type='password']").Count.ShouldBe(0)).ConfigureAwait(true);
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
        AssertNamedControls(cut);
    }

    /// <summary>A coding plan form shows the tick box with its accessible name; without the tick the add is refused with the reason, the typed key is not sent, and nothing is added.</summary>
    [Fact]
    public async Task PlanSetupAsksForTheRuleTickAndRefusesWithoutIt()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        await cut.Find("[data-listing='plan-us'] input[type='text']").InputAsync(new ChangeEventArgs { Value = "eu" }).ConfigureAwait(true);
        await cut.Find("button[aria-label='" + ProviderListingCopy.SetupActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);
        await cut.Find("[data-listing='plan-us'] input[type='password']").InputAsync(new ChangeEventArgs { Value = Key }).ConfigureAwait(true);

        var tick = cut.Find("[data-listing='plan-us'] input[type='checkbox']");
        tick.GetAttribute("aria-label").ShouldBe(ProviderListingCopy.PlanRuleAcceptName("Plan US"));
        tick.HasAttribute("checked").ShouldBeFalse();
        await cut.Find("button[aria-label='" + ProviderListingCopy.AddActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-listing='plan-us'] [role='alert']").TextContent.ShouldBe(ProviderListingCopy.PlanRuleRequired)).ConfigureAwait(true);
        source.Catalogue.ShouldBeEmpty();
        cut.Markup.ShouldNotContain(Key, Case.Sensitive);
    }

    /// <summary>When the operator disabled key entry the key input and the Add action are disabled, the reason is shown on the page and on the card, and no input is held.</summary>
    [Fact]
    public async Task DisabledKeyEntryDisablesTheKeyFieldAndShowsTheReason()
    {
        const string Reason = "Key entry is disabled by the operator in this test.";
        var source = new SettingsFixtures.ModelsSource { KeyWritesDisabledReason = Reason };
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        await cut.Find("[data-listing='plan-us'] input[type='text']").InputAsync(new ChangeEventArgs { Value = "eu" }).ConfigureAwait(true);

        await cut.Find("button[aria-label='" + ProviderListingCopy.SetupActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        cut.Find(".autonogrammer-settings-banner[data-key-writes='disabled']").TextContent.ShouldBe(Reason);
        var key = cut.Find("[data-listing='plan-us'] input[type='password']");
        key.HasAttribute("disabled").ShouldBeTrue();
        key.HasAttribute("value").ShouldBeFalse();
        cut.Find("[data-listing='plan-us'] .autonogrammer-settings-key").TextContent.ShouldContain(Reason, Case.Sensitive);
        cut.Find("button[aria-label='" + ProviderListingCopy.AddActionName("Plan US") + "']").HasAttribute("disabled").ShouldBeTrue();
        source.Catalogue.ShouldBeEmpty();
        AssertNamedControls(cut);
    }

    /// <summary>A listing form without a key is refused with the reason, and nothing is sent.</summary>
    [Fact]
    public async Task ListingSetupWithoutAKeyIsRefused()
    {
        var source = new SettingsFixtures.ModelsSource();
        source.Listings.Add(SettingsFixtures.PlanListing("plan-us", "Plan US", ProviderListingRegion.UnitedStates));
        var cut = await RenderPageAsync(source);
        await cut.Find("[data-listing='plan-us'] input[type='text']").InputAsync(new ChangeEventArgs { Value = "eu" }).ConfigureAwait(true);
        await cut.Find("button[aria-label='" + ProviderListingCopy.SetupActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        await cut.Find("button[aria-label='" + ProviderListingCopy.AddActionName("Plan US") + "']").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[data-listing='plan-us'] [role='alert']").TextContent.ShouldBe(ModelsCopy.ErrApiKeyRequired)).ConfigureAwait(true);
        source.Catalogue.ShouldBeEmpty();
    }

    /// <summary>While loading the page says so in a status; a failed load shows the reason in an alert with Try again, which loads again.</summary>
    [Fact]
    public async Task LoadingAndFailedStatesAreShown()
    {
        var source = new SettingsFixtures.ModelsSource { FailWith = "omp is not installed" };
        source.Providers.Add(SettingsFixtures.CloudProvider("openai", "OpenAI"));
        var page = new ModelsSettingsPage(source);
        var cut = Render<ModelsSettingsView>(parameters => parameters.Add(p => p.Page, page));
        cut.Find(".autonogrammer-settings-state").GetAttribute("role").ShouldBe("status");
        cut.Find(".autonogrammer-settings-state").TextContent.ShouldBe(ModelsCopy.LoadingText);

        await page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.Find("[role='alert']").TextContent.ShouldBe("omp is not installed")).ConfigureAwait(true);
        cut.Find(".autonogrammer-settings-state h4").TextContent.ShouldBe(ModelsCopy.ErrorTitle);

        await cut.Find(".autonogrammer-settings-state button").ClickAsync().ConfigureAwait(true);

        await cut.WaitForAssertionAsync(() => cut.FindAll("[data-provider='openai']").Count.ShouldBe(1)).ConfigureAwait(true);
    }

    private ModelsSettingsPage? _page;

    private ModelsSettingsPage Page => _page ?? throw new InvalidOperationException("The page has not been rendered yet.");

    private async Task<IRenderedComponent<ModelsSettingsView>> RenderPageAsync(SettingsFixtures.ModelsSource source)
    {
        _page = new ModelsSettingsPage(source);
        await _page.LoadAsync(Xunit.TestContext.Current.CancellationToken).ConfigureAwait(true);
        return Render<ModelsSettingsView>(parameters => parameters.Add(p => p.Page, _page));
    }

    private static AngleSharp.Dom.IElement SaveLine<T>(IRenderedComponent<T> cut)
        where T : IComponent =>
        cut.Find(".autonogrammer-settings-saveline");

    private static void AssertNamedControls<T>(IRenderedComponent<T> cut)
        where T : IComponent
    {
        foreach (var element in cut.FindAll("button, input, select, textarea"))
        {
            var named = element.HasAttribute("aria-label") || element.TextContent.Trim().Length > 0 || cut.FindAll("label[for='" + element.Id + "']").Count > 0;
            named.ShouldBeTrue("a control has no accessible name: " + element.OuterHtml);
        }

        cut.Markup.ShouldNotContain("<script", Case.Insensitive);
        cut.FindAll("[style]").Count.ShouldBe(0);
    }
}
