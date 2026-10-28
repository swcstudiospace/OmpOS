using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>The card actions other than the key: use as default, test connection and remove with confirmation.</summary>
public sealed class ModelsActionTests
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

    /// <summary>Use as default writes the selector, marks exactly that row on every card, and announces it.</summary>
    [Fact]
    public async Task UseAsDefaultWritesAndMarksTheRow()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Ollama(), ModelsTestData.OpenAi()).ConfigureAwait(true);
        source.DefaultModel = "ollama-local/llama3";
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        Card(page, "ollama-local").Models.Single().IsDefault.ShouldBeTrue();
        var names = PropertyChangeRecorder.Record(page);

        var done = await Card(page, "openai").UseAsDefaultAsync("openai/gpt-4o", TestContext.Current.CancellationToken).ConfigureAwait(true);

        done.ShouldBeTrue();
        source.DefaultsRequested.ShouldBe(["openai/gpt-4o"]);
        page.DefaultModel.ShouldBe("openai/gpt-4o");
        names.ShouldAnnounce(nameof(ModelsSettingsPage.DefaultModel));
        Card(page, "ollama-local").Models.Single().IsDefault.ShouldBeFalse();
        var openAiRows = Card(page, "openai").Models.ToDictionary(row => row.Selector, StringComparer.Ordinal);
        openAiRows["openai/gpt-4o"].IsDefault.ShouldBeTrue();
        openAiRows["openai/gpt-4o"].CanUseAsDefault.ShouldBeFalse();
        openAiRows["openai/gpt-4o-mini"].IsDefault.ShouldBeFalse();
        openAiRows["openai/gpt-4o-mini"].CanUseAsDefault.ShouldBeTrue();
        page.Announcement.ShouldBe(ModelsCopy.DefaultSet("openai/gpt-4o"));
        source.LoadCalls.ShouldBe(2);
    }

    /// <summary>A model the card does not list, and the model that is already the default, are refused without a write.</summary>
    [Fact]
    public async Task UnknownOrCurrentDefaultIsNotSent()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        source.DefaultModel = "openai/gpt-4o";
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var card = Card(page, "openai");

        (await card.UseAsDefaultAsync("openai/not-listed", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        (await card.UseAsDefaultAsync("openai/gpt-4o", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        (await page.SetDefaultModelAsync("nowhere/model", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        source.DefaultsRequested.ShouldBeEmpty();
    }

    /// <summary>When omp refuses the default the card shows the reason and the default is unchanged.</summary>
    [Fact]
    public async Task RefusedDefaultShowsTheReasonAndKeepsTheOldDefault()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.OpenAi()).ConfigureAwait(true);
        source.DefaultModel = "openai/gpt-4o";
        source.DefaultResult = ModelsActionResult.Failure("omp could not write its config.");
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        (await page.SetDefaultModelAsync("openai/gpt-4o-mini", TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        var card = Card(page, "openai");
        card.ErrorText.ShouldBe("omp could not write its config.");
        page.DefaultModel.ShouldBe("openai/gpt-4o");
        card.Models.Single(row => row.IsDefault).Selector.ShouldBe("openai/gpt-4o");
    }

    /// <summary>Local providers show what the probe found: reachable, not running and unauthorized each have their own text.</summary>
    [Theory]
    [InlineData(ProviderTestStatus.Reachable, "Ollama answered with 3 models.")]
    [InlineData(ProviderTestStatus.NotRunning, "Nothing is listening on localhost:11434. Start Ollama.")]
    [InlineData(ProviderTestStatus.Unauthorized, "The server wants a key.")]
    [InlineData(ProviderTestStatus.Failed, "The server answered something that is not a model list.")]
    public async Task LocalProbeOutcomesShowTheSourcesMessage(ProviderTestStatus status, string message)
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Ollama()).ConfigureAwait(true);
        source.TestOutcomes["ollama-local"] = new ProviderTestOutcome(status, message);
        var card = Card(page, "ollama-local");

        await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        source.Tested.ShouldBe(["ollama-local"]);
        card.TestStatus.ShouldBe(status);
        card.TestText.ShouldBe(message);
        card.IsTesting.ShouldBeFalse();
        page.Announcement.ShouldBe(ModelsCopy.TestAnnouncement("Ollama (local)", message));
    }

    /// <summary>With no message from the source each status uses the page's own wording.</summary>
    [Theory]
    [InlineData(ProviderTestStatus.Reachable, nameof(ModelsCopy.TestReachable))]
    [InlineData(ProviderTestStatus.NotRunning, nameof(ModelsCopy.TestNotRunning))]
    [InlineData(ProviderTestStatus.Unauthorized, nameof(ModelsCopy.TestUnauthorized))]
    [InlineData(ProviderTestStatus.Failed, nameof(ModelsCopy.TestFailed))]
    public async Task BlankMessageFallsBackToTheCopy(ProviderTestStatus status, string copyName)
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Ollama()).ConfigureAwait(true);
        source.TestOutcomes["ollama-local"] = new ProviderTestOutcome(status, " ");
        var card = Card(page, "ollama-local");

        await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var expected = copyName switch
        {
            nameof(ModelsCopy.TestReachable) => ModelsCopy.TestReachable,
            nameof(ModelsCopy.TestNotRunning) => ModelsCopy.TestNotRunning,
            nameof(ModelsCopy.TestUnauthorized) => ModelsCopy.TestUnauthorized,
            _ => ModelsCopy.TestFailed,
        };
        card.TestText.ShouldBe(expected);
    }

    /// <summary>Cloud, coding plan and built-in rows say the test was not run for want of credentials unless the source returns a result.</summary>
    [Fact]
    public async Task CloudAndPlanRowsSayNotRunWithoutASourceResult()
    {
        var (page, source) = await LoadedAsync(
            ModelsTestData.Perplexity(),
            ModelsTestData.MiniMaxPlan(),
            ModelsTestData.OpenAi(),
            ModelsTestData.Gateway()).ConfigureAwait(true);

        foreach (var id in new[] { "perplexity", "minimax-m-plan", "openai" })
        {
            var card = Card(page, id);
            await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            card.TestStatus.ShouldBe(ProviderTestStatus.NotTestable);
            card.TestText.ShouldBe("Testing needs credentials the owner has not supplied; not run.");
        }

        var custom = Card(page, "my-gateway");
        await custom.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        custom.TestText.ShouldBe(ModelsCopy.TestUnavailable);

        source.TestOutcomes["perplexity"] = new ProviderTestOutcome(ProviderTestStatus.Reachable, "Answered with a model list.");
        var perplexity = Card(page, "perplexity");
        await perplexity.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        perplexity.TestStatus.ShouldBe(ProviderTestStatus.Reachable);
        perplexity.TestText.ShouldBe("Answered with a model list.");
    }

    /// <summary>A test shows the testing state while it runs; an expected source failure becomes a failed outcome.</summary>
    [Fact]
    public async Task TestShowsBusyStateAndSourceFailures()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Ollama()).ConfigureAwait(true);
        var card = Card(page, "ollama-local");
        source.TestGate = new TaskCompletionSource();

        var pending = card.TestAsync(TestContext.Current.CancellationToken);
        card.IsTesting.ShouldBeTrue();
        card.TestText.ShouldBeNull();
        await card.TestAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.Tested.Count.ShouldBe(1);

        source.TestThrowsMessage = "omp is not installed.";
        source.TestGate.SetResult();
        await pending.ConfigureAwait(true);

        card.IsTesting.ShouldBeFalse();
        card.TestStatus.ShouldBe(ProviderTestStatus.Failed);
        card.TestText.ShouldBe("omp is not installed.");
    }

    /// <summary>Only providers Autonogrammer wrote can be removed; the others show no remove action.</summary>
    [Fact]
    public async Task OnlyManagedProvidersCanBeRemoved()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Gateway(), ModelsTestData.OpenAi(), ModelsTestData.Ollama()).ConfigureAwait(true);

        foreach (var id in new[] { "openai", "ollama-local" })
        {
            var card = Card(page, id);
            card.CanRemove.ShouldBeFalse();
            card.BeginRemove();
            card.IsConfirmingRemove.ShouldBeFalse();
            (await card.ConfirmRemoveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        }

        source.Removed.ShouldBeEmpty();
        Card(page, "my-gateway").CanRemove.ShouldBeTrue();
    }

    /// <summary>Removal asks first: nothing is written until it is confirmed, and cancelling leaves the provider.</summary>
    [Fact]
    public async Task RemovalAsksFirstAndCanBeCancelled()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Gateway()).ConfigureAwait(true);
        var card = Card(page, "my-gateway");

        (await card.ConfirmRemoveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();
        source.Removed.ShouldBeEmpty();

        card.BeginRemove();
        card.IsConfirmingRemove.ShouldBeTrue();
        card.RemoveQuestion.ShouldBe(ModelsCopy.RemoveQuestion("My gateway"));
        source.Removed.ShouldBeEmpty();
        card.CancelRemove();
        card.IsConfirmingRemove.ShouldBeFalse();
        (await card.ConfirmRemoveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        source.Removed.ShouldBeEmpty();
        page.Groups.Count.ShouldBe(1);
    }

    /// <summary>A confirmed removal writes once and the provider leaves the list, which becomes empty.</summary>
    [Fact]
    public async Task ConfirmedRemovalRemovesTheProvider()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Gateway()).ConfigureAwait(true);
        var card = Card(page, "my-gateway");
        card.BeginRemove();

        (await card.ConfirmRemoveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        source.Removed.ShouldBe(["my-gateway"]);
        page.State.ShouldBe(ModelsPageState.Empty);
        page.Groups.ShouldBeEmpty();
        page.Announcement.ShouldBe(ModelsCopy.CustomRemoved("My gateway"));
    }

    /// <summary>A refused removal leaves the provider, closes the question and shows the reason.</summary>
    [Fact]
    public async Task RefusedRemovalKeepsTheProviderAndShowsTheReason()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Gateway()).ConfigureAwait(true);
        source.RemoveResult = ModelsActionResult.Failure("models.yml is not writable.");
        var card = Card(page, "my-gateway");
        card.BeginRemove();

        (await card.ConfirmRemoveAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeFalse();

        card.IsConfirmingRemove.ShouldBeFalse();
        card.ErrorText.ShouldBe("models.yml is not writable.");
        page.State.ShouldBe(ModelsPageState.Ready);
    }

    /// <summary>A change that omp accepted but whose reload failed keeps the page ready and says so in the notice.</summary>
    [Fact]
    public async Task ReloadFailureAfterAChangeIsANoticeNotAnError()
    {
        var (page, source) = await LoadedAsync(ModelsTestData.Perplexity()).ConfigureAwait(true);
        source.ReloadFailureMessage = "omp did not answer.";
        var card = Card(page, "perplexity");
        card.BeginApiKey();
        card.ApiKey.ShouldNotBeNull().Entry = ModelsTestData.FakeKey;

        (await card.SaveApiKeyAsync(TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBeTrue();

        page.State.ShouldBe(ModelsPageState.Ready);
        page.Notice.ShouldBe(ModelsCopy.RefreshFailed("omp did not answer."));
    }
}
