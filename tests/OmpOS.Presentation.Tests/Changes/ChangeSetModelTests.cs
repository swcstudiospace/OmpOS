using System.Globalization;
using OmpOS.Contracts.Streaming;
using OmpOS.Contracts.Ui;
using OmpOS.Presentation.Changes;
using OmpOS.Presentation.Editor;
using OmpOS.Presentation.State;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Keep, Undo and the hunk-level review of an agent's file edits against a real workspace folder: an undo writes the text from before the
/// agent back only while the file still holds what the agent left, a file the agent created is deleted under the same condition only, and
/// every refusal is a result that writes nothing.
/// </summary>
public sealed class ChangeSetModelTests : IDisposable
{
    private readonly ChangeWorkspace _workspace = new();
    private readonly ChangeSetModel _model;

    /// <summary>Opens a model over a fresh workspace.</summary>
    public ChangeSetModelTests()
    {
        _model = new ChangeSetModel(_workspace.FileSystem);
    }

    /// <summary>Removes the workspace.</summary>
    public void Dispose()
    {
        _model.Dispose();
        _workspace.Dispose();
    }

    /// <summary>The entries list each changed file with its counts, and the summary adds them up.</summary>
    [Fact]
    public void EntriesListTheChangedFilesAndTheSummaryAddsThemUp()
    {
        _model.HasChanges.ShouldBeFalse();

        _model.Update(
        [
            ChangeBlocks.Edit("a.txt", "a\nb\n", "a\nB\nc\n", "c1"),
            ChangeBlocks.Edit("n.txt", null, "x\n", "c2"),
        ]);

        _model.HasChanges.ShouldBeTrue();
        _model.Entries.Select(static entry => entry.Path).ShouldBe(["a.txt", "n.txt"]);
        _model.Entries[0].AddedText.ShouldBe("+2");
        _model.Entries[0].RemovedText.ShouldBe("-1");
        _model.Entries[1].Kind.ShouldBe(ChangeKind.Created);
        _model.Summary.ShouldBe("2 files changed +3 -1");
        _model.ActionableCount.ShouldBe(2);
    }

    /// <summary>Undo of an edited file writes the text from before the agent back to the disk.</summary>
    [Fact]
    public void UndoRestoresTheTextFromBeforeTheAgent()
    {
        _workspace.Write("src/a.txt", "new\n");
        _model.Update([ChangeBlocks.Edit("src/a.txt", "old\n", "new\n")]);

        var result = _model.Undo("src/a.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Read("src/a.txt").ShouldBe("old\n");
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Undone);
        _model.Announcement.ShouldBe(result.Message);
    }

    /// <summary>Several edits of one file are undone together: the file goes back to its text before the first edit.</summary>
    [Fact]
    public void UndoOfSeveralEditsRestoresTheTextBeforeTheFirst()
    {
        _workspace.Write("a.txt", "three\n");
        _model.Update(
        [
            ChangeBlocks.Edit("a.txt", "one\n", "two\n", "c1"),
            ChangeBlocks.Edit("a.txt", "two\n", "three\n", "c2"),
        ]);

        _model.Undo("a.txt").Outcome.ShouldBe(ChangeOutcome.Undone);

        _workspace.Read("a.txt").ShouldBe("one\n");
    }

    /// <summary>A file the agent created is deleted by Undo when it still holds what the agent wrote.</summary>
    [Fact]
    public void UndoDeletesAFileTheAgentCreated()
    {
        _workspace.Write("made/new.txt", "hello\n");
        _model.Update([ChangeBlocks.Edit("made/new.txt", null, "hello\n")]);

        var result = _model.Undo("made/new.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Exists("made/new.txt").ShouldBeFalse();
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Undone);
    }

    /// <summary>A file the agent created that the user then changed is never deleted: the entry is stale and the file stays.</summary>
    [Fact]
    public void UndoNeverDeletesACreatedFileTheUserChanged()
    {
        _workspace.Write("new.txt", "the user's own text\n");
        _model.Update([ChangeBlocks.Edit("new.txt", null, "hello\n")]);

        var result = _model.Undo("new.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Stale);
        _workspace.Read("new.txt").ShouldBe("the user's own text\n");
    }

    /// <summary>
    /// A file that changed since the agent edited it is refused with the stale message and nothing is written; once the file holds what the
    /// agent left again, the same entry can be undone.
    /// </summary>
    [Fact]
    public void UndoOfAFileChangedSinceTheAgentIsStaleAndWritesNothing()
    {
        _workspace.Write("src/a.txt", "user edit\n");
        _model.Update([ChangeBlocks.Edit("src/a.txt", "old\n", "new\n")]);

        var result = _model.Undo("src/a.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Stale);
        result.Message.ShouldBe("The file changed since the agent edited it; review it by hand.");
        _workspace.Read("src/a.txt").ShouldBe("user edit\n");
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Stale);
        _model.Announcement.ShouldContain("src/a.txt", Case.Sensitive);
        _model.Entries.Single().IsActionable.ShouldBeTrue();

        _workspace.Write("src/a.txt", "new\n");

        _model.Undo("src/a.txt").Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Read("src/a.txt").ShouldBe("old\n");
    }

    /// <summary>
    /// The transcript carries line feeds only, so a file whose every line ends in CRLF holds the agent's text: the undo is not stale, and it writes
    /// the text from before the agent back with the file's own CRLF endings, not with line feeds.
    /// </summary>
    [Fact]
    public void UndoOfACrlfFileWhoseTranscriptTextUsesLineFeedsRestoresItWithCrlf()
    {
        _workspace.Write("src/a.txt", "alpha\r\nBETA\r\ngamma\r\n");
        _model.Update([ChangeBlocks.Edit("src/a.txt", "alpha\nbeta\ngamma\n", "alpha\nBETA\ngamma\n")]);

        var result = _model.Undo("src/a.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Read("src/a.txt").ShouldBe("alpha\r\nbeta\r\ngamma\r\n");
    }

    /// <summary>A created CRLF file is deleted by its undo under the same equivalence.</summary>
    [Fact]
    public void UndoDeletesACreatedCrlfFileThatHoldsTheAgentsText()
    {
        _workspace.Write("made.txt", "x\r\ny\r\n");
        _model.Update([ChangeBlocks.Edit("made.txt", null, "x\ny\n")]);

        _model.Undo("made.txt").Outcome.ShouldBe(ChangeOutcome.Undone);

        _workspace.Exists("made.txt").ShouldBeFalse();
    }

    /// <summary>
    /// Only a file whose every line ends in CRLF is treated as the agent's text. A file that mixes endings, uses lone line feeds or lone returns,
    /// or has other text stays stale and is never written: guessing the endings could change a line the user wrote.
    /// </summary>
    [Theory]
    [InlineData("alpha\r\nBETA\ngamma\r\n")]
    [InlineData("alpha\nBETA\ngamma\n\r\n")]
    [InlineData("alpha\rBETA\rgamma\r")]
    [InlineData("alpha\r\nBETA\r\ngamma")]
    [InlineData("alpha\r\nbeta edited\r\ngamma\r\n")]
    public void UndoOfAFileThatIsNotPlainCrlfOfTheAgentsTextIsStaleAndWritesNothing(string onDisk)
    {
        _workspace.Write("src/a.txt", onDisk);
        _model.Update([ChangeBlocks.Edit("src/a.txt", "alpha\nbeta\ngamma\n", "alpha\nBETA\ngamma\n")]);

        var result = _model.Undo("src/a.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Stale);
        _workspace.Read("src/a.txt").ShouldBe(onDisk);
    }

    /// <summary>When the transcript text itself has carriage returns the comparison stays exact: a CRLF file with the same text is undone, an LF one is stale.</summary>
    [Fact]
    public void ATranscriptTextWithCarriageReturnsIsComparedExactly()
    {
        _workspace.Write("src/a.txt", "a\r\nB\r\n");
        _model.Update([ChangeBlocks.Edit("src/a.txt", "a\r\nb\r\n", "a\r\nB\r\n")]);

        _model.Undo("src/a.txt").Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Read("src/a.txt").ShouldBe("a\r\nb\r\n");
    }

    /// <summary>A file the agent edited that is gone now is stale, and is not created again.</summary>
    [Fact]
    public void UndoOfAnEditedFileThatWasRemovedIsStale()
    {
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n")]);

        _model.Undo("a.txt").Outcome.ShouldBe(ChangeOutcome.Stale);

        _workspace.Exists("a.txt").ShouldBeFalse();
    }

    /// <summary>A file the agent emptied, or deleted, comes back with its old text, whether it is gone or empty.</summary>
    [Fact]
    public void UndoOfAnEmptiedFileBringsItsTextBack()
    {
        _workspace.Write("empty.txt", string.Empty);
        _model.Update(
        [
            ChangeBlocks.Edit("empty.txt", "kept\n", string.Empty, "c1"),
            ChangeBlocks.Edit("deleted.txt", "gone\n", string.Empty, "c2"),
        ]);

        _model.Undo("empty.txt").Outcome.ShouldBe(ChangeOutcome.Undone);
        _model.Undo("deleted.txt").Outcome.ShouldBe(ChangeOutcome.Undone);

        _workspace.Read("empty.txt").ShouldBe("kept\n");
        _workspace.Read("deleted.txt").ShouldBe("gone\n");
    }

    /// <summary>A created file that is already gone has nothing to undo, and nothing is written.</summary>
    [Fact]
    public void UndoOfACreatedFileThatIsAlreadyGoneWritesNothing()
    {
        _model.Update([ChangeBlocks.Edit("new.txt", null, "hello\n")]);

        _model.Undo("new.txt").Outcome.ShouldBe(ChangeOutcome.Undone);

        _workspace.Exists("new.txt").ShouldBeFalse();
    }

    /// <summary>A path that reaches outside the workspace through a link is refused, and the file outside is not touched.</summary>
    [Fact]
    public void UndoThroughALinkOutsideTheWorkspaceIsRefused()
    {
        File.WriteAllText(Path.Combine(_workspace.Outside, "x.txt"), "new\n");
        Directory.CreateSymbolicLink(Path.Combine(_workspace.Root, "link"), _workspace.Outside);
        _model.Update([ChangeBlocks.Edit("link/x.txt", "old\n", "new\n")]);

        var result = _model.Undo("link/x.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Refused);
        File.ReadAllText(Path.Combine(_workspace.Outside, "x.txt")).ShouldBe("new\n");
    }

    /// <summary>A path outside the workspace never becomes an entry, so no action can reach it.</summary>
    [Fact]
    public void PathsOutsideTheWorkspaceNeverBecomeEntries()
    {
        _model.Update([ChangeBlocks.Edit("../escape.txt", "a\n", "b\n")]);

        _model.HasChanges.ShouldBeFalse();
        _model.Undo("../escape.txt").Outcome.ShouldBe(ChangeOutcome.NotApplicable);
    }

    /// <summary>A read-only file system is a failed result, never an exception, and the entry stays pending.</summary>
    [Fact]
    public void AReadOnlyFileSystemFailsWithAResult()
    {
        using var readOnly = new ChangeSetModel(new ReadOnlyFileSystem(_workspace.Root));
        readOnly.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n")]);

        var result = readOnly.Undo("a.txt");

        result.Outcome.ShouldBe(ChangeOutcome.Failed);
        readOnly.Entries.Single().Status.ShouldBe(ChangeStatus.Pending);
    }

    /// <summary>Keep leaves the file as it is, and the decision holds until the agent edits the file again.</summary>
    [Fact]
    public void KeepWritesNothingAndHoldsUntilTheAgentEditsAgain()
    {
        _workspace.Write("a.txt", "new\n");
        var edit = ChangeBlocks.Edit("a.txt", "old\n", "new\n", "c1");
        _model.Update([edit]);

        _model.Keep("a.txt").Outcome.ShouldBe(ChangeOutcome.Kept);

        _workspace.Read("a.txt").ShouldBe("new\n");
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Kept);
        _model.Entries.Single().IsActionable.ShouldBeFalse();

        _model.Update([edit]);
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Kept);

        _model.Update([edit, ChangeBlocks.Edit("a.txt", "new\n", "newer\n", "c2")]);
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Pending);
        _model.Entries.Single().Change.NewText.ShouldBe("newer\n");
    }

    /// <summary>An undone file stays undone when the transcript is read again, and a decided file takes no second decision.</summary>
    [Fact]
    public void ADecisionHoldsWhenTheTranscriptIsReadAgain()
    {
        _workspace.Write("a.txt", "new\n");
        var edit = ChangeBlocks.Edit("a.txt", "old\n", "new\n");
        _model.Update([edit]);
        _model.Undo("a.txt");

        _model.Update([edit]);

        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Undone);
        _model.Undo("a.txt").Outcome.ShouldBe(ChangeOutcome.NotApplicable);
        _model.Keep("a.txt").Outcome.ShouldBe(ChangeOutcome.NotApplicable);
    }

    /// <summary>Keep all keeps every pending file.</summary>
    [Fact]
    public void KeepAllKeepsEveryPendingFile()
    {
        _workspace.Write("a.txt", "A\n");
        _workspace.Write("b.txt", "B\n");
        _model.Update([ChangeBlocks.Edit("a.txt", "a\n", "A\n", "c1"), ChangeBlocks.Edit("b.txt", "b\n", "B\n", "c2")]);

        var results = _model.KeepAll();

        results.ShouldAllBe(result => result.Outcome == ChangeOutcome.Kept);
        _model.Entries.ShouldAllBe(entry => entry.Status == ChangeStatus.Kept);
        _model.ActionableCount.ShouldBe(0);
        _model.Announcement.ShouldBe("Kept the changes to 2 files.");
        _workspace.Read("a.txt").ShouldBe("A\n");
    }

    /// <summary>Undo all restores the files that still hold the agent's text and leaves a file that changed since, reporting both.</summary>
    [Fact]
    public void UndoAllRestoresWhatItCanAndLeavesTheStaleFile()
    {
        _workspace.Write("a.txt", "A\n");
        _workspace.Write("b.txt", "the user's text\n");
        _workspace.Write("c.txt", "C\n");
        _model.Update(
        [
            ChangeBlocks.Edit("a.txt", "a\n", "A\n", "c1"),
            ChangeBlocks.Edit("b.txt", "b\n", "B\n", "c2"),
            ChangeBlocks.Edit("c.txt", null, "C\n", "c3"),
        ]);

        var results = _model.UndoAll();

        results.Select(static result => result.Outcome).ShouldBe([ChangeOutcome.Undone, ChangeOutcome.Stale, ChangeOutcome.Undone]);
        _workspace.Read("a.txt").ShouldBe("a\n");
        _workspace.Read("b.txt").ShouldBe("the user's text\n");
        _workspace.Exists("c.txt").ShouldBeFalse();
        _model.Announcement.ShouldBe("Undid the changes to 2 files. 1 file was left as it is; review it by hand.");
        _model.ActionableCount.ShouldBe(1);
    }

    /// <summary>The commands take the file's path as their parameter, and Keep all and Undo all are disabled once nothing waits.</summary>
    [Fact]
    public void CommandsActOnThePathTheyAreGiven()
    {
        _workspace.Write("a.txt", "A\n");
        _model.Update([ChangeBlocks.Edit("a.txt", "a\n", "A\n")]);
        _model.KeepAllCommand.CanExecute(null).ShouldBeTrue();

        _model.UndoCommand.Execute("a.txt");

        _workspace.Read("a.txt").ShouldBe("a\n");
        _model.KeepAllCommand.CanExecute(null).ShouldBeFalse();
        _model.UndoAllCommand.CanExecute(null).ShouldBeFalse();
        _model.Entries.Single().UndoCommand.CanExecute(null).ShouldBeFalse();
    }

    /// <summary>A review of a file is the diff from the text before the agent with every hunk accepted, and Review raises it for the head.</summary>
    [Fact]
    public void ReviewRaisesTheDiffWithEveryHunkAccepted()
    {
        _workspace.Write("a.txt", "new\n");
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n")]);
        ChangeReviewRequestedEventArgs? raised = null;
        _model.ReviewRequested += (_, e) => raised = e;

        var review = _model.Review("a.txt");

        raised.ShouldNotBeNull();
        raised.Path.ShouldBe("a.txt");
        raised.Review.ShouldBeSameAs(review);
        raised.Review.Original.ShouldBe("old\n");
        raised.Review.Hunks.ShouldAllBe(hunk => hunk.Decision == HunkDecision.Accept);
        raised.Review.Apply().ShouldBe("new\n");
    }

    /// <summary>A file that was already decided has nothing to review.</summary>
    [Fact]
    public void ReviewOfADecidedFileRaisesNothing()
    {
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n")]);
        _model.Keep("a.txt");
        var raised = false;
        _model.ReviewRequested += (_, _) => raised = true;

        _model.Review("a.txt").ShouldBeNull();

        raised.ShouldBeFalse();
    }

    /// <summary>Rejecting one hunk and applying the review reverts only that hunk on disk; the rest of the edit stays and can still be undone.</summary>
    [Fact]
    public void RejectingOneHunkRevertsOnlyThatHunkOnDisk()
    {
        var (oldText, newText) = TwoHunkTexts();
        _workspace.Write("a.txt", newText);
        _model.Update([ChangeBlocks.Edit("a.txt", oldText, newText)]);
        var review = _model.CreateReview("a.txt")!;
        review.Hunks.Count.ShouldBe(2);
        review.Decide(0, HunkDecision.Reject);

        var result = _model.ApplyReview("a.txt", review);

        result.Outcome.ShouldBe(ChangeOutcome.PartlyUndone);
        var expectedLines = newText.Split('\n');
        expectedLines[1] = "line 2";
        var expected = string.Join('\n', expectedLines);
        _workspace.Read("a.txt").ShouldBe(expected);
        var entry = _model.Entries.Single();
        entry.Status.ShouldBe(ChangeStatus.Pending);
        entry.Change.NewText.ShouldBe(expected);
        (entry.Added, entry.Removed).ShouldBe((1, 1));

        _model.Undo("a.txt").Outcome.ShouldBe(ChangeOutcome.Undone);
        _workspace.Read("a.txt").ShouldBe(oldText);
    }

    /// <summary>A review with every hunk accepted keeps the file and writes nothing.</summary>
    [Fact]
    public void ApplyingAReviewWithEveryHunkAcceptedKeepsTheFile()
    {
        _workspace.Write("a.txt", "new\n");
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n")]);

        var result = _model.ApplyReview("a.txt", _model.CreateReview("a.txt")!);

        result.Outcome.ShouldBe(ChangeOutcome.Kept);
        _workspace.Read("a.txt").ShouldBe("new\n");
    }

    /// <summary>A review with every hunk rejected undoes the file, and a created file is deleted.</summary>
    [Fact]
    public void ApplyingAReviewWithEveryHunkRejectedUndoesTheFile()
    {
        _workspace.Write("a.txt", "new\n");
        _workspace.Write("n.txt", "made\n");
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n", "c1"), ChangeBlocks.Edit("n.txt", null, "made\n", "c2")]);

        foreach (var path in new[] { "a.txt", "n.txt" })
        {
            var review = _model.CreateReview(path)!;
            for (var index = 0; index < review.Hunks.Count; index++)
            {
                review.Decide(index, HunkDecision.Reject);
            }

            _model.ApplyReview(path, review).Outcome.ShouldBe(ChangeOutcome.Undone);
        }

        _workspace.Read("a.txt").ShouldBe("old\n");
        _workspace.Exists("n.txt").ShouldBeFalse();
    }

    /// <summary>Applying a review to a file that changed since the agent edited it is refused and writes nothing.</summary>
    [Fact]
    public void ApplyingAReviewToAChangedFileIsStaleAndWritesNothing()
    {
        var (oldText, newText) = TwoHunkTexts();
        _workspace.Write("a.txt", newText);
        _model.Update([ChangeBlocks.Edit("a.txt", oldText, newText)]);
        var review = _model.CreateReview("a.txt")!;
        review.Decide(0, HunkDecision.Reject);
        _workspace.Write("a.txt", newText + "user line\n");

        var result = _model.ApplyReview("a.txt", review);

        result.Outcome.ShouldBe(ChangeOutcome.Stale);
        _workspace.Read("a.txt").ShouldBe(newText + "user line\n");
        _model.Entries.Single().Status.ShouldBe(ChangeStatus.Stale);
    }

    /// <summary>A review made for another file's changes is refused as a programming error.</summary>
    [Fact]
    public void ApplyingAReviewMadeForAnotherFileThrows()
    {
        _model.Update([ChangeBlocks.Edit("a.txt", "old\n", "new\n", "c1"), ChangeBlocks.Edit("b.txt", "x\n", "y\n", "c2")]);
        var other = _model.CreateReview("b.txt")!;

        Should.Throw<ArgumentException>(() => _model.ApplyReview("a.txt", other));
    }

    /// <summary>The model follows a session: a file edit that arrives in the transcript becomes an entry, and a failed call does not.</summary>
    [Fact]
    public void AttachFollowsTheSessionTranscript()
    {
        var session = new SessionViewModel();
        _model.Attach(session);
        _model.HasChanges.ShouldBeFalse();

        session.ApplyEvent(Block(1, ChangeBlocks.Call("c1", "completed")));
        session.ApplyEvent(Block(2, ChangeBlocks.Edit("a.txt", "old\n", "new\n", "c1")));

        _model.Entries.Select(static entry => entry.Path).ShouldBe(["a.txt"]);

        session.ApplyEvent(Block(3, ChangeBlocks.Call("c2", "failed")));
        session.ApplyEvent(Block(4, ChangeBlocks.Edit("b.txt", "x\n", "y\n", "c2")));

        _model.Entries.Select(static entry => entry.Path).ShouldBe(["a.txt"]);
    }

    /// <summary>A model that stopped following a session ignores its later edits.</summary>
    [Fact]
    public void DisposeStopsFollowingTheSession()
    {
        var session = new SessionViewModel();
        _model.Attach(session);
        _model.Dispose();

        session.ApplyEvent(Block(1, ChangeBlocks.Edit("a.txt", "old\n", "new\n")));

        _model.HasChanges.ShouldBeFalse();
    }

    private static (string Old, string New) TwoHunkTexts()
    {
        var lines = Enumerable.Range(1, 30).Select(static number => "line " + number.ToString(CultureInfo.InvariantCulture)).ToArray();
        var changed = (string[])lines.Clone();
        changed[1] = "changed 2";
        changed[27] = "changed 28";
        return (string.Join('\n', lines) + "\n", string.Join('\n', changed) + "\n");
    }

    private static StreamEvent Block(long seq, UiBlock block) => new()
    {
        Seq = seq,
        SessionId = "s-1",
        CorrelationId = "s-1/turn-1",
        Timestamp = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
        Payload = new UiBlockPayload { Block = block },
    };

    /// <summary>A workspace that reads files and refuses to write, as a read-only file system does.</summary>
    private sealed class ReadOnlyFileSystem(string root) : IWorkspaceFileSystem
    {
        /// <inheritdoc/>
        public string RootPath { get; } = root;

        /// <inheritdoc/>
        public IReadOnlyList<WorkspaceEntry> ListDirectory(string relativeDirectory) => [];

        /// <inheritdoc/>
        public string ReadFile(string relativePath) => "new\n";
    }
}
