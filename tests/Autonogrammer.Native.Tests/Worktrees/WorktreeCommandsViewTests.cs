using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Views.Worktrees;
using Autonogrammer.Presentation.Worktrees;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Worktrees;

/// <summary>The worktree runs panel: the rows with their commands, the patch preview, the apply outcome, the two-step delete, the best-of-n section and the keyboard and accessibility contract.</summary>
public sealed class WorktreeCommandsViewTests
{
    private static readonly string[] WorktreeNames = ["alpha", "beta"];

    private const string RefreshName = "Refresh the worktree list";
    private const string CloseName = "Close the worktree panel";
    private const string NewNameName = "Name of the new worktree";
    private const string PromptName = "Task for every candidate";
    private const string CountName = "Number of candidates";
    private const string RunCandidatesName = "Run the task in the chosen number of worktrees";
    private const string ConfirmDeleteName = "Delete the worktrees named in the question";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string MergeName(string runId) => "Merge the changes of worktree " + runId + " three ways";

    private static string KeepName(int index) => "Keep candidate " + index + " and discard the others";

    private static async Task RunAsync(ViewWorktreeBackend backend, IBestOfNHost? host, Func<WorktreeCommandsModel, WorktreeCommandsView, Window, Task> body)
    {
        var session = TestAppBuilder.StartSession();
        await using var sessionScope = session.ConfigureAwait(true);
        await session.RunAsync(async () =>
        {
            var model = new WorktreeCommandsModel(backend, new ManualTimeProvider(), host);
            var view = new WorktreeCommandsView { DataContext = model };
            var window = new Window { Width = 900, Height = 2600, Content = view };
            window.Show();
            SettingsTree.SettleSafe(window);
            try
            {
                await body(model, view, window).ConfigureAwait(true);
            }
            finally
            {
                window.Close();
            }
        }, Token).ConfigureAwait(true);
    }

    private static void Click(Window window, WorktreeCommandsView view, string name) => SettingsTree.Click(window, SettingsTree.Named<Button>(view, name));

    /// <summary>The panel reads the runs when it gets its model and draws one row per run with its branch and change count and four named buttons.</summary>
    [Fact]
    public async Task RowsShowTheRunsAndTheirNamedButtons()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 2);
        backend.AddRun("beta", 1);
        await RunAsync(backend, null, (model, view, window) =>
        {
            view.Runs.ItemCount.ShouldBe(2);
            view.Empty.IsVisible.ShouldBeFalse();
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain("alpha on autonogrammer/run/alpha, 2 files changed", StringComparer.Ordinal);
            texts.ShouldContain("beta on autonogrammer/run/beta, 1 file changed", StringComparer.Ordinal);
            foreach (var run in WorktreeNames)
            {
                SettingsTree.Has<Button>(view, WorktreeCommandsCopy.PreviewName(run)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, WorktreeCommandsCopy.ApplyName(run)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, MergeName(run)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, WorktreeCommandsCopy.DeleteName(run)).ShouldBeTrue();
            }

            view.PreviewPanel.IsVisible.ShouldBeFalse();
            view.ApplyOutcome.IsVisible.ShouldBeFalse();
            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            SettingsTree.Unnamed(window).ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>A repository without runs says so and draws no row.</summary>
    [Fact]
    public async Task AnEmptyRepositoryShowsTheEmptySentence()
    {
        await RunAsync(new ViewWorktreeBackend(), null, (model, view, window) =>
        {
            view.Runs.ItemCount.ShouldBe(0);
            view.Empty.IsVisible.ShouldBeTrue();
            view.Empty.Text.ShouldBe(WorktreeCommandsCopy.Empty);
            SettingsTree.Unnamed(window).ShouldBeEmpty();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Refresh reads the runs again.</summary>
    [Fact]
    public async Task RefreshReadsTheRunsAgain()
    {
        var backend = new ViewWorktreeBackend();
        await RunAsync(backend, null, (model, view, window) =>
        {
            view.Runs.ItemCount.ShouldBe(0);
            backend.AddRun("gamma", 4);

            Click(window, view, RefreshName);

            view.Runs.ItemCount.ShouldBe(1);
            view.Empty.IsVisible.ShouldBeFalse();
            SettingsTree.Texts(view).ShouldContain("gamma on autonogrammer/run/gamma, 4 files changed", StringComparer.Ordinal);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Review changes shows the files of the patch with their added and removed counts, marks a binary file, and announces the summary politely.</summary>
    [Fact]
    public async Task PreviewShowsTheFilesAndCounts()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 2);
        await RunAsync(backend, null, (model, view, window) =>
        {
            Click(window, view, WorktreeCommandsCopy.PreviewName("alpha"));

            backend.Calls.ShouldBe(["preview alpha"]);
            view.PreviewPanel.IsVisible.ShouldBeTrue();
            view.PreviewSummary.Text.ShouldBe(WorktreeCommandsCopy.PatchSummary(model.Preview!));
            view.PreviewFiles.ItemCount.ShouldBe(2);
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain("src/a.txt", StringComparer.Ordinal);
            texts.ShouldContain("+3", StringComparer.Ordinal);
            texts.ShouldContain("-1", StringComparer.Ordinal);
            texts.ShouldContain("logo.png", StringComparer.Ordinal);
            texts.ShouldContain("binary", StringComparer.Ordinal);
            view.Status.Text.ShouldBe(view.PreviewSummary.Text);
            AutomationProperties.GetLiveSetting(view.Status).ShouldBe(AutomationLiveSetting.Polite);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Apply asks the backend for a clean apply and the outcome sentence is announced politely.</summary>
    [Fact]
    public async Task ApplyRunsACleanApplyAndAnnouncesTheResult()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 2);
        await RunAsync(backend, null, (model, view, window) =>
        {
            Click(window, view, WorktreeCommandsCopy.ApplyName("alpha"));

            backend.Calls.ShouldBe(["apply alpha Clean"]);
            view.Status.Text.ShouldBe("Applied.");
            view.Status.Classes.Contains("tone-error").ShouldBeFalse();
            AutomationProperties.GetLiveSetting(view.Status).ShouldBe(AutomationLiveSetting.Polite);
            view.ApplyOutcome.IsVisible.ShouldBeTrue();
            view.ApplyOutcomeLine.Text.ShouldBe("Applied.");
            view.Conflicts.IsVisible.ShouldBeFalse();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Merge three ways asks for a three-way merge; conflicting files are listed and the status becomes an assertive error.</summary>
    [Fact]
    public async Task MergeThreeWaysListsTheConflictsAndAnnouncesThemAtOnce()
    {
        var backend = new ViewWorktreeBackend
        {
            Outcome = new WorktreeApplyOutcome(WorktreeApplyState.Conflicts, ["a.cs", "b.cs"], "2 files conflict."),
        };
        backend.AddRun("alpha", 2);
        await RunAsync(backend, null, (model, view, window) =>
        {
            Click(window, view, MergeName("alpha"));

            backend.Calls.ShouldBe(["apply alpha ThreeWay"]);
            view.ApplyOutcome.IsVisible.ShouldBeTrue();
            view.ApplyOutcomeLine.Text.ShouldBe("2 files conflict.");
            view.Conflicts.IsVisible.ShouldBeTrue();
            view.Conflicts.ItemCount.ShouldBe(2);
            SettingsTree.Texts(view).ShouldContain("a.cs", StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldContain("b.cs", StringComparer.Ordinal);
            view.Status.Text.ShouldBe("2 files conflict.");
            view.Status.Classes.Contains("tone-error").ShouldBeTrue();
            AutomationProperties.GetLiveSetting(view.Status).ShouldBe(AutomationLiveSetting.Assertive);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Delete only asks: it opens a confirmation with focus on Cancel and deletes nothing; Escape dismisses it and focus returns to the toolbar.</summary>
    [Fact]
    public async Task DeleteAsksFirstAndEscapeDismissesIt()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 2);
        await RunAsync(backend, null, (model, view, window) =>
        {
            Click(window, view, WorktreeCommandsCopy.DeleteName("alpha"));

            backend.Calls.ShouldBeEmpty();
            view.DeleteConfirmation.IsVisible.ShouldBeTrue();
            view.DeleteWarning.Text.ShouldBe(model.DeleteConfirmationText);
            view.DeleteWarning.Text.ShouldNotBeNull().ShouldContain("alpha", Case.Sensitive);
            SettingsTree.FocusedName(window).ShouldBe(WorktreeCommandsCopy.CancelLabel);

            SettingsTree.Press(window, Key.Escape, PhysicalKey.Escape);

            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            backend.Calls.ShouldBeEmpty();
            model.IsConfirmingDelete.ShouldBeFalse();
            view.Status.Text.ShouldBe(WorktreeCommandsCopy.DeleteCancelled);
            SettingsTree.FocusedName(window).ShouldBe(RefreshName);
            view.Runs.ItemCount.ShouldBe(1);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>The Cancel button dismisses the confirmation, and Confirm deletes the run, reloads the rows and announces the result.</summary>
    [Fact]
    public async Task ConfirmDeletesTheRunAndCancelKeepsIt()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 2);
        backend.AddRun("beta", 1);
        await RunAsync(backend, null, (model, view, window) =>
        {
            Click(window, view, WorktreeCommandsCopy.DeleteName("alpha"));
            Click(window, view, WorktreeCommandsCopy.CancelLabel);
            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            backend.Calls.ShouldBeEmpty();
            view.Runs.ItemCount.ShouldBe(2);

            Click(window, view, WorktreeCommandsCopy.DeleteName("beta"));
            view.DeleteWarning.Text.ShouldNotBeNull().ShouldContain("beta", Case.Sensitive);
            view.DeleteWarning.Text.ShouldNotContain("alpha", Case.Sensitive);
            Click(window, view, ConfirmDeleteName);

            backend.Calls.ShouldBe(["delete beta"]);
            view.DeleteConfirmation.IsVisible.ShouldBeFalse();
            view.Runs.ItemCount.ShouldBe(1);
            view.Status.Text.ShouldBe(WorktreeCommandsCopy.Deleted(1));
            SettingsTree.Texts(view).ShouldNotContain("beta on autonogrammer/run/beta, 1 file changed", StringComparer.Ordinal);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>New worktree starts a run with the typed name made safe, clears the name box and lists the run; Enter in the name box does the same.</summary>
    [Fact]
    public async Task NewWorktreeStartsARunFromTheButtonAndFromEnter()
    {
        var backend = new ViewWorktreeBackend();
        await RunAsync(backend, null, (model, view, window) =>
        {
            view.NewName.Text = "Fix Bug";
            Click(window, view, WorktreeCommandsCopy.StartLabel);

            backend.Calls.ShouldBe(["start Fix-Bug"]);
            string.IsNullOrEmpty(view.NewName.Text).ShouldBeTrue();
            view.Runs.ItemCount.ShouldBe(1);
            view.Status.Text.ShouldBe(WorktreeCommandsCopy.Started(model.Rows[0].Run, string.Empty));

            view.NewName.Text = "second";
            _ = view.NewName.Focus();
            SettingsTree.Press(window, Key.Enter, PhysicalKey.Enter);

            backend.Calls.ShouldBe(["start Fix-Bug", "start second"]);
            view.Runs.ItemCount.ShouldBe(2);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Run candidates makes the chosen number of worktrees and lists each candidate with its state, then announces how many finished.</summary>
    [Fact]
    public async Task BestOfNRunsTheChosenNumberOfCandidates()
    {
        var backend = new ViewWorktreeBackend();
        var host = new ViewBestOfNHost();
        await RunAsync(backend, host, (model, view, window) =>
        {
            view.BestOfCount.SelectedItem.ShouldBe(3);
            view.Candidates.IsVisible.ShouldBeFalse();
            view.BestOfCount.SelectedIndex = 1;
            view.BestOfPrompt.Text = "fix the bug";

            Click(window, view, RunCandidatesName);

            host.Requests.Count.ShouldBe(2);
            host.Requests.All(static request => string.Equals(request.Prompt, "fix the bug", StringComparison.Ordinal)).ShouldBeTrue();
            view.Candidates.IsVisible.ShouldBeTrue();
            view.Candidates.ItemCount.ShouldBe(2);
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain(WorktreeCommandsCopy.CandidateRow(1, BestOfNState.Finished, 0, "done 1"), StringComparer.Ordinal);
            texts.ShouldContain(WorktreeCommandsCopy.CandidateRow(2, BestOfNState.Finished, 0, "done 2"), StringComparer.Ordinal);
            view.Status.Text.ShouldBe(WorktreeCommandsCopy.BestOfDone(2, 2));
            view.Runs.ItemCount.ShouldBe(2);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Enter in the task box runs the candidates with the default count.</summary>
    [Fact]
    public async Task EnterInTheTaskBoxRunsTheCandidates()
    {
        var host = new ViewBestOfNHost();
        await RunAsync(new ViewWorktreeBackend(), host, (model, view, window) =>
        {
            view.BestOfPrompt.Text = "add a test";
            _ = view.BestOfPrompt.Focus();

            SettingsTree.Press(window, Key.Enter, PhysicalKey.Enter);

            host.Requests.Count.ShouldBe(3);
            view.Candidates.ItemCount.ShouldBe(3);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>A blank task and a window that cannot run agents are told on an assertive status line, and nothing runs.</summary>
    [Fact]
    public async Task BestOfNTellsWhyItCannotRun()
    {
        var host = new ViewBestOfNHost();
        await RunAsync(new ViewWorktreeBackend(), host, (model, view, window) =>
        {
            Click(window, view, RunCandidatesName);

            host.Requests.ShouldBeEmpty();
            view.Status.Text.ShouldBe(WorktreeCommandsCopy.NeedsPrompt());
            AutomationProperties.GetLiveSetting(view.Status).ShouldBe(AutomationLiveSetting.Assertive);
            return Task.CompletedTask;
        }).ConfigureAwait(true);

        await RunAsync(new ViewWorktreeBackend(), null, (model, view, window) =>
        {
            view.BestOfPrompt.Text = "something";
            Click(window, view, RunCandidatesName);

            view.Status.Text.ShouldBe(WorktreeCommandsCopy.NoBestOfHost());
            view.Status.Classes.Contains("tone-error").ShouldBeTrue();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Keep this one asks to discard the other candidates through the delete confirmation; confirming deletes only those worktrees.</summary>
    [Fact]
    public async Task KeepThisOneDiscardsTheOthersAfterAConfirmation()
    {
        var backend = new ViewWorktreeBackend();
        var host = new ViewBestOfNHost();
        await RunAsync(backend, host, (model, view, window) =>
        {
            view.BestOfCount.SelectedIndex = 1;
            view.BestOfPrompt.Text = "fix the bug";
            Click(window, view, RunCandidatesName);
            var kept = model.Candidates[0].RunId;
            var discarded = model.Candidates[1].RunId;
            backend.Calls.Clear();

            Click(window, view, KeepName(1));

            backend.Calls.ShouldBeEmpty();
            view.DeleteConfirmation.IsVisible.ShouldBeTrue();
            view.DeleteWarning.Text.ShouldNotBeNull().ShouldContain(discarded, Case.Sensitive);
            view.DeleteWarning.Text.ShouldNotContain(kept, Case.Sensitive);
            SettingsTree.FocusedName(window).ShouldBe(WorktreeCommandsCopy.CancelLabel);

            Click(window, view, ConfirmDeleteName);

            backend.Calls.ShouldBe(["delete " + discarded]);
            view.Runs.ItemCount.ShouldBe(1);
            view.Candidates.ItemCount.ShouldBe(1);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Close raises the close request and closes the model.</summary>
    [Fact]
    public async Task CloseRaisesTheRequestAndClosesTheModel()
    {
        await RunAsync(new ViewWorktreeBackend(), null, (model, view, window) =>
        {
            model.Open();
            var closed = 0;
            view.CloseRequested += (_, _) => closed++;

            Click(window, view, CloseName);

            closed.ShouldBe(1);
            model.IsOpen.ShouldBeFalse();
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>Tab walks the toolbar, the rows and the best-of-n controls in the order they are drawn.</summary>
    [Fact]
    public async Task TabFollowsTheOrderOfTheControls()
    {
        var backend = new ViewWorktreeBackend();
        backend.AddRun("alpha", 1);
        await RunAsync(backend, null, (model, view, window) =>
        {
            var order = SettingsTree.TabStops(view)
                .Where(static control => control is Button or TextBox or ComboBox)
                .Select(static control => AutomationProperties.GetName(control))
                .ToArray();

            order.ShouldBe(
            [
                RefreshName,
                CloseName,
                NewNameName,
                WorktreeCommandsCopy.StartLabel,
                WorktreeCommandsCopy.PreviewName("alpha"),
                WorktreeCommandsCopy.ApplyName("alpha"),
                MergeName("alpha"),
                WorktreeCommandsCopy.DeleteName("alpha"),
                PromptName,
                CountName,
                RunCandidatesName,
            ]);

            _ = view.Refresh.Focus();
            SettingsTree.PressTab(window);
            SettingsTree.FocusedName(window).ShouldBe(CloseName);
            SettingsTree.PressTab(window, backwards: true);
            SettingsTree.FocusedName(window).ShouldBe(RefreshName);
            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }

    /// <summary>With a patch, a conflict result, candidates and a delete confirmation all shown, every control has an accessible name and meets the target size.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndBigEnough()
    {
        var backend = new ViewWorktreeBackend
        {
            Outcome = new WorktreeApplyOutcome(WorktreeApplyState.Conflicts, ["a.cs"], "1 file conflicts."),
        };
        backend.AddRun("alpha", 2);
        await RunAsync(backend, new ViewBestOfNHost(), (model, view, window) =>
        {
            view.BestOfCount.SelectedIndex = 1;
            view.BestOfPrompt.Text = "fix the bug";
            Click(window, view, RunCandidatesName);
            Click(window, view, WorktreeCommandsCopy.PreviewName("alpha"));
            Click(window, view, WorktreeCommandsCopy.ApplyName("alpha"));
            Click(window, view, KeepName(1));

            view.PreviewPanel.IsVisible.ShouldBeTrue();
            view.ApplyOutcome.IsVisible.ShouldBeTrue();
            view.Candidates.IsVisible.ShouldBeTrue();
            view.DeleteConfirmation.IsVisible.ShouldBeTrue();
            SettingsTree.Unnamed(window).ShouldBeEmpty();

            var controls = SettingsTree.Visible<Button>(view).Cast<Control>()
                .Concat(SettingsTree.Visible<TextBox>(view))
                .Concat(SettingsTree.Visible<ComboBox>(view))
                .Where(static control => control.TemplatedParent is null)
                .ToArray();
            controls.Length.ShouldBeGreaterThan(10);
            foreach (var control in controls)
            {
                var name = AutomationProperties.GetName(control);
                name.ShouldNotBeNullOrWhiteSpace();
                control.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too short");
                control.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, $"{name} is too narrow");
            }

            return Task.CompletedTask;
        }).ConfigureAwait(true);
    }
}
