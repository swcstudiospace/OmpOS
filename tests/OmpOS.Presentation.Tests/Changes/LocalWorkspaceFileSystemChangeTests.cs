using OmpOS.Presentation.Changes;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Creating and deleting files on disk for the undo of an agent's edits, and writing a review against the text the file holds now: nothing
/// leaves the workspace root, a file that changed is never deleted, and no temporary file is left behind.
/// </summary>
public sealed class LocalWorkspaceFileSystemChangeTests : IDisposable
{
    private readonly ChangeWorkspace _workspace = new();

    /// <summary>Removes the workspace.</summary>
    public void Dispose() => _workspace.Dispose();

    /// <summary>A created file appears with its parent folders and leaves no temporary file next to it.</summary>
    [Fact]
    public void CreateFileMakesTheFileAndItsParentFolders()
    {
        _workspace.FileSystem.CreateFile("deep/er/new.txt", "hello\n");

        _workspace.Read("deep/er/new.txt").ShouldBe("hello\n");
        Directory.GetFiles(Path.Combine(_workspace.Root, "deep", "er")).Select(Path.GetFileName).ShouldBe(["new.txt"]);
    }

    /// <summary>A file that exists is never overwritten by a create.</summary>
    [Fact]
    public void CreateFileRefusesAnExistingFile()
    {
        _workspace.Write("a.txt", "mine\n");

        Should.Throw<IOException>(() => _workspace.FileSystem.CreateFile("a.txt", "other\n"));

        _workspace.Read("a.txt").ShouldBe("mine\n");
    }

    /// <summary>A create through a link that leaves the root is refused, and nothing is written outside.</summary>
    [Fact]
    public void CreateFileThroughALinkOutsideTheRootIsRefused()
    {
        Directory.CreateSymbolicLink(Path.Combine(_workspace.Root, "link"), _workspace.Outside);

        Should.Throw<UnauthorizedAccessException>(() => _workspace.FileSystem.CreateFile("link/new.txt", "x\n"));
        Should.Throw<ArgumentException>(() => _workspace.FileSystem.CreateFile("../new.txt", "x\n"));

        Directory.GetFiles(_workspace.Outside).ShouldBeEmpty();
    }

    /// <summary>A delete removes the file when it holds the expected text.</summary>
    [Fact]
    public void DeleteFileRemovesAFileThatHoldsTheExpectedText()
    {
        _workspace.Write("a.txt", "mine\n");

        _workspace.FileSystem.DeleteFile("a.txt", "mine\n");

        _workspace.Exists("a.txt").ShouldBeFalse();
    }

    /// <summary>A file that holds other text than expected is never deleted.</summary>
    [Fact]
    public void DeleteFileRefusesAFileThatChanged()
    {
        _workspace.Write("a.txt", "changed\n");

        Should.Throw<StaleWorkspaceFileException>(() => _workspace.FileSystem.DeleteFile("a.txt", "mine\n"));

        _workspace.Read("a.txt").ShouldBe("changed\n");
        Should.Throw<FileNotFoundException>(() => _workspace.FileSystem.DeleteFile("missing.txt", "x\n"));
    }

    /// <summary>A delete through a link that leaves the root is refused and the file outside stays.</summary>
    [Fact]
    public void DeleteFileThroughALinkOutsideTheRootIsRefused()
    {
        File.WriteAllText(Path.Combine(_workspace.Outside, "x.txt"), "x\n");
        Directory.CreateSymbolicLink(Path.Combine(_workspace.Root, "link"), _workspace.Outside);

        Should.Throw<UnauthorizedAccessException>(() => _workspace.FileSystem.DeleteFile("link/x.txt", "x\n"));

        File.Exists(Path.Combine(_workspace.Outside, "x.txt")).ShouldBeTrue();
    }

    /// <summary>
    /// A review whose original is the text before an edit writes to a file that holds the text after it: a review that accepts every hunk
    /// writes nothing, a rejected hunk is reverted, and a file that changed since is left alone.
    /// </summary>
    [Fact]
    public void WriteAgainstTheTextTheFileHoldsNowRevertsTheRejectedHunk()
    {
        const string oldText = "a\nb\nc\nd\ne\nf\ng\nh\ni\nj\n";
        const string newText = "a\nB\nc\nd\ne\nf\ng\nh\nI\nj\n";
        var review = new DiffReview(oldText, ChangeDiff.Create(oldText, newText));
        review.Hunks.Count.ShouldBe(1);
        _workspace.Write("a.txt", newText);
        var writer = new DiffReviewWriter(_workspace.FileSystem);

        review.Decide(0, HunkDecision.Accept);
        writer.Write("a.txt", review, newText).ShouldBe(DiffWriteStatus.Unchanged);

        _workspace.Write("a.txt", newText + "more\n");
        review.Decide(0, HunkDecision.Reject);
        writer.Write("a.txt", review, newText).ShouldBe(DiffWriteStatus.Stale);
        _workspace.Read("a.txt").ShouldBe(newText + "more\n");

        _workspace.Write("a.txt", newText);
        writer.Write("a.txt", review, newText).ShouldBe(DiffWriteStatus.Written);
        _workspace.Read("a.txt").ShouldBe(oldText);
    }
}
