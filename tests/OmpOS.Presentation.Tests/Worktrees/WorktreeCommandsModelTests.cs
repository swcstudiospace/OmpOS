using OmpOS.Presentation.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests.Worktrees;

/// <summary>Verifies the worktree commands: start, review, apply with conflict reporting, delete behind a confirmation, and best-of-n.</summary>
public sealed class WorktreeCommandsModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 9, 5, 7, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static WorktreeRunInfo Run(string id, int files = 0) => new(id, "/wt/" + id, "ompos/run/" + id, files, null);

    private static (WorktreeCommandsModel Model, FakeWorktreeBackend Backend, ScriptedBestOfNHost Host) Create(params WorktreeRunInfo[] runs)
    {
        var backend = new FakeWorktreeBackend();
        backend.Runs.AddRange(runs);
        var host = new ScriptedBestOfNHost();
        return (new WorktreeCommandsModel(backend, new FixedTime(Now), host), backend, host);
    }

    [Fact]
    public async Task StartGeneratesAnIdFromTheClockAndShowsTheSetupSentence()
    {
        var (model, backend, _) = Create();
        backend.SetupMessage = "Setup ran.";

        var run = await model.StartAsync(null, Token).ConfigureAwait(true);

        run.ShouldNotBeNull().RunId.ShouldBe("wt-20261027-090507");
        model.Rows.Select(row => row.RunId).ShouldBe(["wt-20261027-090507"]);
        model.StatusText.ShouldContain("Setup ran.", Case.Sensitive);
        model.IsOpen.ShouldBeTrue();
        model.HasRuns.ShouldBeTrue();
        model.EmptyText.ShouldBeEmpty();
    }

    [Fact]
    public async Task AGivenNameIsMadeIntoAValidRunIdAndMadeUniqueAmongTheExistingRuns()
    {
        var (model, _, _) = Create(Run("fix-login"));

        var first = await model.StartAsync("fix login!", Token).ConfigureAwait(true);
        var second = await model.StartAsync("  Fix Login  ", Token).ConfigureAwait(true);
        var empty = await model.StartAsync("!!!", Token).ConfigureAwait(true);

        first.ShouldNotBeNull().RunId.ShouldBe("fix-login-2");
        second.ShouldNotBeNull().RunId.ShouldBe("Fix-Login-3");
        empty.ShouldNotBeNull().RunId.ShouldStartWith("wt-", Case.Sensitive);
    }

    [Fact]
    public async Task AStartThatFailsShowsTheReasonAndLeavesTheModelUsable()
    {
        var (model, backend, _) = Create();
        backend.StartFailure = new InvalidOperationException("Worktree limit of 25 reached");

        var run = await model.StartAsync("a", Token).ConfigureAwait(true);

        run.ShouldBeNull();
        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldContain("limit of 25", Case.Sensitive);
        model.IsBusy.ShouldBeFalse();
        (await model.StartAsync("a", Token).ConfigureAwait(true)).ShouldNotBeNull();
    }

    [Fact]
    public async Task PreviewShowsThePatchSummaryAndApplyingNeedsNoName_WhenThereIsExactlyOneRun()
    {
        var (model, backend, _) = Create(Run("only", 1));

        await model.PreviewAsync("only", Token).ConfigureAwait(true);
        await model.RefreshAsync(Token).ConfigureAwait(true);
        var outcome = await model.ApplyAsync(string.Empty, WorktreeApplyMethod.Clean, Token).ConfigureAwait(true);

        model.Preview.ShouldNotBeNull().Added.ShouldBe(3);
        outcome.ShouldNotBeNull().State.ShouldBe(WorktreeApplyState.Applied);
        backend.Calls.ShouldBe(["preview only", "apply only Clean"]);
        model.HasError.ShouldBeFalse();
        model.LastApply.ShouldBe(outcome);
    }

    [Fact]
    public async Task ApplyingWithoutANameAsksWhichRunWhenThereAreSeveralAndRefusesAnUnknownName()
    {
        var (model, backend, _) = Create(Run("a"), Run("b"));
        await model.RefreshAsync(Token).ConfigureAwait(true);

        var none = await model.ApplyAsync(string.Empty, WorktreeApplyMethod.Clean, Token).ConfigureAwait(true);

        none.ShouldBeNull();
        model.StatusText.ShouldBe("Name the worktree: a, b.");
        model.HasError.ShouldBeTrue();

        var unknown = await model.ApplyAsync("zzz", WorktreeApplyMethod.Clean, Token).ConfigureAwait(true);

        unknown.ShouldBeNull();
        model.StatusText.ShouldContain("there is no worktree named zzz", Case.Sensitive);
        backend.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task APatchThatDoesNotFitIsAnErrorStatusWithTheConflictingFilesAndAThreeWayRetryIsOffered()
    {
        var (model, backend, _) = Create(Run("a", 2));
        await model.RefreshAsync(Token).ConfigureAwait(true);
        backend.Outcome = new WorktreeApplyOutcome(WorktreeApplyState.Conflicts, ["src/a.cs", "b.txt"], "The changes of run a do not fit this checkout (src/a.cs, b.txt). Nothing was changed.");

        var outcome = await model.ApplyAsync("a", WorktreeApplyMethod.Clean, Token).ConfigureAwait(true);

        outcome.ShouldNotBeNull().ConflictPaths.ShouldBe(["src/a.cs", "b.txt"]);
        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldContain("Nothing was changed", Case.Sensitive);

        await model.ApplyThreeWayCommand.ExecuteAsync("a").ConfigureAwait(true);

        backend.Calls.ShouldBe(["apply a Clean", "apply a ThreeWay"]);
    }

    [Fact]
    public async Task DeletingAsksFirstAndSaysWhatIsLostAndOnlyThenDeletes()
    {
        var (model, backend, _) = Create(Run("a", 3), Run("b"));

        await model.RequestDeleteAsync("a", Token).ConfigureAwait(true);

        model.IsConfirmingDelete.ShouldBeTrue();
        model.DeleteConfirmationText.ShouldBe("Delete worktree a? 3 changed files are lost unless you applied it first.");
        backend.Calls.ShouldBeEmpty();

        (await model.ConfirmDeleteAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        backend.Calls.ShouldBe(["delete a"]);
        model.Rows.Select(row => row.RunId).ShouldBe(["b"]);
        model.IsConfirmingDelete.ShouldBeFalse();
        model.StatusText.ShouldBe("Deleted 1 worktree.");
    }

    [Fact]
    public async Task CancellingADeletionDeletesNothingAndConfirmingWithoutOneDoesNothing()
    {
        var (model, backend, _) = Create(Run("a"));
        (await model.ConfirmDeleteAsync(Token).ConfigureAwait(true)).ShouldBeFalse();
        await model.RequestDeleteAsync(string.Empty, Token).ConfigureAwait(true);

        model.CancelDelete();

        model.IsConfirmingDelete.ShouldBeFalse();
        model.StatusText.ShouldBe(WorktreeCommandsCopy.DeleteCancelled);
        backend.Calls.ShouldBeEmpty();
        backend.Runs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AFailedDeletionIsReportedTheRowsAreReadAgainAndTheConfirmationEnds()
    {
        var (model, backend, _) = Create(Run("a"));
        backend.DeleteFailureFor = "a";
        await model.RequestDeleteAsync("a", Token).ConfigureAwait(true);

        (await model.ConfirmDeleteAsync(Token).ConfigureAwait(true)).ShouldBeFalse();

        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldContain("locked", Case.Sensitive);
        model.IsConfirmingDelete.ShouldBeFalse();
        model.Rows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task BestOfNMakesOneWorktreePerCandidateRunsThePromptInEachAndListsTheResults()
    {
        var (model, backend, host) = Create();
        host.Failing = [2];

        await model.BestOfNAsync("  fix the test ", 3, Token).ConfigureAwait(true);

        backend.Calls.ShouldBe(["start bon-20261027090507-1", "start bon-20261027090507-2", "start bon-20261027090507-3"]);
        host.Requests.Select(request => (request.Index, request.Count, request.Prompt)).ShouldBe([(1, 3, "fix the test"), (2, 3, "fix the test"), (3, 3, "fix the test")], ignoreOrder: true);
        model.Candidates.Select(candidate => candidate.State).ShouldBe([BestOfNState.Finished, BestOfNState.Failed, BestOfNState.Finished]);
        model.Candidates[1].Summary.ShouldBe("ran out of context");
        model.Candidates[0].RowText.ShouldBe("Candidate 1: finished, 0 files changed. done 1");
        model.StatusText.ShouldBe("2 of 3 candidates finished. Review each one, then apply the best and discard the rest.");
        model.Rows.Count.ShouldBe(3);
    }

    [Fact]
    public async Task ACandidateWhoseWorktreeCannotBeMadeFailsAloneAndTheOthersStillRun()
    {
        var (model, backend, host) = Create();
        backend.StartFailure = new InvalidOperationException("limit reached");

        await model.BestOfNAsync("task", 2, Token).ConfigureAwait(true);

        model.Candidates[0].State.ShouldBe(BestOfNState.Failed);
        model.Candidates[0].Summary.ShouldBe("limit reached");
        model.Candidates[1].State.ShouldBe(BestOfNState.Finished);
        host.Requests.ShouldHaveSingleItem().Index.ShouldBe(2);
    }

    [Fact]
    public async Task BestOfNRefusesACountOutOfRangeABlankPromptAndAWindowWithoutAHost()
    {
        var (model, backend, _) = Create();

        await model.BestOfNAsync("task", 0, Token).ConfigureAwait(true);
        model.StatusText.ShouldBe(WorktreeCommandsCopy.CountOutOfRange());
        await model.BestOfNAsync("task", WorktreeCommandsCopy.MaxBestOf + 1, Token).ConfigureAwait(true);
        model.StatusText.ShouldBe(WorktreeCommandsCopy.CountOutOfRange());
        await model.BestOfNAsync("   ", 2, Token).ConfigureAwait(true);
        model.StatusText.ShouldBe(WorktreeCommandsCopy.NeedsPrompt());

        var hostless = new WorktreeCommandsModel(backend, new FixedTime(Now));
        await hostless.BestOfNAsync("task", 2, Token).ConfigureAwait(true);

        hostless.StatusText.ShouldBe(WorktreeCommandsCopy.NoBestOfHost());
        backend.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task DiscardingTheOthersAsksFirstAndDeletesEveryCandidateButTheChosenOne()
    {
        var (model, backend, _) = Create();
        await model.BestOfNAsync("task", 3, Token).ConfigureAwait(true);
        backend.Calls.Clear();

        model.RequestDiscardOthers(2).ShouldBeTrue();

        model.PendingDelete.Select(run => run.RunId).ShouldBe(["bon-20261027090507-1", "bon-20261027090507-3"]);
        backend.Calls.ShouldBeEmpty();

        (await model.ConfirmDeleteAsync(Token).ConfigureAwait(true)).ShouldBeTrue();

        model.Candidates.Select(candidate => candidate.Index).ShouldBe([2]);
        model.Rows.Select(row => row.RunId).ShouldBe(["bon-20261027090507-2"]);
        model.RequestDiscardOthers(2).ShouldBeFalse();
    }

    [Fact]
    public async Task TheCommandsTakeTheRunIdAsTheirParameterAndAreDisabledWithoutOne()
    {
        var (model, backend, _) = Create(Run("a"));
        await model.RefreshAsync(Token).ConfigureAwait(true);

        model.PreviewCommand.CanExecute(null).ShouldBeFalse();
        model.RequestDeleteCommand.CanExecute(string.Empty).ShouldBeFalse();
        await model.PreviewCommand.ExecuteAsync("a").ConfigureAwait(true);
        await model.ApplyCommand.ExecuteAsync("a").ConfigureAwait(true);

        backend.Calls.ShouldBe(["preview a", "apply a Clean"]);
        model.Rows[0].RowName.ShouldBe("a on ompos/run/a, 0 files changed");
        model.Rows[0].DeleteName.ShouldBe("Delete worktree a");
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
