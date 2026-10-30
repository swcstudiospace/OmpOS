using Autonogrammer.Presentation.Checkpoints;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Checkpoints;

/// <summary>Verifies the timeline, the preview and the confirmation that guards every rewind.</summary>
public sealed class CheckpointsModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static CheckpointTurnInfo Turn(int id, string label, params string[] files) => new(id, label, Now.AddMinutes(-10 * id), files);

    private static CheckpointFilePreview File(string path, CheckpointFileState state) => new(path, state, path + " detail");

    private static (CheckpointsModel Model, FakeCheckpointBackend Backend) Create(params CheckpointTurnInfo[] turns)
    {
        var backend = new FakeCheckpointBackend { Turns = [.. turns] };
        return (new CheckpointsModel("s1", backend, new FixedClock(Now)), backend);
    }

    [Fact]
    public async Task RefreshListsTheTurnsOldestFirstWithCountsAndAccessibleNames()
    {
        var (model, _) = Create(Turn(2, "Second", "b.txt", "c.txt"), Turn(1, "First", "a.txt"));

        model.HasCheckpoints.ShouldBeFalse();
        model.EmptyText.ShouldBe(CheckpointsCopy.Empty);

        await model.RefreshAsync(Token).ConfigureAwait(true);

        model.Rows.Select(row => row.TurnId).ShouldBe([1, 2]);
        model.Rows[1].FileCount.ShouldBe(2);
        model.Rows[1].RowName.ShouldBe("Turn 2: Second, 2 files changed, 20 min ago");
        model.Rows[0].RewindName.ShouldBe("Rewind files to before turn 1");
        model.CountText.ShouldBe("2 checkpoints");
        model.EmptyText.ShouldBeEmpty();
    }

    [Fact]
    public async Task PreviewShowsWhatWouldChangeAndWritesNothing()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore), File("b.txt", CheckpointFileState.Delete), File("c.bin", CheckpointFileState.Unrestorable)];

        await model.PreviewAsync(1, Token).ConfigureAwait(true);

        model.Preview.ShouldNotBeNull().ChangeCount.ShouldBe(2);
        model.PreviewLines.ShouldBe(["a.txt: a.txt detail", "b.txt: b.txt detail", "c.bin: c.bin detail"]);
        model.StatusText.ShouldContain("2 files", Case.Sensitive);
        backend.Rewinds.ShouldBeEmpty();
        model.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public async Task ARewindAsksFirstSaysTheConversationStaysAndOnlyThenWrites()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore)];
        await model.RefreshAsync(Token).ConfigureAwait(true);
        CheckpointRewoundEventArgs? raised = null;
        model.Rewound += (_, args) => raised = args;

        await model.RequestRewindAsync(1, Token).ConfigureAwait(true);

        model.IsConfirming.ShouldBeTrue();
        model.PendingTurnId.ShouldBe(1);
        model.ConfirmationText.ShouldContain("Rewind to before turn 1", Case.Sensitive);
        model.ConfirmationText.ShouldContain("The conversation is not changed", Case.Sensitive);
        model.CanForce.ShouldBeFalse();
        backend.Rewinds.ShouldBeEmpty();

        var outcome = await model.ConfirmAsync(force: false, Token).ConfigureAwait(true);

        outcome.ShouldNotBeNull().Status.ShouldBe(CheckpointRewindStatus.Completed);
        backend.Rewinds.ShouldBe([(1, false)]);
        model.IsConfirming.ShouldBeFalse();
        raised.ShouldNotBeNull().Outcome.ShouldBe(outcome);
        model.StatusText.ShouldContain("Restored 1 file.", Case.Sensitive);
        model.HasError.ShouldBeFalse();
    }

    [Fact]
    public async Task AStaleFileKeepsTheConfirmationOpenAndOnlyAnExplicitForceOverwritesIt()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Stale)];
        await model.RequestRewindAsync(1, Token).ConfigureAwait(true);
        model.CanForce.ShouldBeTrue();
        model.ConfirmationText.ShouldContain("changed since the agent's edit", Case.Sensitive);
        backend.RewindStatus = CheckpointRewindStatus.Blocked;

        var blocked = await model.ConfirmAsync(force: false, Token).ConfigureAwait(true);

        blocked.ShouldNotBeNull().Status.ShouldBe(CheckpointRewindStatus.Blocked);
        model.IsConfirming.ShouldBeTrue();
        model.HasError.ShouldBeTrue();
        backend.Rewinds.ShouldBe([(1, false)]);

        backend.RewindStatus = CheckpointRewindStatus.Completed;
        var forced = await model.ConfirmAsync(force: true, Token).ConfigureAwait(true);

        forced.ShouldNotBeNull().Status.ShouldBe(CheckpointRewindStatus.Completed);
        backend.Rewinds.ShouldBe([(1, false), (1, true)]);
        model.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public async Task ForceHasNoEffectWhenNoFileIsStale()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore)];
        await model.RequestRewindAsync(1, Token).ConfigureAwait(true);

        await model.ConfirmAsync(force: true, Token).ConfigureAwait(true);

        backend.Rewinds.ShouldBe([(1, false)]);
    }

    [Fact]
    public async Task CancellingTheConfirmationWritesNothingAndConfirmingWithoutOneDoesNothing()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore)];
        (await model.ConfirmAsync(force: false, Token).ConfigureAwait(true)).ShouldBeNull();
        await model.RequestRewindAsync(1, Token).ConfigureAwait(true);

        model.CancelConfirmation();

        model.IsConfirming.ShouldBeFalse();
        model.StatusText.ShouldBe(CheckpointsCopy.Cancelled);
        backend.Rewinds.ShouldBeEmpty();
        (await model.ConfirmAsync(force: false, Token).ConfigureAwait(true)).ShouldBeNull();
    }

    [Fact]
    public async Task ARewindToACheckpointThatIsGoneOpensNoConfirmation()
    {
        var (model, _) = Create();

        await model.RequestRewindAsync(9, Token).ConfigureAwait(true);

        model.IsConfirming.ShouldBeFalse();
        model.StatusText.ShouldBe(CheckpointsCopy.Gone);
    }

    [Fact]
    public async Task UndoLastAsksForTheNewestCheckpointAndSaysSoWhenThereIsNone()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"), Turn(2, "Second", "b.txt"));
        backend.PreviewFiles = [File("b.txt", CheckpointFileState.Restore)];

        await model.RequestUndoLastAsync(Token).ConfigureAwait(true);

        model.PendingTurnId.ShouldBe(2);

        var (empty, _) = Create();
        await empty.RequestUndoLastAsync(Token).ConfigureAwait(true);

        empty.IsConfirming.ShouldBeFalse();
        empty.StatusText.ShouldBe(CheckpointsCopy.Empty);
    }

    [Fact]
    public async Task ABackendFailureBecomesAnErrorSentenceAndTheModelIsUsableAgain()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.Failure = new IOException("no access");

        await model.RefreshAsync(Token).ConfigureAwait(true);

        model.HasError.ShouldBeTrue();
        model.StatusText.ShouldContain("no access", Case.Sensitive);
        model.IsBusy.ShouldBeFalse();

        backend.Failure = null;
        await model.RefreshAsync(Token).ConfigureAwait(true);

        model.HasError.ShouldBeFalse();
        model.Rows.Count.ShouldBe(1);
    }

    [Fact]
    public async Task TheCommandsTakeTheTurnNumberAsTheirParameterAndWaitWhileBusy()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore)];

        model.RewindCommand.CanExecute(0).ShouldBeFalse();
        await model.RewindCommand.ExecuteAsync(1).ConfigureAwait(true);

        model.PendingTurnId.ShouldBe(1);
        model.ConfirmCommand.CanExecute(null).ShouldBeTrue();
        model.ForceCommand.CanExecute(null).ShouldBeFalse();
        await model.CancelCommand.ExecuteAsync(null).ConfigureAwait(true);
        model.IsConfirming.ShouldBeFalse();
    }

    [Fact]
    public async Task RefreshTimesMovesTheRelativeTimeOfEveryRow()
    {
        var clock = new FixedClock(Now);
        var backend = new FakeCheckpointBackend { Turns = [Turn(1, "First", "a.txt")] };
        var model = new CheckpointsModel("s1", backend, clock);
        await model.RefreshAsync(Token).ConfigureAwait(true);
        model.Rows[0].TimeText.ShouldBe("10 min ago");

        clock.Advance(TimeSpan.FromHours(2));
        model.RefreshTimes();

        model.Rows[0].TimeText.ShouldBe("2 h ago");
    }

    [Fact]
    public async Task CloseHidesTheTimelineAndDismissesAConfirmation()
    {
        var (model, backend) = Create(Turn(1, "First", "a.txt"));
        backend.PreviewFiles = [File("a.txt", CheckpointFileState.Restore)];
        await model.RequestRewindAsync(1, Token).ConfigureAwait(true);
        model.IsOpen.ShouldBeTrue();

        model.Close();

        model.IsOpen.ShouldBeFalse();
        model.IsConfirming.ShouldBeFalse();
    }
}
