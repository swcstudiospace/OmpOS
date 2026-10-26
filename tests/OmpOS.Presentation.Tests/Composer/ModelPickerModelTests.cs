using OmpOS.Presentation.Composer;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Composer;

/// <summary>
/// The composer model picker: the chip shows the model the engine uses (VX-02), a model the engine cannot take leaves the active model
/// and sets a notice, and a long list is grouped by provider and filtered.
/// </summary>
public sealed class ModelPickerModelTests
{
    private static readonly ModelChoice Sonnet = ModelChoice.Create("anthropic/claude-sonnet", "Claude Sonnet");
    private static readonly ModelChoice Opus = ModelChoice.Create("anthropic/claude-opus", "Claude Opus");
    private static readonly ModelChoice Gpt = ModelChoice.Create("openai/gpt-x", "GPT X");
    private static readonly ModelChoice Router = ModelChoice.Create("openrouter/anthropic/claude-3", "Claude 3 via router");

    private sealed class Host(Func<string, ModelSwitchResult> answer)
    {
        public List<string> Requests { get; } = [];

        public Task<ModelSwitchResult> SwitchAsync(string value, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(value);
            return Task.FromResult(answer(value));
        }
    }

    private static Host Accepting() => new(value => ModelSwitchResult.Switched(value));

    private static ModelPickerModel Picker(Host host, string current = "anthropic/claude-sonnet")
    {
        var picker = new ModelPickerModel(host.SwitchAsync);
        picker.Refresh([Sonnet, Opus, Gpt, Router], current);
        return picker;
    }

    /// <summary>The chip names the provider and the model of the current selection.</summary>
    [Fact]
    public void LabelNamesProviderAndModel()
    {
        var picker = Picker(Accepting());

        picker.Label.ShouldBe("anthropic / Claude Sonnet");
        picker.SelectedChoice.ShouldBe(Sonnet);
        picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
    }

    /// <summary>The provider of a model is the part of its identifier before the first slash, so a router's nested ids group under the router.</summary>
    [Fact]
    public void ProviderIsTheIdentifierPrefix()
    {
        Router.Provider.ShouldBe("openrouter");
        ModelChoice.Create("bare", null).Provider.ShouldBe(ModelChoice.OtherProvider);
        ModelChoice.Create("bare", null).Name.ShouldBe("bare");
    }

    /// <summary>A model the engine runs but does not list shows its raw identifier; with no model known yet the chip reads Model.</summary>
    [Fact]
    public void UnlistedModelShowsItsIdentifier()
    {
        var host = Accepting();
        var picker = new ModelPickerModel(host.SwitchAsync, _ => Task.CompletedTask);
        picker.Label.ShouldBe(ModelPickerModel.PendingLabel);

        picker.Refresh([], "mystery/m1");

        picker.Label.ShouldBe("mystery/m1");
        picker.SelectedChoice.ShouldBeNull();
    }

    /// <summary>An accepted switch changes the current model and the label, announces both, and clears the notice.</summary>
    [Fact]
    public async Task AcceptedSwitchChangesTheModelAndLabel()
    {
        var host = Accepting();
        var picker = Picker(host);
        var changed = new List<string?>();
        picker.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        await picker.SelectAsync(Opus).ConfigureAwait(true);

        host.Requests.ShouldBe(["anthropic/claude-opus"]);
        picker.CurrentValue.ShouldBe("anthropic/claude-opus");
        picker.Label.ShouldBe("anthropic / Claude Opus");
        picker.Notice.ShouldBeNull();
        changed.ShouldContain(nameof(ModelPickerModel.CurrentValue), StringComparer.Ordinal);
        changed.ShouldContain(nameof(ModelPickerModel.Label), StringComparer.Ordinal);
    }

    /// <summary>A switch the engine refuses leaves the model as it was and sets the engine's reason as the notice.</summary>
    [Fact]
    public async Task RefusedSwitchKeepsTheModelAndSetsANotice()
    {
        var host = new Host(_ => ModelSwitchResult.Refused("omp does not offer that model.", "anthropic/claude-sonnet"));
        var picker = Picker(host);

        await picker.SelectAsync(Gpt).ConfigureAwait(true);

        picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
        picker.Label.ShouldBe("anthropic / Claude Sonnet");
        picker.Notice.ShouldBe("omp does not offer that model.");
        picker.HasNotice.ShouldBeTrue();
        picker.IsSwitching.ShouldBeFalse();
    }

    /// <summary>A refusal with no reason still says that the model was not changed.</summary>
    [Fact]
    public async Task RefusalWithoutReasonStillSetsANotice()
    {
        var host = new Host(_ => new ModelSwitchResult(false, null, null));
        var picker = Picker(host);

        await picker.SelectAsync(Opus).ConfigureAwait(true);

        picker.Notice.ShouldBe("The model could not be changed.");
        picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
    }

    /// <summary>A host that throws is reported through the command as a notice, the selection returns to the previous model, and the picker is usable again.</summary>
    [Fact]
    public async Task FailedSwitchRevertsAndRecovers()
    {
        var attempts = 0;
        var picker = new ModelPickerModel((value, _) =>
        {
            attempts++;
            return attempts == 1
                ? throw new InvalidOperationException("the connection was lost")
                : Task.FromResult(ModelSwitchResult.Switched(value));
        });
        picker.Refresh([Sonnet, Opus], "anthropic/claude-sonnet");

        await picker.SelectCommand.ExecuteAsync(Opus).ConfigureAwait(true);

        picker.CurrentValue.ShouldBe("anthropic/claude-sonnet");
        picker.Notice.ShouldBe("The model could not be changed: the connection was lost");
        picker.IsSwitching.ShouldBeFalse();
        picker.IsEnabled.ShouldBeTrue();

        await picker.SelectCommand.ExecuteAsync(Opus).ConfigureAwait(true);

        picker.CurrentValue.ShouldBe("anthropic/claude-opus");
        picker.Notice.ShouldBeNull();
    }

    /// <summary>An accepted switch clears the notice an earlier refusal set.</summary>
    [Fact]
    public async Task AcceptedSwitchClearsTheNotice()
    {
        var accept = false;
        var host = new Host(value => accept ? ModelSwitchResult.Switched(value) : ModelSwitchResult.Refused("not now"));
        var picker = Picker(host);
        await picker.SelectAsync(Opus).ConfigureAwait(true);
        picker.Notice.ShouldNotBeNull();

        accept = true;
        await picker.SelectAsync(Opus).ConfigureAwait(true);

        picker.Notice.ShouldBeNull();
    }

    /// <summary>The model already in use is not sent again, and the identifier comparison is case sensitive.</summary>
    [Fact]
    public async Task CurrentModelIsNotSentAgainAndMatchingIsOrdinal()
    {
        var host = Accepting();
        var picker = Picker(host);

        await picker.SelectAsync(Sonnet).ConfigureAwait(true);
        host.Requests.ShouldBeEmpty();

        await picker.SelectAsync(new ModelChoice("Anthropic/Claude-Sonnet", "Claude Sonnet", "Anthropic")).ConfigureAwait(true);
        host.Requests.ShouldBe(["Anthropic/Claude-Sonnet"]);
    }

    /// <summary>While the engine has not answered, the picker is disabled and reports the switch in flight; it never sends a second request.</summary>
    [Fact]
    public async Task SwitchInFlightDisablesThePicker()
    {
        var gate = new TaskCompletionSource<ModelSwitchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        var picker = new ModelPickerModel(async (_, cancellationToken) =>
        {
            requests++;
            return await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(true);
        });
        picker.Refresh([Sonnet, Opus], "anthropic/claude-sonnet");

        var first = picker.SelectAsync(Opus);

        picker.IsSwitching.ShouldBeTrue();
        picker.IsEnabled.ShouldBeFalse();
        picker.SelectCommand.CanExecute(Opus).ShouldBeFalse();
        await picker.SelectAsync(Opus).ConfigureAwait(true);
        requests.ShouldBe(1);

        gate.SetResult(ModelSwitchResult.Switched("anthropic/claude-opus"));
        await first.ConfigureAwait(true);

        picker.IsSwitching.ShouldBeFalse();
        picker.IsEnabled.ShouldBeTrue();
    }

    /// <summary>A model change the engine made on its own moves the label and the current row.</summary>
    [Fact]
    public void ExternalChangeMovesTheLabel()
    {
        var picker = Picker(Accepting());

        picker.Refresh([Sonnet, Opus, Gpt, Router], "openai/gpt-x");

        picker.Label.ShouldBe("openai / GPT X");
        picker.Rows.Single(static row => row.IsCurrent).Choice.ShouldBe(Gpt);
    }

    /// <summary>A run keeps the model fixed: the picker is disabled while one is active and says why.</summary>
    [Fact]
    public async Task RunActiveDisablesThePicker()
    {
        var host = Accepting();
        var picker = Picker(host);
        picker.IsEnabled.ShouldBeTrue();

        picker.IsRunActive = true;

        picker.IsEnabled.ShouldBeFalse();
        picker.IsVisible.ShouldBeTrue();
        picker.ToolTipText.ShouldBe("The model cannot be changed while a response is running.");
        await picker.SelectAsync(Opus).ConfigureAwait(true);
        host.Requests.ShouldBeEmpty();

        picker.IsRunActive = false;
        picker.IsEnabled.ShouldBeTrue();
    }

    /// <summary>The composer feeds its streaming state to the picker it carries, including a picker attached while streaming.</summary>
    [Fact]
    public void ComposerStreamingStateReachesThePicker()
    {
        var picker = Picker(Accepting());
        var composer = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask) { IsStreaming = true };

        composer.ModelPicker = picker;
        picker.IsRunActive.ShouldBeTrue();

        composer.IsStreaming = false;
        picker.IsRunActive.ShouldBeFalse();
    }

    /// <summary>A terminal session runs its own omp, so the chip is hidden and disabled.</summary>
    [Fact]
    public void TerminalSessionHidesThePicker()
    {
        var picker = Picker(Accepting());

        picker.IsTerminalSession = true;

        picker.IsVisible.ShouldBeFalse();
        picker.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Once the engine reports no model option the chip is hidden; before it reports, the chip shows so that opening it can ask.</summary>
    [Fact]
    public void NoModelOptionHidesThePickerOnceKnown()
    {
        var picker = new ModelPickerModel(Accepting().SwitchAsync, _ => Task.CompletedTask);
        picker.IsVisible.ShouldBeTrue();
        picker.IsEnabled.ShouldBeTrue();

        picker.Refresh([], null);

        picker.IsLoaded.ShouldBeTrue();
        picker.IsVisible.ShouldBeFalse();
        picker.IsEnabled.ShouldBeFalse();
    }

    /// <summary>Opening the chip asks the host to start the session once; the host's report fills the list.</summary>
    [Fact]
    public async Task EnsureLoadedAsksTheHostOnce()
    {
        var loads = 0;
        ModelPickerModel? picker = null;
        picker = new ModelPickerModel(
            Accepting().SwitchAsync,
            _ =>
            {
                loads++;
                picker!.Refresh([Sonnet, Opus], "anthropic/claude-opus");
                return Task.CompletedTask;
            });

        await picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        await picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        loads.ShouldBe(1);
        picker.Label.ShouldBe("anthropic / Claude Opus");
        picker.IsLoading.ShouldBeFalse();
    }

    /// <summary>A failed load sets a notice, leaves the models unknown, and the next attempt asks the host again.</summary>
    [Fact]
    public async Task FailedLoadSetsANoticeAndRetries()
    {
        var loads = 0;
        var picker = new ModelPickerModel(
            Accepting().SwitchAsync,
            _ => ++loads == 1 ? throw new InvalidOperationException("omp did not start") : Task.CompletedTask);

        await picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        picker.Notice.ShouldBe("The models could not be loaded: omp did not start");
        picker.IsLoaded.ShouldBeFalse();
        picker.IsVisible.ShouldBeTrue();

        await picker.EnsureLoadedAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        loads.ShouldBe(2);
        picker.Notice.ShouldBeNull();
    }

    /// <summary>The list groups models under their provider, providers in alphabetical order and models in the engine's order.</summary>
    [Fact]
    public void RowsGroupModelsByProvider()
    {
        var picker = Picker(Accepting());

        picker.Rows.Select(static row => row.IsHeading ? $"# {row.Text}" : row.Text).ShouldBe(
            ["# anthropic", "Claude Sonnet", "Claude Opus", "# openai", "GPT X", "# openrouter", "Claude 3 via router"]);
    }

    /// <summary>The filter keeps the models whose name or identifier contains every word, ignoring case, and drops empty groups.</summary>
    [Fact]
    public void FilterMatchesEveryWordInNameOrIdentifier()
    {
        var picker = Picker(Accepting());

        picker.FilterText = "CLAUDE router";
        picker.Rows.Select(static row => row.Text).ShouldBe(["openrouter", "Claude 3 via router"]);

        picker.FilterText = "anthropic/claude-o";
        picker.Rows.Select(static row => row.Text).ShouldBe(["anthropic", "Claude Opus"]);

        picker.FilterText = "nothing matches";
        picker.Rows.ShouldBeEmpty();
        picker.HasNoMatches.ShouldBeTrue();
        picker.HighlightedRow.ShouldBeNull();

        picker.FilterText = "  ";
        picker.Rows.Count.ShouldBe(7);
        picker.HasNoMatches.ShouldBeFalse();
    }

    /// <summary>The keyboard cursor starts on the model in use, skips headings, stops at the ends, and Enter switches to the highlighted model.</summary>
    [Fact]
    public async Task KeyboardCursorWalksTheModelsAndEnterSwitches()
    {
        var host = Accepting();
        var picker = Picker(host);
        picker.HighlightedRow!.Choice.ShouldBe(Sonnet);
        picker.HighlightedRow.IsHighlighted.ShouldBeTrue();

        picker.MoveHighlight(1);
        picker.HighlightedRow!.Choice.ShouldBe(Opus);
        picker.MoveHighlight(1);
        picker.HighlightedRow!.Choice.ShouldBe(Gpt);
        picker.Rows.Count(static row => row.IsHighlighted).ShouldBe(1);

        picker.MoveHighlight(10);
        picker.HighlightedRow!.Choice.ShouldBe(Router);
        picker.MoveHighlight(-10);
        picker.HighlightedRow!.Choice.ShouldBe(Sonnet);

        picker.MoveHighlight(2);
        await picker.ConfirmHighlightedAsync().ConfigureAwait(true);

        host.Requests.ShouldBe(["openai/gpt-x"]);
        picker.Label.ShouldBe("openai / GPT X");
    }

    /// <summary>Filtering keeps the cursor on its model while that model still matches, and otherwise moves it to the first match.</summary>
    [Fact]
    public void FilterKeepsOrMovesTheCursor()
    {
        var picker = Picker(Accepting());
        picker.MoveHighlight(1);
        picker.HighlightedRow!.Choice.ShouldBe(Opus);

        picker.FilterText = "claude";
        picker.HighlightedRow!.Choice.ShouldBe(Opus);

        picker.FilterText = "gpt";
        picker.HighlightedRow!.Choice.ShouldBe(Gpt);
    }

    /// <summary>The pointer can highlight a model row, but not a heading or a row of an older list.</summary>
    [Fact]
    public void HighlightAcceptsOnlyCurrentModelRows()
    {
        var picker = Picker(Accepting());
        var heading = picker.Rows.First(static row => row.IsHeading);
        var stale = picker.Rows[^1];

        picker.Highlight(heading);
        picker.HighlightedRow!.Choice.ShouldBe(Sonnet);

        picker.Highlight(picker.Rows.Single(static row => row.Choice == Gpt));
        picker.HighlightedRow!.Choice.ShouldBe(Gpt);

        picker.FilterText = "claude";
        picker.Highlight(stale);
        picker.HighlightedRow!.Choice.ShouldNotBe(Router);
        picker.Rows.Count(static row => row.IsHighlighted).ShouldBe(1);
    }

    /// <summary>After the engine session ended, a picker with a loader forgets its models and reads Model again; one without a loader has nothing to offer and hides.</summary>
    [Fact]
    public void ResetForgetsTheModels()
    {
        var withLoader = new ModelPickerModel(Accepting().SwitchAsync, _ => Task.CompletedTask);
        withLoader.Refresh([Sonnet, Opus], "anthropic/claude-sonnet");
        withLoader.FilterText = "opus";

        withLoader.Reset();

        withLoader.Choices.ShouldBeEmpty();
        withLoader.Rows.ShouldBeEmpty();
        withLoader.HighlightedRow.ShouldBeNull();
        withLoader.IsLoaded.ShouldBeFalse();
        withLoader.IsVisible.ShouldBeTrue();
        withLoader.Label.ShouldBe(ModelPickerModel.PendingLabel);

        var withoutLoader = Picker(Accepting());

        withoutLoader.Reset();

        withoutLoader.IsVisible.ShouldBeFalse();
        withoutLoader.IsEnabled.ShouldBeFalse();
    }

    /// <summary>The picker refuses a missing delegate or list.</summary>
    [Fact]
    public void ArgumentsAreValidated()
    {
        Should.Throw<ArgumentNullException>(() => new ModelPickerModel(null!));
        Should.Throw<ArgumentNullException>(() => Picker(Accepting()).Refresh(null!, null));
        Should.Throw<ArgumentException>(() => ModelChoice.Create(" ", "name"));
    }
}
