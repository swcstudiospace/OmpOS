using Autonogrammer.Contracts.Ui;
using Autonogrammer.Presentation.Checkpoints;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.Checkpoints;

/// <summary>Verifies how a transcript is cut into turns and what each turn's net file edits are.</summary>
public sealed class TurnChangeSetTests
{
    private const string Root = "/work/app";

    private static UserMessageBlock User(string id, string text) => new() { Id = id, Text = text };

    [Fact]
    public void EachUserMessageBeginsATurnAndTheNetEditIsFirstOldToLastNew()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            User("u1", "Rename it"),
            ChangeBlocks.Edit("a.txt", "v0", "v1", "c1"),
            ChangeBlocks.Edit("a.txt", "v1", "v2", "c2"),
            User("u2", "Add a file"),
            ChangeBlocks.Edit("b.txt", null, "b", "c3"),
        ];

        var turns = TurnChangeSet.Build(blocks, Root);

        turns.Select(turn => turn.TurnKey).ShouldBe(["u1", "u2"]);
        turns[0].Label.ShouldBe("Rename it");
        turns[0].Files.ShouldHaveSingleItem().ShouldBe(new CheckpointFileEdit("a.txt", "v0", "v2"));
        turns[1].Files.ShouldHaveSingleItem().ShouldBe(new CheckpointFileEdit("b.txt", null, "b"));
    }

    [Fact]
    public void TheSameFileEditedInTwoTurnsIsInBothTurns()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            User("u1", "one"),
            ChangeBlocks.Edit("a.txt", "v0", "v1", "c1"),
            User("u2", "two"),
            ChangeBlocks.Edit("a.txt", "v1", "v2", "c2"),
        ];

        var turns = TurnChangeSet.Build(blocks, Root);

        turns.Select(turn => turn.Files.Single().OldText).ShouldBe(["v0", "v1"]);
    }

    [Fact]
    public void FailedCallsTurnsWithoutEditsAndFilesWithNoNetChangeAreLeftOut()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            User("u1", "no edits"),
            User("u2", "failing"),
            ChangeBlocks.Edit("a.txt", "a", "b", "bad"),
            ChangeBlocks.Call("bad", "failed"),
            User("u3", "round trip"),
            ChangeBlocks.Edit("c.txt", "x", "y", "c1"),
            ChangeBlocks.Edit("c.txt", "y", "x", "c2"),
        ];

        TurnChangeSet.Build(blocks, Root).ShouldBeEmpty();
    }

    [Fact]
    public void AbsolutePathsAreMadeRelativeAndPathsOutsideTheRootAreDropped()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            User("u1", "paths"),
            ChangeBlocks.Edit(Root + "/src/a.txt", "a", "b", "c1"),
            ChangeBlocks.Edit("/etc/passwd", "a", "b", "c2"),
            ChangeBlocks.Edit("../escape.txt", "a", "b", "c3"),
        ];

        var turns = TurnChangeSet.Build(blocks, Root);

        turns.ShouldHaveSingleItem().Files.Select(file => file.Path).ShouldBe(["src/a.txt"]);
    }

    [Fact]
    public void EditsBeforeAnyUserMessageFormTheSessionStartTurnAndALongPromptIsShortened()
    {
        IReadOnlyList<UiBlock> blocks =
        [
            ChangeBlocks.Edit("a.txt", "a", "b", "c1"),
            User("u1", "line one\nline two " + new string('z', 400)),
            ChangeBlocks.Edit("b.txt", "a", "b", "c2"),
        ];

        var turns = TurnChangeSet.Build(blocks, Root);

        turns[0].TurnKey.ShouldBe(TurnChangeSet.SessionStartKey);
        turns[1].Label.Length.ShouldBe(TurnChangeSet.MaxLabelChars);
        turns[1].Label.ShouldStartWith("line one line two", Case.Sensitive);
    }
}
