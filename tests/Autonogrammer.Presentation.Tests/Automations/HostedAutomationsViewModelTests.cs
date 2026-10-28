using Autonogrammer.Presentation.Automations;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Automations;

/// <summary>
/// The hosted automations page: the five states are distinct, toggle, run now and delete (after a confirmation) write to the manager and
/// read the list again, the run history follows the selection, expected failures become text and never throw, and cancelling leaves the page
/// as it was.
/// </summary>
public sealed class HostedAutomationsViewModelTests
{
    private static readonly HostedAutomationEntry Nightly = AutomationFixtures.Entry("nightly", AutomationFixtures.Draft());

    private static readonly HostedAutomationEntry Paused = AutomationFixtures.Entry(
        "paused",
        AutomationFixtures.Draft() with { Name = "Weekly digest", Enabled = false });

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Loading shows until the list arrives, then the page is ready with one row per automation and is not busy.</summary>
    [Fact]
    public async Task LoadShowsLoadingThenTheRows()
    {
        var manager = new FakeAutomationsManager(Nightly, Paused);
        manager.Hold("List");
        var model = new HostedAutomationsViewModel(manager);
        model.State.ShouldBe(HostedAutomationsViewState.Loading);

        var pending = model.LoadAsync(Token);
        model.State.ShouldBe(HostedAutomationsViewState.Loading);
        model.IsBusy.ShouldBeTrue();
        model.Rows.ShouldBeEmpty();

        manager.Release("List");
        await pending.ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Ready);
        model.IsBusy.ShouldBeFalse();
        model.LoadFailure.ShouldBeEmpty();
        model.Rows.Select(row => row.Name).ShouldBe(["Nightly triage", "Weekly digest"]);
        model.Rows[1].ToggleText.ShouldBe(AutomationsCopy.TurnOnLabel);
        model.SelectedRow.ShouldBeNull();
        model.History.ShouldBeEmpty();
    }

    /// <summary>No automation is the empty state, not an error.</summary>
    [Fact]
    public async Task NoAutomationsIsTheEmptyState()
    {
        var model = new HostedAutomationsViewModel(new FakeAutomationsManager());

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Empty);
        model.Rows.ShouldBeEmpty();
        model.LoadFailure.ShouldBeEmpty();
    }

    /// <summary>A service that has automations switched off is the unavailable state, with no failure text and no row.</summary>
    [Fact]
    public async Task ServiceWithAutomationsSwitchedOffIsUnavailable()
    {
        var manager = new FakeAutomationsManager(Nightly);
        manager.Fail("List", new AutomationsUnavailableException("Automations:Enabled is false."));
        var model = new HostedAutomationsViewModel(manager);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Unavailable);
        model.Rows.ShouldBeEmpty();
        model.LoadFailure.ShouldBeEmpty();
        model.IsBusy.ShouldBeFalse();
    }

    /// <summary>A failed load is the error state with the reason, and a retry that works replaces it with the list.</summary>
    [Fact]
    public async Task FailedLoadShowsTheReasonAndARetryRecovers()
    {
        var manager = new FakeAutomationsManager(Nightly);
        manager.Fail("List", new AutomationsSourceException("The service did not answer."));
        var model = new HostedAutomationsViewModel(manager);

        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Error);
        model.LoadFailure.ShouldBe("The service did not answer.");
        model.Rows.ShouldBeEmpty();

        manager.Fail("List", null);
        await model.LoadAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Ready);
        model.LoadFailure.ShouldBeEmpty();
        model.Rows.Count.ShouldBe(1);
    }

    /// <summary>The five states are five different values.</summary>
    [Fact]
    public async Task EveryStateIsDistinct()
    {
        var loading = new HostedAutomationsViewModel(new FakeAutomationsManager());

        var unavailableManager = new FakeAutomationsManager();
        unavailableManager.Fail("List", new AutomationsUnavailableException());
        var unavailable = new HostedAutomationsViewModel(unavailableManager);
        await unavailable.LoadAsync(Token).ConfigureAwait(true);

        var empty = new HostedAutomationsViewModel(new FakeAutomationsManager());
        await empty.LoadAsync(Token).ConfigureAwait(true);

        var errorManager = new FakeAutomationsManager();
        errorManager.Fail("List", new AutomationsSourceException("down"));
        var error = new HostedAutomationsViewModel(errorManager);
        await error.LoadAsync(Token).ConfigureAwait(true);

        var ready = new HostedAutomationsViewModel(new FakeAutomationsManager(Nightly));
        await ready.LoadAsync(Token).ConfigureAwait(true);

        new[] { loading.State, unavailable.State, empty.State, error.State, ready.State }.Distinct().Count().ShouldBe(5);
    }

    /// <summary>Cancelling a reload throws and puts the page back as it was: the list stays on screen and nothing is busy.</summary>
    [Fact]
    public async Task CancellingAReloadLeavesThePageAsItWas()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Hold("List");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var pending = model.LoadAsync(source.Token);
        model.State.ShouldBe(HostedAutomationsViewState.Loading);
        await source.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(Token)).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Ready);
        model.Rows.Count.ShouldBe(1);
        model.IsBusy.ShouldBeFalse();
        model.LoadFailure.ShouldBeEmpty();
    }

    /// <summary>A manager exception that is not an expected failure is a defect: it escapes and does not leave the page busy.</summary>
    [Fact]
    public async Task UnexpectedManagerExceptionEscapes()
    {
        var manager = new FakeAutomationsManager(Nightly);
        manager.Fail("List", new InvalidOperationException("defect"));
        var model = new HostedAutomationsViewModel(manager);

        await Should.ThrowAsync<InvalidOperationException>(() => model.LoadAsync(Token)).ConfigureAwait(true);

        model.IsBusy.ShouldBeFalse();
    }

    /// <summary>Selecting an automation loads its history, newest first, as rows; an unknown automation is ignored.</summary>
    [Fact]
    public async Task SelectingAnAutomationLoadsItsHistory()
    {
        var manager = new FakeAutomationsManager(Nightly, Paused);
        manager.SetHistory(
            "nightly",
            AutomationFixtures.Run("run-2", HostedRunStatus.Failed, error: "clone failed", duration: TimeSpan.FromSeconds(125)),
            AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded, "webhook", "Labelled 3 issues"));
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);

        await model.SelectAsync("nightly", Token).ConfigureAwait(true);

        model.SelectedId.ShouldBe("nightly");
        model.SelectedRow.ShouldNotBeNull().Name.ShouldBe("Nightly triage");
        model.History.Select(run => run.RunId).ShouldBe(["run-2", "run-1"]);
        model.History[0].StatusText.ShouldBe("Failed");
        model.History[0].DurationText.ShouldBe("took 2 min 5 s");
        model.History[0].ErrorText.ShouldBe("clone failed");
        model.History[1].SummaryText.ShouldBe("Labelled 3 issues");

        var calls = manager.Calls.Count;
        await model.SelectAsync("unknown", Token).ConfigureAwait(true);
        manager.Calls.Count.ShouldBe(calls);
        model.SelectedId.ShouldBe("nightly");
    }

    /// <summary>A history that cannot be read shows the reason as an action failure and an empty history, and the selection stays.</summary>
    [Fact]
    public async Task FailedHistoryShowsTheReason()
    {
        var manager = new FakeAutomationsManager(Nightly);
        manager.Fail("History", new AutomationsSourceException("The run log is unreadable."));
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);

        await model.SelectAsync("nightly", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("The run log is unreadable.");
        model.History.ShouldBeEmpty();
        model.SelectedId.ShouldBe("nightly");
    }

    /// <summary>Toggle writes the opposite state and reads the list again, so the row shows the new toggle.</summary>
    [Fact]
    public async Task ToggleWritesTheOppositeStateAndReloads()
    {
        var manager = new FakeAutomationsManager(Nightly, Paused);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        var listCalls = manager.CountOf("List");

        await model.ToggleAsync("paused", Token).ConfigureAwait(true);
        await model.ToggleAsync("nightly", Token).ConfigureAwait(true);

        manager.Calls.ShouldContain("SetEnabled:paused:on", StringComparer.Ordinal);
        manager.Calls.ShouldContain("SetEnabled:nightly:off", StringComparer.Ordinal);
        manager.CountOf("List").ShouldBe(listCalls + 2);
        RowOf(model, "paused").IsEnabled.ShouldBeTrue();
        RowOf(model, "nightly").ToggleText.ShouldBe(AutomationsCopy.TurnOnLabel);
        model.ActionFailure.ShouldBeEmpty();
        model.IsBusy.ShouldBeFalse();
    }

    /// <summary>A failed toggle shows the reason, changes nothing and does not read the list again; an unknown automation makes no call.</summary>
    [Fact]
    public async Task FailedToggleShowsTheReasonAndKeepsTheRows()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Fail("SetEnabled", new AutomationsSourceException("The change was refused."));
        var listCalls = manager.CountOf("List");

        await model.ToggleAsync("nightly", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("The change was refused.");
        model.Rows.Single().IsEnabled.ShouldBeTrue();
        manager.CountOf("List").ShouldBe(listCalls);

        await model.ToggleAsync("unknown", Token).ConfigureAwait(true);
        manager.CountOf("SetEnabled").ShouldBe(1);
    }

    /// <summary>When the toggle worked but the list cannot be read again, the failure is reported and the old rows stay on screen.</summary>
    [Fact]
    public async Task ReloadFailureAfterAChangeKeepsTheRowsAndReportsTheReason()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Fail("List", new AutomationsSourceException("The list is unreachable."));

        await model.ToggleAsync("nightly", Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Ready);
        model.ActionFailure.ShouldBe("The list is unreachable.");
        model.Rows.Count.ShouldBe(1);
    }

    /// <summary>
    /// Run now asks the manager for a run, announces its status with the automation name, reads the list again and refreshes the history of the
    /// selected automation.
    /// </summary>
    [Fact]
    public async Task RunNowAnnouncesTheRunAndRefreshesTheHistory()
    {
        var manager = new FakeAutomationsManager(Nightly);
        manager.NextRun = AutomationFixtures.Run("run-manual", HostedRunStatus.Launched, "manual", externalId: "agent-7");
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        await model.SelectAsync("nightly", Token).ConfigureAwait(true);
        model.History.ShouldBeEmpty();

        await model.RunNowAsync("nightly", Token).ConfigureAwait(true);

        manager.Calls.ShouldContain("RunNow:nightly", StringComparer.Ordinal);
        model.Notice.ShouldBe(AutomationsCopy.RunNotice("Nightly triage", HostedRunStatus.Launched));
        model.Notice.ShouldContain("Nightly triage", Case.Sensitive);
        model.History.Single().RunId.ShouldBe("run-manual");
        model.History.Single().AgentText.ShouldBe("Agent: agent-7");
        model.Rows.Single().LastResultText.ShouldStartWith("Last run: launched at", Case.Sensitive);
        model.ActionFailure.ShouldBeEmpty();
    }

    /// <summary>A run now that is refused shows the reason, announces nothing and does not read the list again.</summary>
    [Fact]
    public async Task RefusedRunNowShowsTheReason()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Fail("RunNow", new AutomationsSourceException("The hourly limit is reached."));
        var listCalls = manager.CountOf("List");

        await model.RunNowAsync("nightly", Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("The hourly limit is reached.");
        model.Notice.ShouldBeEmpty();
        manager.CountOf("List").ShouldBe(listCalls);
    }

    /// <summary>Cancelling an action throws and leaves the page not busy and without a failure.</summary>
    [Fact]
    public async Task CancellingAnActionLeavesThePageAsItWas()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Hold("SetEnabled");
        using var source = CancellationTokenSource.CreateLinkedTokenSource(Token);

        var pending = model.ToggleAsync("nightly", source.Token);
        model.IsBusy.ShouldBeTrue();
        await source.CancelAsync().ConfigureAwait(true);
        await Should.ThrowAsync<OperationCanceledException>(() => pending.WaitAsync(Token)).ConfigureAwait(true);

        model.IsBusy.ShouldBeFalse();
        model.ActionFailure.ShouldBeEmpty();
        model.Rows.Single().IsEnabled.ShouldBeTrue();
    }

    /// <summary>Delete first asks: nothing is deleted while the confirmation is open, and cancelling closes it without a call.</summary>
    [Fact]
    public async Task DeleteNeedsAConfirmationAndCancelDeletesNothing()
    {
        var manager = new FakeAutomationsManager(Nightly, Paused);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);

        model.RequestDelete("nightly");

        model.IsConfirmingDelete.ShouldBeTrue();
        model.PendingDeleteId.ShouldBe("nightly");
        model.PendingDeleteText.ShouldBe(AutomationsCopy.DeleteWarning("Nightly triage"));
        manager.CountOf("Delete").ShouldBe(0);

        model.CancelDelete();

        model.IsConfirmingDelete.ShouldBeFalse();
        model.PendingDeleteText.ShouldBeEmpty();
        await model.ConfirmDeleteAsync(Token).ConfigureAwait(true);
        manager.CountOf("Delete").ShouldBe(0);
        model.Rows.Count.ShouldBe(2);

        model.RequestDelete("unknown");
        model.IsConfirmingDelete.ShouldBeFalse();
    }

    /// <summary>Confirming deletes the automation, reads the list again, clears the selection and announces it.</summary>
    [Fact]
    public async Task ConfirmedDeleteRemovesTheAutomationAndClearsTheSelection()
    {
        var manager = new FakeAutomationsManager(Nightly, Paused);
        manager.SetHistory("nightly", AutomationFixtures.Run("run-1", HostedRunStatus.Succeeded));
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        await model.SelectAsync("nightly", Token).ConfigureAwait(true);
        model.RequestDelete("nightly");

        await model.ConfirmDeleteAsync(Token).ConfigureAwait(true);

        manager.Calls.ShouldContain("Delete:nightly", StringComparer.Ordinal);
        model.Rows.Select(row => row.Id).ShouldBe(["paused"]);
        model.SelectedId.ShouldBeEmpty();
        model.History.ShouldBeEmpty();
        model.IsConfirmingDelete.ShouldBeFalse();
        model.Notice.ShouldBe(AutomationsCopy.DeletedNotice("Nightly triage"));
        model.State.ShouldBe(HostedAutomationsViewState.Ready);
    }

    /// <summary>Deleting the last automation moves the page to the empty state.</summary>
    [Fact]
    public async Task DeletingTheLastAutomationShowsTheEmptyState()
    {
        var model = new HostedAutomationsViewModel(new FakeAutomationsManager(Nightly));
        await model.LoadAsync(Token).ConfigureAwait(true);
        model.RequestDelete("nightly");

        await model.ConfirmDeleteAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Empty);
        model.Rows.ShouldBeEmpty();
    }

    /// <summary>A refused delete keeps the row, shows the reason and closes the confirmation.</summary>
    [Fact]
    public async Task RefusedDeleteKeepsTheRowAndShowsTheReason()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        manager.Fail("Delete", new AutomationsSourceException("A run is still in progress."));
        model.RequestDelete("nightly");

        await model.ConfirmDeleteAsync(Token).ConfigureAwait(true);

        model.ActionFailure.ShouldBe("A run is still in progress.");
        model.Notice.ShouldBeEmpty();
        model.Rows.Count.ShouldBe(1);
        model.IsConfirmingDelete.ShouldBeFalse();
    }

    /// <summary>The editor opens on a new automation or on an existing one, and closing it drops it; an unknown automation opens nothing.</summary>
    [Fact]
    public async Task EditorOpensOnNewAndExistingAutomationsAndCloses()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);

        model.BeginCreate();
        model.Editor.ShouldNotBeNull().IsEdit.ShouldBeFalse();

        model.BeginEdit("nightly");
        var editor = model.Editor.ShouldNotBeNull();
        editor.IsEdit.ShouldBeTrue();
        editor.Name.ShouldBe("Nightly triage");

        model.CloseEditor();
        model.Editor.ShouldBeNull();

        model.BeginEdit("unknown");
        model.Editor.ShouldBeNull();
    }

    /// <summary>Saving a new automation reads the list again, selects the saved row and closes the editor.</summary>
    [Fact]
    public async Task SavingFromTheEditorReloadsSelectsTheRowAndClosesTheEditor()
    {
        var manager = new FakeAutomationsManager();
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        model.State.ShouldBe(HostedAutomationsViewState.Empty);
        model.BeginCreate();
        var editor = model.Editor.ShouldNotBeNull();
        editor.Name = "Release notes";
        editor.Prompt = "Draft the release notes.";
        editor.CronText = "0 8 * * 1";
        editor.TargetKind = "local";

        await model.SaveEditorAsync(Token).ConfigureAwait(true);

        model.State.ShouldBe(HostedAutomationsViewState.Ready);
        model.Rows.Single().Name.ShouldBe("Release notes");
        model.SelectedRow.ShouldNotBeNull().Name.ShouldBe("Release notes");
        model.Editor.ShouldBeNull();
        model.Notice.ShouldBe(AutomationsCopy.SavedNotice("Release notes"));
        manager.Saves.Single().Id.ShouldBeNull();
    }

    /// <summary>
    /// A save that generated a webhook secret keeps the editor open, showing the secret once, until the user confirms it is stored; then the
    /// editor closes and the secret is nowhere on the page.
    /// </summary>
    [Fact]
    public async Task EditorStaysOpenWhileTheSecretIsShownThenClosesOnceStored()
    {
        const string canary = "whsec-canary-91c2";
        var manager = new FakeAutomationsManager { SecretOnSave = canary };
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        model.BeginCreate();
        var editor = model.Editor.ShouldNotBeNull();
        editor.Name = "Push handler";
        editor.Prompt = "Review the push.";
        editor.WebhookEnabled = true;
        editor.WebhookFlavour = "github";
        editor.WebhookEventsText = "push";
        editor.TargetKind = "local";

        await model.SaveEditorAsync(Token).ConfigureAwait(true);

        model.Editor.ShouldBeSameAs(editor);
        editor.RevealedSecret.ShouldBe(canary);
        model.Rows.Single().WebhookText.ShouldContain("/webhooks/", Case.Sensitive);
        EverythingTheListShows(model).ShouldNotContain(canary, Case.Sensitive);

        model.AcknowledgeEditorSecret();

        model.Editor.ShouldBeNull();
        editor.RevealedSecret.ShouldBeEmpty();
        EverythingTheListShows(model).ShouldNotContain(canary, Case.Sensitive);
    }

    /// <summary>An invalid editor makes no manager call from the page either; the editor stays open and shows its messages.</summary>
    [Fact]
    public async Task InvalidEditorSavesNothingAndStaysOpen()
    {
        var manager = new FakeAutomationsManager();
        var model = new HostedAutomationsViewModel(manager);
        await model.LoadAsync(Token).ConfigureAwait(true);
        model.BeginCreate();

        await model.SaveEditorAsync(Token).ConfigureAwait(true);

        manager.CountOf("Save").ShouldBe(0);
        model.Editor.ShouldNotBeNull().Errors.Count.ShouldBe(3);
    }

    /// <summary>The page raises its one change event after each step and the last one already shows the finished state.</summary>
    [Fact]
    public async Task ChangedIsRaisedAndTheLastEventShowsTheFinishedState()
    {
        var manager = new FakeAutomationsManager(Nightly);
        var model = new HostedAutomationsViewModel(manager);
        var busyAtEnd = true;
        var events = 0;
        model.Changed += (_, _) =>
        {
            events++;
            busyAtEnd = model.IsBusy;
        };

        await model.LoadAsync(Token).ConfigureAwait(true);

        events.ShouldBeGreaterThan(0);
        busyAtEnd.ShouldBeFalse();
        model.State.ShouldBe(HostedAutomationsViewState.Ready);
    }

    private static HostedAutomationRowModel RowOf(HostedAutomationsViewModel model, string id) =>
        model.Rows.Single(row => string.Equals(row.Id, id, StringComparison.Ordinal));

    private static string EverythingTheListShows(HostedAutomationsViewModel model) =>
        string.Join(
            '\n',
            model.Rows.SelectMany(row => new[]
            {
                row.Name,
                row.TriggerText,
                row.TargetText,
                row.NextRunText,
                row.LastResultText,
                row.WebhookPath,
                row.WebhookText,
                row.ToggleName,
                row.AccessibleName,
            }).Append(model.Notice).Append(model.ActionFailure).Append(model.LoadFailure).Append(model.PendingDeleteText));
}
