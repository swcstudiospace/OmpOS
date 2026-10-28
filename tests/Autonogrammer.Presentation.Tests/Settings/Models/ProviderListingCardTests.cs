using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>
/// One provider listing card: the facts it shows (country, residency, status, verify mark, plan terms) and the setup flow (route, URL
/// variables, model ids, write-only key) with the single hand-over of the key and no trace of it afterwards.
/// </summary>
public sealed class ProviderListingCardTests
{
    private const string Key = ModelsTestData.FakeKey;

    private static async Task<(ModelsSettingsPage Page, ListingModelsSource Source, ProviderListingCardModel Card)> OpenAsync(string id, ProviderListing[]? listings = null)
    {
        var source = new ListingModelsSource(listings ?? ListingTestData.All());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source, page.Directory.Find(id).ShouldNotBeNull());
    }

    [Fact]
    public async Task ACardShowsCountryResidencyStatusAndSource()
    {
        var (_, _, card) = await OpenAsync("mistral").ConfigureAwait(true);

        card.RegionText.ShouldBe("Europe");
        card.KindText.ShouldBe("Model APIs");
        card.CountryText.ShouldBe("France");
        card.ResidencyText.ShouldBe("Residency, as the vendor states it (not verified by Autonogrammer): EU hosting is inferred, not stated by the vendor");
        card.StatusText.ShouldBe("Verified 2026-10-27");
        card.VerifyMark.ShouldBeNull();
        card.VerifyBeforeUse.ShouldBeFalse();
        card.SourceText.ShouldBe("Source: https://docs.mistral.ai/openapi.yaml");
        card.DocsText.ShouldBe("Documentation: https://docs.mistral.ai/api");
        card.KeyPageText.ShouldBe("Get a key: https://console.mistral.ai/home");
        card.KeyEnvironmentName.ShouldBe("MISTRAL_API_KEY");
    }

    [Fact]
    public async Task ACardWithoutCountryOrResidencyShowsNeither()
    {
        var (_, _, card) = await OpenAsync("ollama").ConfigureAwait(true);

        card.CountryText.ShouldBeNull();
        card.ResidencyText.ShouldBeNull();
        card.DocsText.ShouldBeNull();
        card.KeyPageText.ShouldBeNull();
    }

    [Fact]
    public async Task AnUnverifiedCardSaysVerifyBeforeUseAndAPartialOneSaysPartlyVerified()
    {
        var (_, _, unverified) = await OpenAsync("baichuan").ConfigureAwait(true);
        unverified.VerifyBeforeUse.ShouldBeTrue();
        unverified.VerifyMark.ShouldBe("Verify before use");
        unverified.StatusText.ShouldBe("Unverified 2026-10-27");
        unverified.Note.ShouldNotBeNull().ShouldStartWith("Verify before use:", Case.Sensitive);

        var (_, _, partial) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        partial.VerifyBeforeUse.ShouldBeFalse();
        partial.VerifyMark.ShouldBe("Partly verified");
    }

    [Fact]
    public async Task APlanCardShowsTheRuleTheTagsTheTiersAndTheCloudSentence()
    {
        var (_, _, card) = await OpenAsync("zai").ConfigureAwait(true);

        card.HasPlan.ShouldBeTrue();
        card.PlanRuleText.ShouldBe("Vendor rule: Only in officially supported tools; bulk or automated usage and resale are banned.");
        card.PlanTags.ShouldBe(["Interactive use only", "Plan key only, not the pay-as-you-go key", "Cloud agents use pay-as-you-go keys"]);
        card.IsInteractiveOnly.ShouldBeTrue();
        card.PlanTierLines.ShouldBe(["Lite: $18 per month; 2,000 credits per 5 hours", "Pro: price unverified"]);
        card.PlanUrl.ShouldBe(new Uri("https://z.ai/subscribe"));
        card.PlanRuleSourceUrl.ShouldBe(new Uri("https://docs.z.ai/devpack/usage-policy.md"));
        card.PlanHostText.ShouldBe("Autonogrammer sends this key only to the plan's own host.");
    }

    [Fact]
    public async Task ANonPlanCardHasNoPlanTermsAndNoCloudSentence()
    {
        var (_, _, card) = await OpenAsync("mistral").ConfigureAwait(true);

        card.HasPlan.ShouldBeFalse();
        card.PlanTags.ShouldBeEmpty();
        card.PlanTierLines.ShouldBeEmpty();
        card.PlanRuleText.ShouldBeNull();
        card.PlanHostText.ShouldBeNull();
        card.IsInteractiveOnly.ShouldBeFalse();
    }

    [Fact]
    public async Task ASignInListingHasNoKeyFieldAndCannotBeSetUp()
    {
        var (_, _, card) = await OpenAsync("anthropic-subscription").ConfigureAwait(true);

        card.IsSignInOnly.ShouldBeTrue();
        card.SignInText.ShouldBe("Sign in inside omp with /login anthropic. Autonogrammer never stores this credential and shows no key field for it.");
        card.CanSetUp.ShouldBeFalse();
        card.CanBeginSetup.ShouldBeFalse();
        card.TakesKey.ShouldBeFalse();

        card.BeginSetup();

        card.IsSetupOpen.ShouldBeFalse();
        card.ApiKey.ShouldBeNull();
        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
    }

    [Fact]
    public async Task AnOmpNativeKeylessRuntimeNeedsNoSetup()
    {
        var (_, _, card) = await OpenAsync("ollama").ConfigureAwait(true);

        card.UsesOmpKey.ShouldBeTrue();
        card.TakesKey.ShouldBeFalse();
        card.CanSetUp.ShouldBeFalse();
    }

    [Fact]
    public async Task SettingUpAnOmpNativeProviderHandsTheKeyToOmpOnceAndClearsIt()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync("mistral").ConfigureAwait(true);
        card.UsesOmpKey.ShouldBeTrue();
        card.KeyStorageNote.ShouldBe(ModelsCopy.OmpKeyStorageNote);
        card.BeginSetup();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = $"  {Key}\n";
        draft.IsValid.ShouldBeTrue();

        var saved = await card.SaveAsync(token).ConfigureAwait(true);

        saved.ShouldBeTrue();
        source.OmpKeys.ShouldBe([("mistral", Key)]);
        source.CatalogueAdds.ShouldBeEmpty();
        draft.Entry.ShouldBeEmpty();
        card.IsSetupOpen.ShouldBeFalse();
        card.ApiKey.ShouldBeNull();
        card.ActionText.ShouldBe("Mistral AI La Plateforme was added to omp.");
        card.ConfiguredText.ShouldBe("Set up: omp holds a credential.");
        page.Announcement.ShouldBe("Mistral AI La Plateforme was added to omp.");
        SecretScan.ShouldNotHold(page, Key);
    }

    [Fact]
    public async Task SettingUpABlockProviderSendsTheRequestWithTheTypedValuesAndTheKeySeparately()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        card.UsesOmpKey.ShouldBeFalse();
        card.KeyStorageNote.ShouldBe(ModelsCopy.AutonogrammerKeyStorageNote);
        card.BaseAddressText.ShouldBe("https://api.infomaniak.com/2/ai/{product_id}/openai/v1");
        card.BeginSetup();
        card.UrlInputs.Select(input => input.Name).ShouldBe(["product_id"]);
        card.UrlInputs[0].Value = " 424242 ";
        card.ModelIdsText = "llama3, mistral3\nllama3";
        card.ApiKey.ShouldNotBeNull().Entry = Key;
        card.BaseAddressText.ShouldBe("https://api.infomaniak.com/2/ai/424242/openai/v1");

        var saved = await card.SaveAsync(token).ConfigureAwait(true);

        saved.ShouldBeTrue();
        var (request, key) = source.CatalogueAdds.ShouldHaveSingleItem();
        key.ShouldBe(Key);
        request.ListingId.ShouldBe("infomaniak-ai-tools");
        request.RouteId.ShouldBe("openai");
        request.UrlValues.ShouldBe(new Dictionary<string, string>(StringComparer.Ordinal) { ["product_id"] = "424242" });
        request.ModelIds.ShouldBe(["llama3", "mistral3"]);
        request.ToString().ShouldNotContain(Key, Case.Sensitive);
        source.OmpKeys.ShouldBeEmpty();
        card.ConfiguredText.ShouldBe("Set up: omp holds a credential.");
        SecretScan.ShouldNotHold(page, Key);
    }

    [Theory]
    [InlineData("", "Enter Product ID.")]
    [InlineData("your-product-id", "Product ID must be an identifier of letters, digits, dots and hyphens, not a placeholder.")]
    [InlineData("1/../x", "Product ID must be an identifier of letters, digits, dots and hyphens, not a placeholder.")]
    public async Task AnEmptyOrPlaceholderUrlValueIsRefusedBeforeAnythingIsSent(string value, string expected)
    {
        var (_, source, card) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        card.BeginSetup();
        card.UrlInputs[0].Value = value;
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe(expected);
        card.UrlInputs[0].Error.ShouldBe(expected);
        source.CatalogueAdds.ShouldBeEmpty();
        card.IsSetupOpen.ShouldBeTrue();
    }

    [Fact]
    public async Task ARouteWithoutAModelsListNeedsAModelIdAndAKey()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, source, card) = await OpenAsync("perplexity").ConfigureAwait(true);
        card.NeedsModelIds.ShouldBeTrue();
        card.ModelIdsHelp.ShouldBe(ProviderListingCopy.ModelIdsNeededText);
        card.BeginSetup();
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        (await card.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        card.ErrorText.ShouldBe(ProviderListingCopy.ModelIdsNeededText);

        card.ModelIdsText = "sonar";
        card.ApiKey.ShouldNotBeNull().Clear();
        (await card.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ErrApiKeyRequired);
        source.CatalogueAdds.ShouldBeEmpty();
    }

    [Fact]
    public async Task AKeylessRuntimeOmpDoesNotKnowIsWrittenWithAnEmptyKey()
    {
        var (_, source, card) = await OpenAsync("localai").ConfigureAwait(true);
        card.TakesKey.ShouldBeFalse();
        card.CanSetUp.ShouldBeTrue();
        card.BeginSetup();
        card.ApiKey.ShouldBeNull();

        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.CatalogueAdds.ShouldHaveSingleItem().Key.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChoosingASecondRouteChangesTheEndpointTheKeyPathAndTheModelRule()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, source, card) = await OpenAsync("deepseek").ConfigureAwait(true);
        card.HasRouteChoice.ShouldBeTrue();
        card.SelectedRoute.ShouldBe(card.Routes[0]);
        card.UsesOmpKey.ShouldBeTrue();
        card.NeedsModelIds.ShouldBeFalse();
        card.BeginSetup();
        var names = PropertyChangeRecorder.Record(card);

        card.SelectedRoute = card.Routes[1];

        card.BaseAddressText.ShouldBe("https://api.deepseek.com/anthropic");
        card.UsesOmpKey.ShouldBeFalse();
        card.NeedsModelIds.ShouldBeTrue();
        card.KeyStorageNote.ShouldBe(ModelsCopy.AutonogrammerKeyStorageNote);
        names.ShouldContain(nameof(ProviderListingCardModel.BaseAddressText), StringComparer.Ordinal);
        card.ApiKey.ShouldNotBeNull().Entry = Key;
        card.ModelIdsText = "deepseek-chat";

        (await card.SaveAsync(token).ConfigureAwait(true)).ShouldBeTrue();

        source.CatalogueAdds.ShouldHaveSingleItem().Request.RouteId.ShouldBe("anthropic");
        source.OmpKeys.ShouldBeEmpty();
        card.ConfiguredId.ShouldBe("deepseek-anthropic");
    }

    [Fact]
    public async Task ARouteThatIsNotOneOfTheListingsIsIgnored()
    {
        var (_, _, card) = await OpenAsync("deepseek").ConfigureAwait(true);
        var foreign = ListingTestData.Perplexity().Routes[0];

        card.SelectedRoute = foreign;
        card.SelectedRoute = null;

        card.SelectedRoute.ShouldBe(card.Routes[0]);
    }

    [Fact]
    public async Task AFailedAddShowsTheReasonWithoutTheKeyAndClearsTheEntry()
    {
        var (page, source, card) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        source.AddResult = ModelsActionResult.Failure("models.yml is read-only.");
        card.BeginSetup();
        card.UrlInputs[0].Value = "424242";
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe("models.yml is read-only.");
        draft.Entry.ShouldBeEmpty();
        card.IsSetupOpen.ShouldBeTrue();
        SecretScan.ShouldNotHold(page, Key);
    }

    [Fact]
    public async Task AFaultySourceThatQuotesTheKeyIsHiddenByTheCard()
    {
        var (page, source, card) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        source.AddResult = ModelsActionResult.Failure("unused");
        source.EchoKeyInFailure = true;
        card.BeginSetup();
        card.UrlInputs[0].Value = "424242";
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe("omp refused the key [hidden].");
        SecretScan.ShouldNotHold(page, Key);
    }

    [Fact]
    public async Task ASecondSaveWhileOneRunsSendsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, source, card) = await OpenAsync("infomaniak-ai-tools").ConfigureAwait(true);
        var gate = new TaskCompletionSource();
        source.AddGate = gate;
        card.BeginSetup();
        card.UrlInputs[0].Value = "424242";
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        var first = card.SaveAsync(token);
        card.IsBusy.ShouldBeTrue();
        (await card.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        gate.SetResult();
        (await first.ConfigureAwait(true)).ShouldBeTrue();

        source.CatalogueAdds.Count.ShouldBe(1);
        card.IsBusy.ShouldBeFalse();
    }

    [Fact]
    public async Task CancellingTheFormClearsTheTypedKey()
    {
        var (_, source, card) = await OpenAsync("mistral").ConfigureAwait(true);
        card.BeginSetup();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        card.CancelSetup();

        draft.Entry.ShouldBeEmpty();
        card.IsSetupOpen.ShouldBeFalse();
        card.ApiKey.ShouldBeNull();
        card.CanBeginSetup.ShouldBeTrue();
        source.OmpKeys.ShouldBeEmpty();
    }

    [Fact]
    public async Task AHeadWithoutCatalogueSupportSaysSoInsteadOfFailingSilently()
    {
        var source = new LegacyModelsSource(ListingTestData.Infomaniak());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = page.Directory.Find("infomaniak-ai-tools").ShouldNotBeNull();
        card.BeginSetup();
        card.UrlInputs[0].Value = "424242";
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        (await card.SaveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldBe(ProviderListingCopy.NotSupportedText);
        SecretScan.ShouldNotHold(page, Key);
    }

    [Fact]
    public async Task TheCardNeverHoldsTheKeyWhileItIsTypedOutsideTheDraft()
    {
        var (page, _, card) = await OpenAsync("mistral").ConfigureAwait(true);
        card.BeginSetup();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        draft.MaskedPreview.ShouldNotContain("sk", Case.Sensitive);
        draft.ToString().ShouldNotContain(Key, Case.Sensitive);
        card.AddActionName.ShouldNotContain(Key, Case.Sensitive);
        card.Listing.ToString().ShouldNotContain(Key, Case.Sensitive);
        page.Directory.Announcement.ShouldNotContain(Key, Case.Sensitive);
    }
}
