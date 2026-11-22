using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>
/// Typed-flow discipline of the Models page (Tc00c AC-106 to AC-110, AC-126): every blocked flow ends with exactly 1 message
/// naming the cause and returns to a non-busy state, and a cancelled flow never drops silently.
/// </summary>
public sealed class ModelsFlowDisciplineTests
{
    private static async Task<(ModelsSettingsPage Page, ScriptedModelsSource Source)> LoadedAsync(params ProviderInfo[] providers)
    {
        var source = new ScriptedModelsSource(providers);
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        return (page, source);
    }

    private static ProviderCardModel Card(ModelsSettingsPage page, string id) =>
        page.Groups.SelectMany(group => group.Cards).Single(card => string.Equals(card.Id, id, StringComparison.Ordinal));

    private static int MessageCount(ProviderCardModel card) =>
        new[] { card.ErrorText, card.ActionText, card.TestText }.Count(text => !string.IsNullOrWhiteSpace(text));

    private static int ListingMessageCount(ProviderListingCardModel card) =>
        new[] { card.ErrorText, card.ActionText }.Count(text => !string.IsNullOrWhiteSpace(text));

    [Fact]
    public async Task ATestCancelledBeforeItStartsReportsFailedWithOneMessageAndStopsTesting()
    {
        var (page, _) = await LoadedAsync(ModelsTestData.Ollama()).ConfigureAwait(true);
        var card = Card(page, "ollama-local");

        await card.TestAsync(new CancellationToken(canceled: true)).ConfigureAwait(true);

        card.TestStatus.ShouldBe(ProviderTestStatus.Failed);
        card.TestText.ShouldBe(ModelsCopy.TestCancelled);
        card.IsTesting.ShouldBeFalse();
        card.ErrorText.ShouldBeNull();
        card.ActionText.ShouldBeNull();
        MessageCount(card).ShouldBe(1, "a blocked test shows exactly one message");
        page.Announcement.ShouldBe(ModelsCopy.TestAnnouncement("Ollama (local)", ModelsCopy.TestCancelled));
        MessageCount(card).ShouldBe(1);
    }

    [Fact]
    public async Task ATestCancelledWhileItRunsReportsFailedWithOneMessageAndStopsTesting()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Ollama()).ConfigureAwait(true);
        var card = Card(page, "ollama-local");
        source.TestGate = new TaskCompletionSource();
        using var cts = new CancellationTokenSource();
        var pending = card.TestAsync(cts.Token);
        card.IsTesting.ShouldBeTrue();

        await cts.CancelAsync().ConfigureAwait(true);
        await pending.ConfigureAwait(true);

        card.TestStatus.ShouldBe(ProviderTestStatus.Failed);
        card.TestText.ShouldBe(ModelsCopy.TestCancelled);
        card.IsTesting.ShouldBeFalse();
        MessageCount(card).ShouldBe(1);
        page.Announcement.ShouldBe(ModelsCopy.TestAnnouncement("Ollama (local)", ModelsCopy.TestCancelled));
    }

    [Fact]
    public async Task ATestSourceExceptionReportsFailedWithTheCauseAndStopsTesting()
    {

        var thrown = new ScriptedModelsSource(ModelsTestData.Ollama()) { TestThrowsMessage = "the probe timed out." };
        var throwingPage = new ModelsSettingsPage(thrown);
        await throwingPage.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var throwingCard = throwingPage.Groups.SelectMany(group => group.Cards).Single(candidate => string.Equals(candidate.Id, "ollama-local", StringComparison.Ordinal));

        await throwingCard.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        throwingCard.TestStatus.ShouldBe(ProviderTestStatus.Failed);
        throwingCard.TestText.ShouldBe("the probe timed out.");
        throwingCard.IsTesting.ShouldBeFalse();
        MessageCount(throwingCard).ShouldBe(1);
    }

    [Fact]
    public async Task AKeySaveCancelledBeforeItSendsReportsOneFailureAndStaysNonBusy()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        var card = Card(page, "openai");
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = ModelsTestData.FakeKey;
        draft.IsValid.ShouldBeTrue();

        var saved = await card.SaveApiKeyAsync(new CancellationToken(canceled: true)).ConfigureAwait(true);

        saved.ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ActionCancelled);
        card.ActionText.ShouldBeNull();
        card.IsBusy.ShouldBeFalse();
        draft.Entry.ShouldBeEmpty();
        card.IsApiKeyEditorOpen.ShouldBeTrue("a cancelled save keeps the entry open");
        MessageCount(card).ShouldBe(1);
        source.KeysReceived.ShouldBeEmpty();
    }

    [Fact]
    public async Task AUseAsDefaultCancelledBeforeItSendsReportsOneFailureAndStaysNonBusy()
    {
        var (page, _) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        var card = Card(page, "openai");

        var done = await card.UseAsDefaultAsync("openai/gpt-4o", new CancellationToken(canceled: true)).ConfigureAwait(true);

        done.ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ActionCancelled);
        card.ActionText.ShouldBeNull();
        card.IsBusy.ShouldBeFalse();
        MessageCount(card).ShouldBe(1);
    }

    [Fact]
    public async Task ARemovalCancelledBeforeItSendsReportsOneFailureAndStaysNonBusy()
    {
        var (page, _) = await LoadedAsync(ModelsTestData.Gateway()).ConfigureAwait(true);
        var card = Card(page, "my-gateway");
        card.BeginRemove();

        var removed = await card.ConfirmRemoveAsync(new CancellationToken(canceled: true)).ConfigureAwait(true);

        removed.ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ActionCancelled);
        card.IsBusy.ShouldBeFalse();
        card.IsConfirmingRemove.ShouldBeFalse();
        MessageCount(card).ShouldBe(1);
    }

    [Fact]
    public async Task AListingSaveCancelledBeforeItSendsReportsOneFailureAndStaysNonBusy()
    {
        var (_, source, card) = await OpenListingAsync().ConfigureAwait(true);
        card.BeginSetup();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = ModelsTestData.FakeKey;
        draft.IsValid.ShouldBeTrue();

        var saved = await card.SaveAsync(new CancellationToken(canceled: true)).ConfigureAwait(true);

        saved.ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ActionCancelled);
        card.ActionText.ShouldBeNull();
        card.IsBusy.ShouldBeFalse();
        draft.Entry.ShouldBeEmpty();
        ListingMessageCount(card).ShouldBe(1);
        source.OmpKeys.ShouldBeEmpty();
        source.CatalogueAdds.ShouldBeEmpty();
    }

    [Fact]
    public async Task ACustomSaveCancelledKeepsTheDraftOpenWithOneError()
    {
        var (page, _) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        page.BeginAddCustom();
        var draft = page.Draft.ShouldNotBeNull();
        draft.DisplayName = "My gateway";
        draft.Id = "my-gateway";
        draft.EndpointText = "https://gateway.example.com/v1";
        draft.ModelsText = "fast";

        var saved = await page.SaveAsync(new CancellationToken(canceled: true)).ConfigureAwait(true);

        saved.ShouldBeFalse();
        page.Draft.ShouldNotBeNull("a cancelled save keeps the form open");
        page.DraftError.ShouldBe(ModelsCopy.ActionCancelled);
        MessageCount(page.Groups.SelectMany(group => group.Cards).First()).ShouldBe(0, "no card shows a message for a draft error");
    }

    [Fact]
    public async Task ATestAfterAnActionReplacesTheOlderMessage()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        source.DefaultModel = "openai/gpt-4o-mini";
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = Card(page, "openai");

        (await card.UseAsDefaultAsync("openai/gpt-4o", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();
        card.ActionText.ShouldNotBeNull();
        MessageCount(card).ShouldBe(1);

        await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        card.ActionText.ShouldBeNull();
        card.ErrorText.ShouldBeNull();
        card.TestText.ShouldNotBeNull();
        card.IsTesting.ShouldBeFalse();
        MessageCount(card).ShouldBe(1);
    }

    [Fact]
    public async Task AnActionAfterATestReplacesTheOlderMessage()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        source.TestOutcomes["openai"] = new ProviderTestOutcome(ProviderTestStatus.Reachable, string.Empty);
        source.DefaultModel = "openai/gpt-4o-mini";
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = Card(page, "openai");

        await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        card.TestText.ShouldNotBeNull();
        MessageCount(card).ShouldBe(1);

        (await card.UseAsDefaultAsync("openai/gpt-4o", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        card.TestText.ShouldBeNull();
        card.TestStatus.ShouldBeNull();
        card.ActionText.ShouldNotBeNull();
        MessageCount(card).ShouldBe(1);
    }

    private static async Task<(ModelsSettingsPage Page, ListingModelsSource Source, ProviderListingCardModel Card)> OpenListingAsync()
    {
        var listings = new[] { ListingTestData.Mistral() };
        var source = new ListingModelsSource(listings);
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = page.Directory.Find("mistral").ShouldNotBeNull();
        return (page, source, card);
    }
}
