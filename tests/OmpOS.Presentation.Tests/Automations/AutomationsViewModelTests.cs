using OmpOS.Presentation.Automations;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Automations;

/// <summary>
/// The automations list (32-A): the five states are distinct, a paused automation names its guard and how to resume, a disabled
/// automation shows its toggle off, and enable, disable and resume write to the source and read the list again.
/// </summary>
public sealed class AutomationsViewModelTests
{
    private static readonly DateTimeOffset RecordedAt = new(2026, 10, 26, 8, 30, 0, TimeSpan.Zero);

    private static readonly AutomationEntry Nightly = Automation("nightly", "schedule", enabled: true);
    private static readonly AutomationEntry Manual = Automation("manual-only", "manual", enabled: false);

    /// <summary>A paused automation shows the guard that paused it, why, and the words that say how to resume.</summary>
    [Fact]
    public async Task PausedAutomationShowsGuardReasonAndHowToResume()
    {
        var pause = new AutomationPauseEntry("rate-limit", "5 runs in the last hour", "Run `omp automation resume nightly` once the window has passed");
        var paused = Nightly with
        {
            LastRun = new AutomationLastRun(AutomationOutcome.Refused, "paused by guard 'rate-limit'", RecordedAt),
            Pause = pause,
        };
        var model = new AutomationsViewModel(new ScriptedAutomationsSource(paused, Manual));

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(AutomationsViewState.Degraded);
        model.PausedCount.ShouldBe(1);
        var row = model.Rows.Single(candidate => candidate.IsPaused);
        row.Id.ShouldBe("nightly");
        row.StatusText.ShouldBe("Paused");
        row.PauseText.ShouldBe("Paused by guard rate-limit: 5 runs in the last hour");
        row.ResumeText.ShouldBe("To resume: Run `omp automation resume nightly` once the window has passed");
        row.ResumeName.ShouldBe("Resume automation nightly");
        row.LastOutcome.ShouldBe(AutomationOutcome.Refused);
        row.LastOutcomeText.ShouldBe("Last run: refused at 2026-10-26 08:30:00 UTC (paused by guard 'rate-limit')");

        var other = model.Rows.Single(candidate => !candidate.IsPaused);
        other.PauseText.ShouldBeEmpty();
        other.ResumeText.ShouldBeEmpty();
    }

    /// <summary>A disabled automation reads Disabled and its toggle is off; the toggle names the change it would make.</summary>
    [Fact]
    public async Task DisabledAutomationShowsEnableToggleOff()
    {
        var model = new AutomationsViewModel(new ScriptedAutomationsSource(Nightly, Manual));

        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(AutomationsViewState.Success);
        var disabled = model.Rows.Single(row => string.Equals(row.Id, "manual-only", StringComparison.Ordinal));
        disabled.IsEnabled.ShouldBeFalse();
        disabled.EnabledText.ShouldBe("Disabled");
        disabled.StatusText.ShouldBe("Disabled");
        disabled.ToggleText.ShouldBe("Turn on");
        disabled.ToggleName.ShouldBe("Enable automation manual-only");
        disabled.LastOutcomeText.ShouldBe("Last run: never");
        disabled.MaxRuntimeText.ShouldBe("Maximum runtime: 00:05:00");

        var enabled = model.Rows.Single(row => string.Equals(row.Id, "nightly", StringComparison.Ordinal));
        enabled.IsEnabled.ShouldBeTrue();
        enabled.EnabledText.ShouldBe("Enabled");
        enabled.ToggleText.ShouldBe("Turn off");
        enabled.ToggleName.ShouldBe("Disable automation nightly");
    }

    /// <summary>Loading, empty, error, success and degraded are five different states with their own data.</summary>
    [Fact]
    public async Task LoadingEmptyErrorSuccessDegradedAreDistinct()
    {
        var token = TestContext.Current.CancellationToken;

        var loadingSource = new ScriptedAutomationsSource(true, Nightly);
        var loading = new AutomationsViewModel(loadingSource);
        var pending = loading.LoadAsync(token);
        var loadingState = loading.State;
        loadingState.ShouldBe(AutomationsViewState.Loading);
        loading.IsBusy.ShouldBeTrue();
        loading.Rows.ShouldBeEmpty();
        loadingSource.CompleteLoad();
        await pending.ConfigureAwait(true);
        loading.IsBusy.ShouldBeFalse();
        loading.State.ShouldBe(AutomationsViewState.Success);

        var empty = new AutomationsViewModel(new ScriptedAutomationsSource());
        await empty.LoadAsync(token).ConfigureAwait(true);

        var error = new AutomationsViewModel(new ScriptedAutomationsSource(Nightly) { LoadFailure = "guard store is unreadable" });
        await error.LoadAsync(token).ConfigureAwait(true);

        var success = new AutomationsViewModel(new ScriptedAutomationsSource(Nightly, Manual));
        await success.LoadAsync(token).ConfigureAwait(true);

        var degraded = new AutomationsViewModel(new ScriptedAutomationsSource(
            Nightly,
            Manual with { Pause = new AutomationPauseEntry("loop-guard", "self trigger", "Disable the event trigger, then resume") }));
        await degraded.LoadAsync(token).ConfigureAwait(true);

        new[] { loadingState, empty.State, error.State, success.State, degraded.State }.Distinct().Count().ShouldBe(5);
        empty.State.ShouldBe(AutomationsViewState.Empty);
        empty.Rows.ShouldBeEmpty();
        empty.LoadFailure.ShouldBeEmpty();

        error.State.ShouldBe(AutomationsViewState.Error);
        error.LoadFailure.ShouldBe("guard store is unreadable");
        error.Rows.ShouldBeEmpty();

        success.PausedCount.ShouldBe(0);
        success.Rows.ShouldAllBe(row => !row.IsPaused && row.PauseText.Length == 0);

        degraded.PausedCount.ShouldBe(1);
        degraded.Rows.Single(row => row.IsPaused).Id.ShouldBe("manual-only");
    }

    /// <summary>Toggling writes the opposite enabled state to the source and the row follows; a failed write shows the reason and keeps the list.</summary>
    [Fact]
    public async Task ToggleWritesTheOppositeStateAndAFailedWriteIsShown()
    {
        var source = new ScriptedAutomationsSource(Nightly, Manual);
        var model = new AutomationsViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        await model.ToggleAsync("manual-only", TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.Entry("manual-only").IsEnabled.ShouldBeTrue();
        model.Rows.Single(row => string.Equals(row.Id, "manual-only", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue();

        await model.ToggleAsync("nightly", TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.Entry("nightly").IsEnabled.ShouldBeFalse();
        model.Rows.Single(row => string.Equals(row.Id, "nightly", StringComparison.Ordinal)).ToggleText.ShouldBe("Turn on");
        source.WriteCount.ShouldBe(2);

        source.WriteFailure = "guard store is read-only";
        await model.ToggleAsync("nightly", TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.ActionFailure.ShouldBe("guard store is read-only");
        model.Rows.Single(row => string.Equals(row.Id, "nightly", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();
        source.WriteCount.ShouldBe(2);

        await model.ToggleAsync("unknown", TestContext.Current.CancellationToken).ConfigureAwait(true);
        source.WriteCount.ShouldBe(2);
    }

    /// <summary>Resume clears the pause at the source, returns the list to success, and neither enables the automation nor changes its outcome.</summary>
    [Fact]
    public async Task ResumeClearsThePauseWithoutEnablingTheAutomation()
    {
        var paused = Manual with { Pause = new AutomationPauseEntry("rate-limit", "too many runs", "Wait for the window to pass") };
        var source = new ScriptedAutomationsSource(paused);
        var model = new AutomationsViewModel(source);
        await model.LoadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        model.State.ShouldBe(AutomationsViewState.Degraded);

        await model.ResumeAsync("manual-only", TestContext.Current.CancellationToken).ConfigureAwait(true);

        model.State.ShouldBe(AutomationsViewState.Success);
        model.PausedCount.ShouldBe(0);
        var row = model.Rows.Single();
        row.IsPaused.ShouldBeFalse();
        row.IsEnabled.ShouldBeFalse();
        row.StatusText.ShouldBe("Disabled");
        source.WriteCount.ShouldBe(1);
    }

    private static AutomationEntry Automation(string id, string trigger, bool enabled) =>
        new(id, trigger, enabled, TimeSpan.FromMinutes(5), LastRun: null, Pause: null);
}
