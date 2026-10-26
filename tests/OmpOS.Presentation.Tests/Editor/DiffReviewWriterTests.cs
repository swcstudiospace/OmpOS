using System.Text;
using OmpOS.Presentation.Editor;
using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>
/// Writing a reviewed diff to a real file (ADE-11): only the accepted hunks reach the disk, a file that changed since the review
/// began is never overwritten, and the write leaves no temporary file behind.
/// </summary>
public sealed class DiffReviewWriterTests : IDisposable
{
    private const string Original = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";
    private const string FirstHunkOnly = "alpha\nBETA\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";
    private const string SecondHunkOnly = "alpha\nbeta\ngamma\ndelta\nepsilon\nzeta\neta\niota\ntheta\n";

    private readonly string _root = Path.Combine(Path.GetTempPath(), "ompos-write-" + Guid.NewGuid().ToString("N"));

    /// <summary>Creates the sandbox workspace folder.</summary>
    public DiffReviewWriterTests()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
    }

    /// <summary>Removes the sandbox.</summary>
    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    /// <summary>Accepting the first hunk and leaving the second rejected writes exactly the first hunk's change.</summary>
    [Fact]
    public void AcceptingOneHunkWritesOnlyThatHunkToTheFile()
    {
        File.WriteAllText(Path.Combine(_root, "src", "notes.txt"), Original);
        var fileSystem = new LocalWorkspaceFileSystem(_root);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);
        review.Decide(1, HunkDecision.Reject);

        var status = new DiffReviewWriter(fileSystem).Write("src/notes.txt", review);

        status.ShouldBe(DiffWriteStatus.Written);
        File.ReadAllText(Path.Combine(_root, "src", "notes.txt")).ShouldBe(FirstHunkOnly);
        fileSystem.ReadFile("src/notes.txt").ShouldBe(FirstHunkOnly);
    }

    /// <summary>The rejected hunk's lines stay on disk exactly as they were, and the accepted hunk's lines are applied.</summary>
    [Fact]
    public void RejectingTheOtherHunkLeavesItsLinesOnDisk()
    {
        File.WriteAllText(Path.Combine(_root, "src", "notes.txt"), Original);
        var review = Review();
        review.Decide(0, HunkDecision.Reject);
        review.Decide(1, HunkDecision.Accept);

        var status = new DiffReviewWriter(new LocalWorkspaceFileSystem(_root)).Write("src/notes.txt", review);

        status.ShouldBe(DiffWriteStatus.Written);
        var onDisk = File.ReadAllText(Path.Combine(_root, "src", "notes.txt"));
        onDisk.ShouldBe(SecondHunkOnly);
        onDisk.ShouldContain("\nbeta\n");
        onDisk.ShouldNotContain("BETA", Case.Sensitive);
    }

    /// <summary>A file edited after the review began is refused: its newer text stays and nothing is written over it.</summary>
    [Fact]
    public void ANewerFileOnDiskRefusesTheWrite()
    {
        var path = Path.Combine(_root, "src", "notes.txt");
        File.WriteAllText(path, Original);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);
        const string Newer = "alpha\nbeta edited elsewhere\ngamma\ndelta\nepsilon\nzeta\neta\ntheta\n";
        File.WriteAllText(path, Newer);

        var status = new DiffReviewWriter(new LocalWorkspaceFileSystem(_root)).Write("src/notes.txt", review);

        status.ShouldBe(DiffWriteStatus.Stale);
        File.ReadAllText(path).ShouldBe(Newer);
        Directory.GetFileSystemEntries(Path.Combine(_root, "src")).Length.ShouldBe(1);
    }

    /// <summary>With nothing accepted the file is not touched, even when it has since changed.</summary>
    [Fact]
    public void NothingAccepted_LeavesTheFileAlone()
    {
        var path = Path.Combine(_root, "src", "notes.txt");
        File.WriteAllText(path, Original);
        var review = Review();
        review.Decide(1, HunkDecision.Reject);
        File.WriteAllText(path, "changed\n");

        var status = new DiffReviewWriter(new LocalWorkspaceFileSystem(_root)).Write("src/notes.txt", review);

        status.ShouldBe(DiffWriteStatus.Unchanged);
        File.ReadAllText(path).ShouldBe("changed\n");
    }

    /// <summary>The atomic write leaves no temporary file in the directory, and a byte order mark on the file survives.</summary>
    [Fact]
    public void Write_KeepsTheByteOrderMark_AndLeavesNoTemporaryFile()
    {
        var path = Path.Combine(_root, "src", "notes.txt");
        File.WriteAllBytes(path, [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(Original)]);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);

        new DiffReviewWriter(new LocalWorkspaceFileSystem(_root)).Write("src/notes.txt", review).ShouldBe(DiffWriteStatus.Written);

        var bytes = File.ReadAllBytes(path);
        bytes.AsSpan(0, 3).ToArray().ShouldBe(Encoding.UTF8.GetPreamble());
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).ShouldBe(FirstHunkOnly);
        Directory.GetFileSystemEntries(Path.Combine(_root, "src")).Length.ShouldBe(1);
    }

    /// <summary>A Unix file mode, such as the executable bit, survives the replace.</summary>
    [Fact]
    public void Write_KeepsTheUnixFileMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_root, "src", "notes.txt");
        File.WriteAllText(path, Original);
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead;
        File.SetUnixFileMode(path, mode);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);

        new DiffReviewWriter(new LocalWorkspaceFileSystem(_root)).Write("src/notes.txt", review).ShouldBe(DiffWriteStatus.Written);

        File.GetUnixFileMode(path).ShouldBe(mode);
    }

    /// <summary>A path that climbs above the root, and a file that does not exist, are refused without writing anything.</summary>
    [Fact]
    public void Write_OutsideTheRootOrToAMissingFile_IsRefused()
    {
        var fileSystem = new LocalWorkspaceFileSystem(_root);
        var writer = new DiffReviewWriter(fileSystem);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);

        Should.Throw<ArgumentException>(() => writer.Write("../escape.txt", review));
        Should.Throw<FileNotFoundException>(() => writer.Write("src/missing.txt", review));
        Directory.GetFileSystemEntries(Path.Combine(_root, "src")).ShouldBeEmpty();
    }

    /// <summary>The in-memory file system the other presentation tests use refuses a stale write the same way.</summary>
    [Fact]
    public void InMemoryFileSystem_RefusesAStaleWrite_AndCountsOnlyRealWrites()
    {
        var fileSystem = new InMemoryWorkspaceFileSystem().WithFile("notes.txt", Original);
        var writer = new DiffReviewWriter(fileSystem);
        var review = Review();
        review.Decide(0, HunkDecision.Accept);

        writer.Write("notes.txt", review).ShouldBe(DiffWriteStatus.Written);
        writer.Write("notes.txt", review).ShouldBe(DiffWriteStatus.Stale);

        fileSystem.ReadFile("notes.txt").ShouldBe(FirstHunkOnly);
        fileSystem.WriteCount.ShouldBe(1);
    }

    private static DiffReview Review() => new(Original, UnifiedDiff.Parse(UnifiedDiffTests.TwoHunks));
}
