using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Changes;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Reading a session's changed files out of the transcript: one entry per path with the first old text and the last new text, failed
/// calls ignored, and paths outside the workspace refused.
/// </summary>
public sealed class SessionChangeSetTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "ompos-change-set-root");

    /// <summary>Several edits of one file give one change with the text before the first edit and the text after the last.</summary>
    [Fact]
    public void SeveralEditsOfOnePathGiveOneChangeFromTheFirstOldToTheLastNew()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Call("c1", "completed"),
            ChangeBlocks.Edit("src/a.txt", "one\n", "two\n", "c1"),
            ChangeBlocks.Call("c2", "completed"),
            ChangeBlocks.Edit("src/a.txt", "two\n", "three\n", "c2"),
            ChangeBlocks.Edit("src/b.txt", "x\n", "y\n", "c2", index: 1),
        ];

        var set = SessionChangeSet.Build(blocks, Root);

        set.Changes.Select(static change => change.Path).ShouldBe(["src/a.txt", "src/b.txt"]);
        var first = set.Changes[0];
        first.OldText.ShouldBe("one\n");
        first.NewText.ShouldBe("three\n");
        first.EditCount.ShouldBe(2);
        first.LastBlockId.ShouldBe("c2-edit-0");
        first.Kind.ShouldBe(ChangeKind.Edited);
    }

    /// <summary>An absolute path inside the workspace is made relative to the root, and is the same file as its relative spelling.</summary>
    [Fact]
    public void AnAbsolutePathInsideTheWorkspaceIsTheSameFileAsItsRelativePath()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Edit(Path.Combine(Root, "src", "a.txt"), "one\n", "two\n", "c1"),
            ChangeBlocks.Edit("./src/../src/a.txt", "two\n", "three\n", "c2"),
        ];

        var set = SessionChangeSet.Build(blocks, Root);

        var change = set.Changes.ShouldHaveSingleItem();
        change.Path.ShouldBe("src/a.txt");
        change.OldText.ShouldBe("one\n");
        change.NewText.ShouldBe("three\n");
    }

    /// <summary>A file whose first edit has no old text was created by the agent, and its counts are all added lines.</summary>
    [Fact]
    public void AFileWhoseFirstEditHasNoOldTextWasCreated()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Edit("new.txt", null, "a\nb\n", "c1"),
            ChangeBlocks.Edit("new.txt", "a\nb\n", "a\nb\nc\n", "c2"),
        ];

        var change = SessionChangeSet.Build(blocks, Root).Changes.ShouldHaveSingleItem();

        change.Kind.ShouldBe(ChangeKind.Created);
        change.OldText.ShouldBeNull();
        change.NewText.ShouldBe("a\nb\nc\n");
        (change.Added, change.Removed).ShouldBe((3, 0));
    }

    /// <summary>An edit that leaves a file with no text is an emptied file: the transcript cannot tell it from a deleted one.</summary>
    [Fact]
    public void AnEditThatLeavesNoTextIsAnEmptiedFile()
    {
        var change = SessionChangeSet.Build([ChangeBlocks.Edit("gone.txt", "a\nb\n", string.Empty, "c1")], Root).Changes.ShouldHaveSingleItem();

        change.Kind.ShouldBe(ChangeKind.Emptied);
        (change.Added, change.Removed).ShouldBe((0, 2));
    }

    /// <summary>The edits of a tool call that failed never reached the file, so they are ignored, and the other calls still count.</summary>
    [Fact]
    public void FailedCallsAreIgnored()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Call("c1", "completed"),
            ChangeBlocks.Edit("a.txt", "one\n", "two\n", "c1"),
            ChangeBlocks.Call("c2", "failed"),
            ChangeBlocks.Edit("a.txt", "two\n", "three\n", "c2"),
            ChangeBlocks.Call("c3", "failed"),
            ChangeBlocks.Edit("b.txt", "x\n", "y\n", "c3"),
        ];

        var set = SessionChangeSet.Build(blocks, Root);

        var change = set.Changes.ShouldHaveSingleItem();
        change.Path.ShouldBe("a.txt");
        change.NewText.ShouldBe("two\n");
    }

    /// <summary>A path that leaves the workspace, in any spelling, is refused: it is not a change, and it is listed as refused.</summary>
    [Fact]
    public void PathsOutsideTheWorkspaceAreRefused()
    {
        var outside = Path.Combine(Path.GetDirectoryName(Root)!, "elsewhere", "secret.txt");
        UiBlock[] blocks =
        [
            ChangeBlocks.Edit("../escape.txt", "a\n", "b\n", "c1"),
            ChangeBlocks.Edit(outside, "a\n", "b\n", "c2"),
            ChangeBlocks.Edit("/etc/ompos-test-passwd", "a\n", "b\n", "c3"),
            ChangeBlocks.Edit("src/../../escape2.txt", "a\n", "b\n", "c4"),
            ChangeBlocks.Edit("ok.txt", "a\n", "b\n", "c5"),
        ];

        var set = SessionChangeSet.Build(blocks, Root);

        set.Changes.Select(static change => change.Path).ShouldBe(["ok.txt"]);
        set.RefusedPaths.ShouldBe(["../escape.txt", outside, "/etc/ompos-test-passwd", "src/../../escape2.txt"]);
    }

    /// <summary>A file the agent put back the way it was has no net change and is left out.</summary>
    [Fact]
    public void AFileEditedBackToItsOriginalTextHasNoChange()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Edit("a.txt", "one\n", "two\n", "c1"),
            ChangeBlocks.Edit("a.txt", "two\n", "one\n", "c2"),
        ];

        SessionChangeSet.Build(blocks, Root).Changes.ShouldBeEmpty();
    }

    /// <summary>The counts are the lines added and removed between the first old text and the last new text.</summary>
    [Fact]
    public void CountsSpanTheWholeSession()
    {
        UiBlock[] blocks =
        [
            ChangeBlocks.Edit("a.txt", "a\nb\nc\n", "a\nB\nc\n", "c1"),
            ChangeBlocks.Edit("a.txt", "a\nB\nc\n", "a\nB\nc\nd\n", "c2"),
        ];

        var change = SessionChangeSet.Build(blocks, Root).Changes.ShouldHaveSingleItem();

        (change.Added, change.Removed).ShouldBe((2, 1));
    }
}
