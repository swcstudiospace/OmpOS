using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Settings.Models;

/// <summary>The Models page states (Loading, Ready, Empty, Error), its groups, completeness note, warning and search filter.</summary>
public sealed class ModelsPageLoadTests
{
    /// <summary>The page fills the Models section and is clean before any edit.</summary>
    [Fact]
    public void PageFillsTheModelsSectionAndStartsClean()
    {
        var page = new ModelsSettingsPage(new ScriptedModelsSource());

        page.Section.ShouldBe(SettingsSection.Models);
        page.Title.ShouldBe(ModelsCopy.PageTitle);
        page.State.ShouldBe(ModelsPageState.Loading);
        page.IsDirty.ShouldBeFalse();
        page.HasError.ShouldBeFalse();
    }

    /// <summary>Loading, ready, empty and error are four different states with their own data.</summary>
    [Fact]
    public async Task LoadingReadyEmptyAndErrorAreDistinct()
    {
        var token = TestContext.Current.CancellationToken;

        var gate = new TaskCompletionSource();
        var loadingSource = new ScriptedModelsSource(ModelsTestData.Ollama()) { LoadGate = gate };
        var loading = new ModelsSettingsPage(loadingSource);
        var pending = loading.LoadAsync(token);
        var loadingState = loading.State;
        loading.Groups.ShouldBeEmpty();
        gate.SetResult();
        await pending.ConfigureAwait(true);

        var empty = new ModelsSettingsPage(new ScriptedModelsSource());
        await empty.LoadAsync(token).ConfigureAwait(true);

        var failed = new ModelsSettingsPage(new ScriptedModelsSource { LoadFailureMessage = "omp is not installed." });
        await failed.LoadAsync(token).ConfigureAwait(true);

        new[] { loadingState, loading.State, empty.State, failed.State }.Distinct().Count().ShouldBe(4);
        loadingState.ShouldBe(ModelsPageState.Loading);
        loading.State.ShouldBe(ModelsPageState.Ready);
        empty.State.ShouldBe(ModelsPageState.Empty);
        failed.State.ShouldBe(ModelsPageState.Error);
        failed.LoadFailure.ShouldBe("omp is not installed.");
        failed.Groups.ShouldBeEmpty();
    }

    /// <summary>The empty state explains that omp lists only providers with a credential and points to Add API key.</summary>
    [Fact]
    public void EmptyStateExplainsTheCredentialRule()
    {
        ModelsCopy.EmptyText.ShouldContain("only providers it has a credential for", Case.Sensitive);
        ModelsCopy.EmptyText.ShouldContain(ModelsCopy.AddApiKeyLabel, Case.Sensitive);
        ModelsCopy.IncompleteNote.ShouldContain("only providers it has credentials for", Case.Sensitive);
    }

    /// <summary>A load failure without text falls back to the copy, and the next load recovers.</summary>
    [Fact]
    public async Task BlankFailureUsesCopyAndReloadRecovers()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedModelsSource(ModelsTestData.Ollama()) { LoadFailureMessage = " " };
        var page = new ModelsSettingsPage(source);

        await page.LoadAsync(token).ConfigureAwait(true);
        page.State.ShouldBe(ModelsPageState.Error);
        page.LoadFailure.ShouldBe(ModelsCopy.LoadFailed);

        source.LoadFailureMessage = null;
        await page.LoadAsync(token).ConfigureAwait(true);
        page.State.ShouldBe(ModelsPageState.Ready);
        page.LoadFailure.ShouldBeNull();
    }

    /// <summary>Cancelling a load throws and leaves the page in the state it was in.</summary>
    [Fact]
    public async Task CancellingALoadRestoresTheState()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedModelsSource(ModelsTestData.Ollama());
        var page = new ModelsSettingsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);

        source.LoadGate = new TaskCompletionSource();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pending = page.LoadAsync(cancellation.Token);
        page.State.ShouldBe(ModelsPageState.Loading);
        await cancellation.CancelAsync().ConfigureAwait(true);

        Exception? thrown = null;
        try
        {
            await pending.ConfigureAwait(true);
        }
        catch (OperationCanceledException exception)
        {
            thrown = exception;
        }

        thrown.ShouldBeAssignableTo<OperationCanceledException>();
        page.State.ShouldBe(ModelsPageState.Ready);
        page.Groups.Count.ShouldBe(1);
    }

    /// <summary>The page covers local, cloud, coding plan, custom and omp built-in providers, in that order, each with its own heading.</summary>
    [Fact]
    public async Task EveryGroupIsListedInOrder()
    {
        var page = new ModelsSettingsPage(new ScriptedModelsSource(ModelsTestData.OneOfEveryGroup().Reverse().ToArray()));

        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.Groups.Select(group => group.Group).ShouldBe(
            [ProviderGroup.Local, ProviderGroup.Cloud, ProviderGroup.CodingPlan, ProviderGroup.Custom, ProviderGroup.OmpBuiltIn]);
        page.Groups.Select(group => group.Title).Distinct(StringComparer.Ordinal).Count().ShouldBe(5);
        page.Groups.ShouldAllBe(group => group.Description.Length > 0 && group.Cards.Count == 1);
        page.VisibleCardCount.ShouldBe(5);
    }

    /// <summary>A catalogue card shows its endpoint, its source with the date it was read, and its status line.</summary>
    [Fact]
    public async Task CatalogueCardCitesSourceAndDate()
    {
        var page = new ModelsSettingsPage(new ScriptedModelsSource(ModelsTestData.Perplexity()));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var card = page.Groups.Single().Cards.Single();

        card.EndpointText.ShouldBe("https://api.perplexity.ai");
        card.SourceText.ShouldBe("Source: https://docs.perplexity.ai/, read 2026-10-22");
        card.StatusLine.ShouldBe("API key, no credential in omp");
        card.HasPlan.ShouldBeFalse();
    }

    /// <summary>A coding plan card lists each tier with its published limit, the validity period and the plan page.</summary>
    [Fact]
    public async Task CodingPlanCardShowsQuotaValidityAndPlanPage()
    {
        var page = new ModelsSettingsPage(new ScriptedModelsSource(ModelsTestData.MiniMaxPlan()));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var card = page.Groups.Single().Cards.Single();

        card.HasPlan.ShouldBeTrue();
        card.PlanLines.ShouldBe(["Starter: about 100 requests per 5 hours", "Max: no request limit published"]);
        card.PlanValidityText.ShouldBe("A paid period lasts 30 days.");
        card.PlanUrl.ShouldBe(new Uri("https://platform.minimax.example/plan"));
    }

    /// <summary>An incomplete list says so, a complete one does not, and omp's configuration warning is shown.</summary>
    [Fact]
    public async Task IncompleteNoteAndWarningFollowTheSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        var source = new ScriptedModelsSource(ModelsTestData.OpenAi()) { IsComplete = false, Warning = "models.yml validation failed: line 3" };
        var page = new ModelsSettingsPage(source);

        await page.LoadAsync(token).ConfigureAwait(true);

        page.IsComplete.ShouldBeFalse();
        page.IncompleteNote.ShouldBe(ModelsCopy.IncompleteNote);
        page.Warning.ShouldBe("models.yml validation failed: line 3");

        source.IsComplete = true;
        source.Warning = null;
        await page.LoadAsync(token).ConfigureAwait(true);

        page.IncompleteNote.ShouldBeNull();
        page.Warning.ShouldBeNull();
    }

    /// <summary>The search box keeps cards whose name, id, endpoint, group or a model contains the text, ignoring case.</summary>
    [Theory]
    [InlineData("ollama", 1)]
    [InlineData("PERPLEX", 1)]
    [InlineData("localhost:11434", 1)]
    [InlineData("gpt-4o", 1)]
    [InlineData("coding plans", 1)]
    [InlineData("  my-gateway  ", 1)]
    [InlineData("a", 5)]
    [InlineData("zzz-nothing", 0)]
    public async Task SearchFiltersCards(string search, int expected)
    {
        var page = new ModelsSettingsPage(new ScriptedModelsSource(ModelsTestData.OneOfEveryGroup()));
        await page.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        page.SearchText = search;

        page.VisibleCardCount.ShouldBe(expected);
        page.HasNoMatches.ShouldBe(expected == 0);
        page.Announcement.ShouldBe(ModelsCopy.MatchCount(expected));
        page.Groups.ShouldAllBe(group => group.Cards.Count > 0);
    }

    /// <summary>Clearing the search brings every card back; a reload keeps the search and the same card objects.</summary>
    [Fact]
    public async Task ClearingSearchRestoresAndReloadKeepsIt()
    {
        var token = TestContext.Current.CancellationToken;
        var page = new ModelsSettingsPage(new ScriptedModelsSource(ModelsTestData.OneOfEveryGroup()));
        await page.LoadAsync(token).ConfigureAwait(true);
        var firstCard = page.Groups[0].Cards[0];

        page.SearchText = "zzz-nothing";
        page.HasNoMatches.ShouldBeTrue();
        page.SearchText = "ollama";
        await page.LoadAsync(token).ConfigureAwait(true);
        page.SearchText.ShouldBe("ollama");
        page.Groups.Single().Cards.Single().ShouldBeSameAs(firstCard);

        page.SearchText = string.Empty;
        page.VisibleCardCount.ShouldBe(5);
        page.HasNoMatches.ShouldBeFalse();
    }
}
