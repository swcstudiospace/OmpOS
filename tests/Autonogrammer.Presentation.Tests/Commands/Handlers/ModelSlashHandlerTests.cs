using Autonogrammer.Presentation.Checkpoints;
using Autonogrammer.Presentation.Commands;
using Autonogrammer.Presentation.Commands.Handlers;
using Autonogrammer.Presentation.SideChats;
using Autonogrammer.Presentation.Tests.Checkpoints;
using Autonogrammer.Presentation.Tests.SideChats;
using Autonogrammer.Presentation.Tests.Worktrees;
using Autonogrammer.Presentation.Worktrees;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Commands.Handlers;

/// <summary>Verifies the slash handlers that run the checkpoint, fork, side chat and worktree models: what each typed argument does and that nothing destructive happens without the model's confirmation.</summary>
public sealed class ModelSlashHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static SlashActionRequest Typed(string typed) => new("x", string.Empty, typed);

    private static (CheckpointsModel Model, FakeCheckpointBackend Backend) CreateCheckpoints()
    {
        var backend = new FakeCheckpointBackend
        {
            Turns = [new CheckpointTurnInfo(1, "First", Now.AddMinutes(-5), ["a.txt"]), new CheckpointTurnInfo(2, "Second", Now.AddMinutes(-2), ["b.txt"])],
            PreviewFiles = [new CheckpointFilePreview("b.txt", CheckpointFileState.Restore, "b.txt detail")],
        };
        return (new CheckpointsModel("s1", backend, new FixedClock(Now)), backend);
    }

    [Fact]
    public async Task RewindWithoutANumberOpensTheTimelineAndWritesNothing()
    {
        var (model, backend) = CreateCheckpoints();
        var handler = new CheckpointSlashHandler(HandlerActionIds.ChatRewind, () => model);

        var result = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Completed);
        result.Message.ShouldBe(HandlerCopy.CheckpointsOpened);
        model.IsOpen.ShouldBeTrue();
        model.Rows.Count.ShouldBe(2);
        backend.Rewinds.ShouldBeEmpty();
    }

    [Fact]
    public async Task RewindWithANumberAsksForConfirmationAndRestoreIsTheSameCommand()
    {
        var (model, backend) = CreateCheckpoints();
        var rewind = new CheckpointSlashHandler(HandlerActionIds.ChatRewind, () => model);
        var restore = new CheckpointSlashHandler(HandlerActionIds.ChatRestore, () => model);

        var asked = await rewind.RunAsync(Typed("#2"), Token).ConfigureAwait(true);

        asked.Outcome.ShouldBe(SlashActionOutcome.Completed);
        asked.Message.ShouldContain("Rewind to before turn 2", Case.Sensitive);
        model.PendingTurnId.ShouldBe(2);
        model.CancelConfirmation();
        (await restore.RunAsync(Typed("1"), Token).ConfigureAwait(true)).Message.ShouldContain("before turn 1", Case.Sensitive);
        backend.Rewinds.ShouldBeEmpty();
    }

    [Fact]
    public async Task UndoAsksForTheLatestCheckpointAndANonNumberIsRefused()
    {
        var (model, _) = CreateCheckpoints();
        var undo = new CheckpointSlashHandler(HandlerActionIds.ChatUndo, () => model);
        var rewind = new CheckpointSlashHandler(HandlerActionIds.ChatRewind, () => model);

        (await undo.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Message.ShouldContain("before turn 2", Case.Sensitive);
        var refused = await rewind.RunAsync(Typed("last"), Token).ConfigureAwait(true);

        refused.Outcome.ShouldBe(SlashActionOutcome.Failed);
        refused.Message.ShouldBe(HandlerCopy.NeedsNumber);
        (await rewind.RunAsync(Typed("0"), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.Failed);
    }

    [Fact]
    public async Task RewindIsNotAvailableWhenSettingsTurnedItOffOrNoChatIsOpen()
    {
        var (model, _) = CreateCheckpoints();
        var off = new CheckpointSlashHandler(HandlerActionIds.ChatRewind, () => model, () => false);
        var none = new CheckpointSlashHandler(HandlerActionIds.ChatUndo, () => null);

        (await off.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Message.ShouldBe(HandlerCopy.RewindOff);
        none.CanRun.ShouldBeFalse();
        (await none.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true)).Outcome.ShouldBe(SlashActionOutcome.NotAvailable);
        Should.Throw<ArgumentException>(() => new CheckpointSlashHandler("chat.other", () => model));
    }

    [Fact]
    public async Task ForkWithoutANumberDuplicatesAndWithOneForksFromThatMessage()
    {
        var forker = new ScriptedSessionForker(canForkFromMessage: true);
        var model = new ForkModel("s1", forker);
        var handler = new ForkSlashHandler(() => model);

        var whole = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var partial = await handler.RunAsync(Typed("4"), Token).ConfigureAwait(true);
        var bad = await handler.RunAsync(Typed("here"), Token).ConfigureAwait(true);

        whole.Outcome.ShouldBe(SlashActionOutcome.Completed);
        partial.Message.ShouldContain("first 4 messages", Case.Sensitive);
        bad.Outcome.ShouldBe(SlashActionOutcome.Failed);
        forker.Requests.ShouldBe([new ForkRequest("s1", null), new ForkRequest("s1", 4)]);
    }

    [Fact]
    public async Task AForkThatTheEngineCannotMakeIsAFailedResultWithTheReason()
    {
        var forker = new ScriptedSessionForker(canForkFromMessage: false);
        var handler = new ForkSlashHandler(() => new ForkModel("s1", forker));

        var result = await handler.RunAsync(Typed("3"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldBe(ForkCopy.WholeChatOnly);
        new ForkSlashHandler(() => null).CanRun.ShouldBeFalse();
    }

    [Fact]
    public async Task SideOpensASideChatWithTheTypedQuestionAnsweredByTheEngine()
    {
        var engine = new ScriptedSideChatEngine();
        var hub = new SideChatHub(new Autonogrammer.Presentation.Chats.SideChatPanel("s1"), engine, () => []);
        var handler = new SideChatSlashHandler(() => hub);

        var asked = await handler.RunAsync(Typed("what is a token?"), Token).ConfigureAwait(true);
        var empty = await handler.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);

        asked.Outcome.ShouldBe(SlashActionOutcome.Completed);
        engine.Requests.ShouldHaveSingleItem().Question.ShouldBe("what is a token?");
        empty.Message.ShouldBe("Opened a side chat.");
        hub.Open.Count.ShouldBe(2);
        handler.ActionId.ShouldBe(SlashActionIds.SideOpen);
    }

    [Fact]
    public async Task AFailedSideAnswerIsAFailedResult()
    {
        var engine = new ScriptedSideChatEngine { Answer = null };
        var handler = new SideChatSlashHandler(() => new SideChatHub(new Autonogrammer.Presentation.Chats.SideChatPanel("s1"), engine, () => []));

        var result = await handler.RunAsync(Typed("why?"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldContain("engine offline", Case.Sensitive);
    }

    private static (WorktreeCommandsModel Model, FakeWorktreeBackend Backend) CreateWorktrees(params WorktreeRunInfo[] runs)
    {
        var backend = new FakeWorktreeBackend();
        backend.Runs.AddRange(runs);
        return (new WorktreeCommandsModel(backend, new FixedClock(Now), new ScriptedBestOfNHost()), backend);
    }

    [Fact]
    public async Task WorktreeStartApplyAndDeleteRunOverTheModelAndDeleteOnlyAsks()
    {
        var (model, backend) = CreateWorktrees();
        var start = new WorktreeSlashHandler(HandlerActionIds.WorktreeStart, () => model);
        var apply = new WorktreeSlashHandler(HandlerActionIds.WorktreeApply, () => model);
        var delete = new WorktreeSlashHandler(HandlerActionIds.WorktreeDelete, () => model);

        (await start.RunAsync(Typed("login fix"), Token).ConfigureAwait(true)).Message.ShouldContain("login-fix", Case.Sensitive);
        var applied = await apply.RunAsync(Typed(string.Empty), Token).ConfigureAwait(true);
        var asked = await delete.RunAsync(Typed("login-fix"), Token).ConfigureAwait(true);

        applied.Outcome.ShouldBe(SlashActionOutcome.Completed);
        asked.Message.ShouldContain("Delete worktree login-fix", Case.Sensitive);
        model.IsConfirmingDelete.ShouldBeTrue();
        backend.Calls.ShouldBe(["start login-fix", "apply login-fix Clean"]);
    }

    [Fact]
    public async Task ApplyWithTheWordMergeMergesThreeWaysAndAConflictIsAFailedResult()
    {
        var (model, backend) = CreateWorktrees(new WorktreeRunInfo("a", "/wt/a", "autonogrammer/run/a", 1, null));
        backend.Outcome = new WorktreeApplyOutcome(WorktreeApplyState.Conflicts, ["x.cs"], "Merged run a with conflicts (x.cs).");
        var apply = new WorktreeSlashHandler(HandlerActionIds.WorktreeApply, () => model);

        var result = await apply.RunAsync(Typed("a merge"), Token).ConfigureAwait(true);

        result.Outcome.ShouldBe(SlashActionOutcome.Failed);
        result.Message.ShouldContain("x.cs", Case.Sensitive);
        backend.Calls.ShouldBe(["apply a ThreeWay"]);
    }

    [Fact]
    public async Task BestOfNTakesTheCountAndTheTaskAndRefusesAMissingCount()
    {
        var (model, backend) = CreateWorktrees();
        var handler = new WorktreeSlashHandler(HandlerActionIds.WorktreeBestOfN, () => model);

        var bad = await handler.RunAsync(Typed("fix the bug"), Token).ConfigureAwait(true);
        var good = await handler.RunAsync(Typed("2 fix the bug"), Token).ConfigureAwait(true);

        bad.Message.ShouldBe(HandlerCopy.BestOfNeedsCount);
        good.Outcome.ShouldBe(SlashActionOutcome.Completed);
        model.Candidates.Count.ShouldBe(2);
        backend.Calls.Count(call => call.StartsWith("start ", StringComparison.Ordinal)).ShouldBe(2);
        new WorktreeSlashHandler(HandlerActionIds.WorktreeApply, () => null).CanRun.ShouldBeFalse();
        Should.Throw<ArgumentException>(() => new WorktreeSlashHandler("worktree.other", () => model));
    }
}
