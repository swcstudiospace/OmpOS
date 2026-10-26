using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The Models page: provider cards in their groups, the models and the default, search, notes, and Test connection.</summary>
public sealed class ModelsPageViewTests
{
    private static readonly string[] CardOrder = ["ollama", "openrouter", "zai-plan", "mine", "openai", "anthropic"];

    internal static ProviderCardView Card(ModelsPageView view, string id) =>
        view.CardViews.FirstOrDefault(card => string.Equals(card.Card?.Id, id, StringComparison.Ordinal)).ShouldNotBeNull($"no card is drawn for {id}");

    /// <summary>The cards are drawn group by group in the fixed group order, each group under its title and description.</summary>
    [Fact]
    public async Task CardsAreDrawnGroupByGroupInOrderUnderTheirGroupHeaders()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            view.CardViews.Select(static card => card.Card!.Id).ShouldBe(CardOrder);
            var groups = new[] { ProviderGroup.Local, ProviderGroup.Cloud, ProviderGroup.CodingPlan, ProviderGroup.Custom, ProviderGroup.OmpBuiltIn };
            var texts = SettingsTree.Texts(view);
            texts.Where(text => groups.Any(group => string.Equals(ModelsCopy.GroupTitle(group), text, StringComparison.Ordinal)))
                .ShouldBe(groups.Select(ModelsCopy.GroupTitle));
            foreach (var group in groups)
            {
                texts.ShouldContain(ModelsCopy.GroupDescription(group), StringComparer.Ordinal);
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A card shows the name, the status line, the endpoint, the source line and the models, and offers only the actions omp can back.</summary>
    [Fact]
    public async Task ACardShowsItsFactsAndOnlyTheActionsOmpCanDo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            var ollama = Card(view, "ollama");
            var ollamaTexts = SettingsTree.Texts(ollama);
            ollamaTexts.ShouldContain("Ollama", StringComparer.Ordinal);
            ollamaTexts.ShouldContain(ModelsCopy.StatusLine(ProviderAuthKind.None, CredentialState.NotRequired), StringComparer.Ordinal);
            ollamaTexts.ShouldContain("http://localhost:11434/v1", StringComparer.Ordinal);
            ollamaTexts.ShouldContain("ollama/llama3", StringComparer.Ordinal);
            ollama.ApiKey.IsEffectivelyVisible.ShouldBeFalse("a provider that takes no key has no key action");
            ollama.Edit.IsEffectivelyVisible.ShouldBeFalse();
            ollama.Remove.IsEffectivelyVisible.ShouldBeFalse();
            ollama.Test.IsEffectivelyVisible.ShouldBeTrue();

            var openrouter = Card(view, "openrouter");
            openrouter.ApiKey.IsEffectivelyVisible.ShouldBeTrue();
            openrouter.ApiKey.Content.ShouldBe(ModelsCopy.AddApiKeyLabel);
            AutomationProperties.GetName(openrouter.ApiKey).ShouldBe(SettingsViewCopy.ForProvider(ModelsCopy.AddApiKeyLabel, "OpenRouter"));
            SettingsTree.Texts(openrouter).ShouldContain(ModelsCopy.StatusLine(ProviderAuthKind.ApiKey, CredentialState.Absent), StringComparer.Ordinal);

            var openai = Card(view, "openai");
            openai.ApiKey.Content.ShouldBe(ModelsCopy.ReplaceApiKeyLabel);

            var mine = Card(view, "mine");
            mine.Edit.IsEffectivelyVisible.ShouldBeTrue();
            mine.Remove.IsEffectivelyVisible.ShouldBeTrue();
            AutomationProperties.GetName(mine.Edit).ShouldBe(SettingsViewCopy.ForProvider(ModelsCopy.EditLabel, "My Gateway"));

            var anthropic = Card(view, "anthropic");
            var anthropicTexts = SettingsTree.Texts(anthropic);
            anthropic.ApiKey.IsEffectivelyVisible.ShouldBeFalse("omp signs in by itself; there is no sign-in action here");
            anthropicTexts.ShouldContain(ModelsCopy.SignInHint, StringComparer.Ordinal);
            anthropicTexts.ShouldContain(ModelsCopy.NoModelsText, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A coding plan shows its tiers with their limits, how long a paid period lasts and where the plan is described, as selectable named text.</summary>
    [Fact]
    public async Task ACodingPlanShowsItsQuotaValidityAndAddressAsSelectableText()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var plan = Card(view, "zai-plan");

            var selectable = SettingsTree.Visible<SelectableTextBlock>(plan).Select(static block => block.Text).ToList();

            selectable.ShouldBe(
            [
                ModelsCopy.PlanLine("Lite", "about 120 requests per 5 hours"),
                ModelsCopy.PlanLine("Pro", "about 600 requests per 5 hours"),
                ModelsCopy.ValidityLine(30),
                "https://plans.example.test/pricing",
            ]);
            foreach (var block in SettingsTree.Visible<SelectableTextBlock>(plan))
            {
                block.Focusable.ShouldBeTrue();
                AutomationProperties.GetName(block).ShouldBe(block.Text);
            }

            SettingsTree.Texts(plan).ShouldContain(ModelsCopy.SourceLine(new Uri("https://plans.example.test/pricing"), new DateOnly(2026, 10, 22)), StringComparer.Ordinal);
            SettingsTree.Texts(Card(view, "ollama")).ShouldNotContain(ModelsCopy.ValidityLine(30), StringComparer.Ordinal);
            SettingsTree.Visible<SelectableTextBlock>(Card(view, "ollama")).ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The default model carries the badge and no action; every other model offers Use as default under its own accessible name, and taking it moves the badge.</summary>
    [Fact]
    public async Task UseAsDefaultMovesTheDefaultBadge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var openai = Card(view, "openai");

            SettingsTree.Texts(openai).Count(static text => string.Equals(text, ModelsCopy.DefaultModelLabel, StringComparison.Ordinal)).ShouldBe(1);
            var offered = openai.UseAsDefaultButtons.Where(static button => button.IsEffectivelyVisible).ToList();
            offered.Select(static button => AutomationProperties.GetName(button))
                .ShouldBe([ModelsCopy.UseAsDefaultName("openai/gpt-4o-mini")]);
            SettingsTree.Visible<Button>(view).Where(static button => button.Classes.Contains("use-default")).Count().ShouldBe(6, "every model but the default offers the action");

            SettingsTree.Click(fixture.Window, offered[0]);

            fixture.ModelsSource.Defaults.ShouldBe(["openai/gpt-4o-mini"]);
            fixture.Models.DefaultModel.ShouldBe("openai/gpt-4o-mini");
            var after = Card(view, "openai").UseAsDefaultButtons.Where(static button => button.IsEffectivelyVisible).ToList();
            after.Select(static button => AutomationProperties.GetName(button)).ShouldBe([ModelsCopy.UseAsDefaultName("openai/gpt-4o")]);
            SettingsTree.Texts(Card(view, "openai")).Count(static text => string.Equals(text, ModelsCopy.DefaultModelLabel, StringComparison.Ordinal)).ShouldBe(1);
            view.Live.Text.ShouldBe(ModelsCopy.DefaultSet("openai/gpt-4o-mini"));
        }, ct).ConfigureAwait(true);
    }

    /// <summary>When omp refuses a default the card says so and the badge stays where it was.</summary>
    [Fact]
    public async Task ARefusedDefaultShowsItsReasonAndKeepsTheBadge()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.ModelsSource.DefaultResult = ModelsActionResult.Failure("omp config is locked.")).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var button = Card(view, "openai").UseAsDefaultButtons.First(static candidate => candidate.IsEffectivelyVisible);

            SettingsTree.Click(fixture.Window, button);

            var openai = Card(view, "openai");
            openai.Error.IsEffectivelyVisible.ShouldBeTrue();
            openai.Error.Text.ShouldBe("omp config is locked.");
            fixture.Models.DefaultModel.ShouldBe(ModelsData.DefaultSelector);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Typing in the search box keeps the matching cards and says how many match; a search that matches nothing says so.</summary>
    [Fact]
    public async Task SearchFiltersTheCardsAndAnnouncesTheCount()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            view.Search.Text = "ollama";
            fixture.Settle();
            view.CardViews.Select(static card => card.Card!.Id).ShouldBe(["ollama"]);
            view.Live.Text.ShouldBe(ModelsCopy.MatchCount(1));

            view.Search.Text = "no-such-provider";
            fixture.Settle();
            view.CardViews.ShouldBeEmpty();
            view.NoMatches.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain(ModelsCopy.NoMatchesText, StringComparer.Ordinal);

            view.Search.Text = string.Empty;
            fixture.Settle();
            view.CardViews.Count.ShouldBe(CardOrder.Length);
            view.NoMatches.IsEffectivelyVisible.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>omp's warning shows as a banner, and an incomplete list says that omp lists only providers it has credentials for.</summary>
    [Fact]
    public async Task TheWarningAndTheIncompleteNoteAreShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            view.Warning.IsEffectivelyVisible.ShouldBeFalse();
            view.Incomplete.IsEffectivelyVisible.ShouldBeFalse();

            fixture.ModelsSource.Snapshot = fixture.ModelsSource.Snapshot with { Warning = "models.yml did not validate: line 3.", IsComplete = false };
            await fixture.Models.LoadAsync(ct).ConfigureAwait(true);
            fixture.Settle();

            view.Warning.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain("models.yml did not validate: line 3.", StringComparer.Ordinal);
            view.Incomplete.IsEffectivelyVisible.ShouldBeTrue();
            view.Incomplete.Text.ShouldBe(ModelsCopy.IncompleteNote);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An empty list says why and what to do, and the page keeps its search and its Add provider button.</summary>
    [Fact]
    public async Task AnEmptyListKeepsAddProviderAndExplainsItself()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(
                ct,
                static f => f.ModelsSource.Snapshot = new ModelsSnapshot([], null, null, false)).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            view.Empty.IsEffectivelyVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain(ModelsCopy.EmptyTitle, StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldContain(ModelsCopy.EmptyText, StringComparer.Ordinal);
            view.CardViews.ShouldBeEmpty();
            view.AddProvider.IsEffectivelyVisible.ShouldBeTrue();
            view.Search.IsEffectivelyVisible.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Test connection shows what the test found, in words and in a colour role, in a polite live region; the page announces it too.</summary>
    [Fact]
    public async Task TestConnectionShowsItsOutcomeInALiveRegion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            SettingsTree.Click(fixture.Window, Card(view, "ollama").Test);
            var ollama = Card(view, "ollama");
            var expected = ModelsCopy.TestText(ProviderTestStatus.Reachable, "Answered on the endpoint.", ProviderGroup.Local);
            fixture.ModelsSource.Tested.ShouldBe(["ollama"]);
            ollama.TestResult.IsEffectivelyVisible.ShouldBeTrue();
            ollama.TestResult.Text.ShouldBe(expected);
            ollama.TestResult.Classes.ShouldContain("tone-success", StringComparer.Ordinal);
            AutomationProperties.GetLiveSetting(ollama.TestResult).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(ollama.TestResult).ShouldBe(expected);
            view.Live.Text.ShouldBe(ModelsCopy.TestAnnouncement("Ollama", expected));

            fixture.ModelsSource.TestOutcome = new ProviderTestOutcome(ProviderTestStatus.NotRunning, "Nothing answered.");
            SettingsTree.Click(fixture.Window, ollama.Test);
            ollama.TestResult.Text.ShouldBe(ModelsCopy.TestText(ProviderTestStatus.NotRunning, "Nothing answered.", ProviderGroup.Local));
            ollama.TestResult.Classes.ShouldContain("tone-error", StringComparer.Ordinal);
            ollama.TestResult.Classes.ShouldNotContain("tone-success", StringComparer.Ordinal);

            fixture.ModelsSource.TestOutcome = new ProviderTestOutcome(ProviderTestStatus.NotTestable, string.Empty);
            SettingsTree.Click(fixture.Window, Card(view, "openrouter").Test);
            var openrouter = Card(view, "openrouter");
            openrouter.TestResult.Text.ShouldBe(ModelsCopy.TestText(ProviderTestStatus.NotTestable, string.Empty, ProviderGroup.Cloud));
            openrouter.TestResult.Classes.ShouldContain("tone-secondary", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>While a test runs its button is off, the card says Testing, and a second press cannot start another test.</summary>
    [Fact]
    public async Task ATestInProgressDisablesItsButton()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var gate = new TaskCompletionSource();
            using var fixture = await SettingsFixture.OpenAsync(ct, f => f.ModelsSource.TestGate = gate).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            var ollama = Card(view, "ollama");

            SettingsTree.Click(fixture.Window, ollama.Test);

            ollama.Test.IsEnabled.ShouldBeFalse();
            SettingsTree.Texts(ollama).ShouldContain(ModelsCopy.TestingText, StringComparer.Ordinal);
            fixture.ModelsSource.Tested.ShouldBe(["ollama"]);

            gate.SetResult();
            fixture.Settle();

            ollama.Test.IsEnabled.ShouldBeTrue();
            SettingsTree.Texts(ollama).ShouldNotContain(ModelsCopy.TestingText, StringComparer.Ordinal);
            ollama.TestResult.IsEffectivelyVisible.ShouldBeTrue();
        }, ct).ConfigureAwait(true);
    }
}
