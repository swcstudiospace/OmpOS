using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>
/// The Add / Replace API key flow: the typed key lives only in the draft, goes to the source once, is cleared on success and on
/// failure and never shows on any model, notification, error or announcement.
/// </summary>
public sealed class ApiKeyFlowTests
{
    private const string Key = ModelsTestData.FakeKey;

    private static async Task<(ModelsSettingsPage Page, ScriptedModelsSource Source, ProviderCardModel Card)> OpenAsync(
        ProviderInfo? provider = null)
    {
        var source = new ScriptedModelsSource(provider ?? ModelsTestData.Perplexity(), ModelsTestData.Ollama());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = page.Groups.SelectMany(group => group.Cards).First(candidate => candidate.CanSetApiKey);
        return (page, source, card);
    }

    /// <summary>The action reads Add API key while omp holds no key and Replace API key once it does.</summary>
    [Fact]
    public async Task LabelIsAddThenReplace()
    {
        var (_, _, card) = await OpenAsync().ConfigureAwait(true);
        card.ApiKeyActionLabel.ShouldBe("Add API key");

        var (_, _, keyed) = await OpenAsync(ModelsTestData.Perplexity(CredentialState.Present)).ConfigureAwait(true);
        keyed.ApiKeyActionLabel.ShouldBe("Replace API key");
    }

    /// <summary>Only providers that take an API key offer the flow; a sign-in provider says to use omp login and offers no key.</summary>
    [Fact]
    public async Task OnlyApiKeyProvidersOfferTheFlow()
    {
        var source = new ScriptedModelsSource(ModelsTestData.Ollama(), ModelsTestData.SignInOnly(), ModelsTestData.OpenAi());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cards = page.Groups.SelectMany(group => group.Cards).ToDictionary(card => card.Id, StringComparer.Ordinal);

        cards["ollama-local"].CanSetApiKey.ShouldBeFalse();
        cards["anthropic"].CanSetApiKey.ShouldBeFalse();
        cards["anthropic"].SignInHint.ShouldBe(ModelsCopy.SignInHint);
        cards["openai"].CanSetApiKey.ShouldBeTrue();
        cards["openai"].SignInHint.ShouldBeNull();

        cards["anthropic"].BeginApiKey();
        cards["anthropic"].IsApiKeyEditorOpen.ShouldBeFalse();
        cards["openai"].BeginApiKey();
        cards["openai"].IsApiKeyEditorOpen.ShouldBeTrue();
    }

    /// <summary>A saved key is sent once, trimmed, to the right provider; the draft is cleared, the list reloads and no model holds the key.</summary>
    [Fact]
    public async Task SuccessSendsOnceClearsAndLeavesNoTrace()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync().ConfigureAwait(true);
        var pageNames = PropertyChangeRecorder.Record(page);
        var cardNames = PropertyChangeRecorder.Record(card);
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();
        var draftNames = PropertyChangeRecorder.Record(draft);
        draft.Entry = $"  {Key}\n";
        draft.HasEntry.ShouldBeTrue();
        draft.IsValid.ShouldBeTrue();
        draft.MaskedPreview.ShouldNotContain("sk", Case.Sensitive);

        var saved = await card.SaveApiKeyAsync(token).ConfigureAwait(true);

        saved.ShouldBeTrue();
        source.KeysReceived.ShouldBe([("perplexity", Key)]);
        draft.Entry.ShouldBeEmpty();
        card.IsApiKeyEditorOpen.ShouldBeFalse();
        card.ErrorText.ShouldBeNull();
        card.ApiKeyActionLabel.ShouldBe(ModelsCopy.ReplaceApiKeyLabel);
        card.StatusLine.ShouldContain("credential present in omp", Case.Sensitive);
        source.LoadCalls.ShouldBe(2);
        page.Announcement.ShouldBe(ModelsCopy.KeySaved("Perplexity"));
        SecretScan.ShouldNotHold(page, Key);
        SecretScan.ShouldNotHold(card, Key);
        SecretScan.ShouldNotHold(draft, Key);
        pageNames.Concat(cardNames).Concat(draftNames).ShouldAllBe(name => !name.Contains("sk-", StringComparison.Ordinal));
    }

    /// <summary>A refused key is cleared too: the entry stays open and empty with the source's message, and no model holds the key.</summary>
    [Fact]
    public async Task FailureClearsTheKeyAndShowsTheSourcesMessage()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync().ConfigureAwait(true);
        source.KeyResult = ModelsActionResult.Failure("omp could not write its credential database.");
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        var saved = await card.SaveApiKeyAsync(token).ConfigureAwait(true);

        saved.ShouldBeFalse();
        draft.Entry.ShouldBeEmpty();
        draft.HasEntry.ShouldBeFalse();
        card.IsApiKeyEditorOpen.ShouldBeTrue();
        card.ErrorText.ShouldBe("omp could not write its credential database.");
        card.ApiKeyActionLabel.ShouldBe(ModelsCopy.AddApiKeyLabel);
        source.LoadCalls.ShouldBe(1);
        SecretScan.ShouldNotHold(page, Key);
    }

    /// <summary>A source that quotes the key in its failure message cannot make the page show it.</summary>
    [Fact]
    public async Task AFailureMessageThatQuotesTheKeyIsHidden()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync().ConfigureAwait(true);
        source.KeyResult = ModelsActionResult.Failure("refused");
        source.EchoKeyInFailure = true;
        card.BeginApiKey();
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        (await card.SaveApiKeyAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        card.ErrorText.ShouldNotBeNull().ShouldNotContain(Key, Case.Sensitive);
        card.ErrorText.ShouldContain("[hidden]", Case.Sensitive);
        SecretScan.ShouldNotHold(page, Key);
    }

    /// <summary>The expected source exception is a failure too, with the key hidden and cleared.</summary>
    [Fact]
    public async Task ASourceExceptionIsAFailureWithTheKeyHidden()
    {
        var token = TestContext.Current.CancellationToken;
        var (page, source, card) = await OpenAsync().ConfigureAwait(true);
        source.ThrowOnKey = true;
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        (await card.SaveApiKeyAsync(token).ConfigureAwait(true)).ShouldBeFalse();

        draft.Entry.ShouldBeEmpty();
        card.ErrorText.ShouldNotBeNull().ShouldNotContain(Key, Case.Sensitive);
        SecretScan.ShouldNotHold(page, Key);
    }

    /// <summary>An empty entry is not sent; a key with a space or line break is rejected inline without quoting it; an over-long key is rejected.</summary>
    [Fact]
    public async Task InvalidEntriesAreNotSentAndNotQuoted()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, source, card) = await OpenAsync().ConfigureAwait(true);
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();

        (await card.SaveApiKeyAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        card.ErrorText.ShouldBe(ModelsCopy.ErrApiKeyRequired);

        draft.Entry = "sk-has space-inside";
        draft.IsValid.ShouldBeFalse();
        draft.Error.ShouldBe(ModelsCopy.ErrApiKeyCharacters);
        (await card.SaveApiKeyAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        card.ErrorText.ShouldNotBeNull().ShouldNotContain("sk-has", Case.Sensitive);

        draft.Entry = new string('k', CustomProviderRules.MaxApiKeyLength + 1);
        draft.Error.ShouldBe(ModelsCopy.ErrApiKeyTooLong);

        draft.Entry = new string('k', CustomProviderRules.MaxApiKeyLength);
        draft.Error.ShouldBeNull();
        source.KeysReceived.ShouldBeEmpty();
    }

    /// <summary>A second save while the first is still running sends nothing.</summary>
    [Fact]
    public async Task ASecondSaveWhileBusySendsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        var (_, source, card) = await OpenAsync().ConfigureAwait(true);
        source.KeyGate = new TaskCompletionSource();
        card.BeginApiKey();
        card.ApiKey.ShouldNotBeNull().Entry = Key;

        var first = card.SaveApiKeyAsync(token);
        card.IsBusy.ShouldBeTrue();
        (await card.SaveApiKeyAsync(token).ConfigureAwait(true)).ShouldBeFalse();
        source.KeyGate.SetResult();
        (await first.ConfigureAwait(true)).ShouldBeTrue();

        source.KeysReceived.Count.ShouldBe(1);
        card.IsBusy.ShouldBeFalse();
    }

    /// <summary>Cancelling the entry clears what was typed and closes it; ToString and the masked preview never show the key.</summary>
    [Fact]
    public async Task CancelClearsAndNothingPrintsTheKey()
    {
        var (_, _, card) = await OpenAsync().ConfigureAwait(true);
        card.BeginApiKey();
        var draft = card.ApiKey.ShouldNotBeNull();
        draft.Entry = Key;

        draft.ToString().ShouldNotContain(Key, Case.Sensitive);
        draft.ToString().ShouldContain("entry present", Case.Sensitive);
        draft.MaskedPreview.Length.ShouldBe(12);
        card.CancelApiKey();

        draft.Entry.ShouldBeEmpty();
        card.IsApiKeyEditorOpen.ShouldBeFalse();
        card.ApiKey.ShouldBeNull();
    }

    /// <summary>A provider omp knows says omp keeps the key in its credential database; a provider Autonogrammer wrote says Autonogrammer keeps it in its own store and omp keeps no copy.</summary>
    [Fact]
    public async Task EachCardStatesWhereItsKeyIsKept()
    {
        var (_, _, omp) = await OpenAsync().ConfigureAwait(true);
        var (_, _, custom) = await OpenAsync(ModelsTestData.Gateway()).ConfigureAwait(true);

        omp.BeginApiKey();
        custom.BeginApiKey();
        var ompNote = omp.ApiKey.ShouldNotBeNull().KeyStorageNote;
        var customNote = custom.ApiKey.ShouldNotBeNull().KeyStorageNote;

        ompNote.ShouldBe(ModelsCopy.OmpKeyStorageNote);
        ompNote.ShouldContain("omp keeps the key in its own credential database", Case.Sensitive);
        ompNote.ShouldContain("does not keep it", Case.Sensitive);
        customNote.ShouldBe(ModelsCopy.AutonogrammerKeyStorageNote);
        customNote.ShouldContain("Autonogrammer keeps the key in secrets/secrets.json", Case.Sensitive);
        customNote.ShouldContain("omp keeps no copy", Case.Sensitive);
        customNote.ShouldNotContain("does not keep it", Case.Sensitive);
    }
}
