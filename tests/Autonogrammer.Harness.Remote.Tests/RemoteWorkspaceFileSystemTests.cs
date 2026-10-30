using Autonogrammer.Presentation.Editor;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>
/// The remote workspace file system over the fake ssh: a real temporary directory plays the remote machine, and every operation is one script run
/// through the fake ssh by <c>sh -c</c>. The tests cover hostile names, symbolic links that leave the root, the size cap, binary files, the stale-content
/// guard and what ssh was given on its command line.
/// </summary>
public sealed class RemoteWorkspaceFileSystemTests
{
    private const int Cap = WorkspaceFileLimits.DefaultMaxReadBytes;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Files and directories are listed with their kinds, including hidden files and names with spaces, quotes, dollars and backticks.</summary>
    [Fact]
    public async Task ListingShowsKindsAndHostileNames()
    {
        using var fixture = RemoteFixture.Create();
        const string Hostile = "it's \"q\" $HOME `id`.txt";
        _ = fixture.Write("a b.txt", "x");
        _ = fixture.Write("sub/c.md", "y");
        _ = fixture.Write(".hidden", "z");
        _ = fixture.Write(Hostile, "w");

        var entries = await fixture.FileSystem().ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);

        entries.OrderBy(entry => entry.Name, StringComparer.Ordinal).Select(entry => (entry.Name, entry.RelativePath, entry.IsDirectory)).ToArray().ShouldBe(
        [
            (".hidden", ".hidden", false),
            ("a b.txt", "a b.txt", false),
            (Hostile, Hostile, false),
            ("sub", "sub", true),
        ]);
        (await fixture.FileSystem().ListDirectoryAsync("sub", Token).ConfigureAwait(true)).ShouldHaveSingleItem().RelativePath.ShouldBe("sub/c.md");
    }

    /// <summary>A name with a line break cannot be addressed and is left out; its neighbours are still listed.</summary>
    [Fact]
    public async Task ANameWithALineBreakIsLeftOut()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("good.txt", "x");
        _ = fixture.Write("bad\nname.txt", "x");

        var entries = await fixture.FileSystem().ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);

        entries.ShouldHaveSingleItem().Name.ShouldBe("good.txt");
    }

    /// <summary>A link that stays inside the root is listed; a link that leaves it, and a dangling link, are not.</summary>
    [Fact]
    public async Task LinksThatLeaveTheRootAreNotListed()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("a b.txt", "x");
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "escape"), fixture.Outside.Path);
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "inlink"), "a b.txt");
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "dangling"), "/nonexistent-autonogrammer-target");

        var entries = await fixture.FileSystem().ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);

        entries.Select(entry => entry.Name).Order(StringComparer.Ordinal).ToArray().ShouldBe(["a b.txt", "inlink"]);
    }

    /// <summary>Listing or reading through a directory link that leaves the root is refused and nothing outside is read.</summary>
    [Fact]
    public async Task ReadingThroughAnEscapingLinkIsRefused()
    {
        using var fixture = RemoteFixture.Create();
        await File.WriteAllTextAsync(fixture.Outside.Combine("secret.txt"), "TOP-SECRET", TestContext.Current.CancellationToken).ConfigureAwait(true);
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "escape"), fixture.Outside.Path);
        var fileSystem = fixture.FileSystem();

        await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.ListDirectoryAsync("escape", Token)).ConfigureAwait(true);
        var error = await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.ReadFileAsync("escape/secret.txt", Cap, Token)).ConfigureAwait(true);

        error.Message.ShouldNotContain("TOP-SECRET", Case.Sensitive);
    }

    /// <summary>A file link that stays inside the root reads like the file it points to.</summary>
    [Fact]
    public async Task AnInsideLinkReadsItsTarget()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("real.txt", "inside");
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "link.txt"), "real.txt");

        (await fixture.FileSystem().ReadFileAsync("link.txt", Cap, Token).ConfigureAwait(true)).ShouldBe("inside");
    }

    /// <summary>A missing directory, a file used as a directory and a missing file raise the exceptions the contract documents.</summary>
    [Fact]
    public async Task MissingPathsRaiseTheDocumentedExceptions()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("f.txt", "x");
        var fileSystem = fixture.FileSystem();

        await Should.ThrowAsync<DirectoryNotFoundException>(() => fileSystem.ListDirectoryAsync("nope", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<DirectoryNotFoundException>(() => fileSystem.ListDirectoryAsync("f.txt", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<FileNotFoundException>(() => fileSystem.ReadFileAsync("nope.txt", Cap, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<ArgumentException>(() => fileSystem.ReadFileAsync(string.Empty, Cap, Token)).ConfigureAwait(true);
    }

    /// <summary>A path that climbs out of the root is refused before ssh is started.</summary>
    [Fact]
    public async Task AClimbingPathIsRefusedBeforeSsh()
    {
        using var fixture = RemoteFixture.Create();

        await Should.ThrowAsync<ArgumentException>(() => fixture.FileSystem().ReadFileAsync("../outside.txt", Cap, Token)).ConfigureAwait(true);

        fixture.Host.SshCalls().ShouldBeEmpty();
    }

    /// <summary>A file larger than the cap is not returned, and the remote side stops reading after the cap.</summary>
    [Fact]
    public async Task AFileLargerThanTheCapIsRefused()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("small.txt", "0123456789012345");
        _ = fixture.Write("huge.txt", new string('x', 2 * 1024 * 1024));
        var fileSystem = fixture.FileSystem();

        await Should.ThrowAsync<WorkspaceFileTooLargeException>(() => fileSystem.ReadFileAsync("small.txt", 10, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<WorkspaceFileTooLargeException>(() => fileSystem.ReadFileAsync("huge.txt", Cap, Token)).ConfigureAwait(true);
        (await fileSystem.ReadFileAsync("small.txt", 16, Token).ConfigureAwait(true)).ShouldBe("0123456789012345");
    }

    /// <summary>A binary file and a file that is not UTF-8 are refused, and a byte order mark is not part of the text.</summary>
    [Fact]
    public async Task BinaryAndNonUtf8FilesAreRefusedAndTheBomIsDropped()
    {
        using var fixture = RemoteFixture.Create();
        await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "bin.dat"), [0x41, 0x00, 0x42], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "latin.txt"), [0x63, 0x61, 0x66, 0xE9], TestContext.Current.CancellationToken).ConfigureAwait(true);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "bom.txt"), [0xEF, 0xBB, 0xBF, 0x68, 0x69], TestContext.Current.CancellationToken).ConfigureAwait(true);
        var fileSystem = fixture.FileSystem();

        await Should.ThrowAsync<InvalidDataException>(() => fileSystem.ReadFileAsync("bin.dat", Cap, Token)).ConfigureAwait(true);
        await Should.ThrowAsync<InvalidDataException>(() => fileSystem.ReadFileAsync("latin.txt", Cap, Token)).ConfigureAwait(true);
        (await fileSystem.ReadFileAsync("bom.txt", Cap, Token).ConfigureAwait(true)).ShouldBe("hi");
    }

    /// <summary>Stat tells files, directories and missing paths apart, and gives the size of a file.</summary>
    [Fact]
    public async Task StatDescribesFilesDirectoriesAndMissingPaths()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("dir/file.txt", "12345");
        var fileSystem = fixture.FileSystem();

        var file = await fileSystem.StatAsync("dir/file.txt", Token).ConfigureAwait(true);
        var directory = await fileSystem.StatAsync("dir", Token).ConfigureAwait(true);
        var root = await fileSystem.StatAsync(string.Empty, Token).ConfigureAwait(true);
        var missing = await fileSystem.StatAsync("dir/none", Token).ConfigureAwait(true);

        file.ShouldNotBeNull().IsDirectory.ShouldBeFalse();
        file.Length.ShouldBe(5L);
        file.ModifiedUtc.ShouldNotBeNull();
        directory.ShouldNotBeNull().IsDirectory.ShouldBeTrue();
        root.ShouldNotBeNull().IsDirectory.ShouldBeTrue();
        missing.ShouldBeNull();
    }

    /// <summary>A write replaces the text, keeps the file mode, leaves no temporary file and puts the new text on stdin, never on the command line.</summary>
    [Fact]
    public async Task AWriteReplacesTheTextKeepsTheModeAndKeepsTheTextOffTheCommandLine()
    {
        if (OperatingSystem.IsWindows())
        {
            Assert.Skip("Unix file modes only.");
            return;
        }

        using var fixture = RemoteFixture.Create();
        const string Canary = "CANARY-CONTENT-7f3a91";
        var path = fixture.Write("script.sh", "old\n");
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
        File.SetUnixFileMode(path, mode);

        await fixture.FileSystem().WriteFileAsync("script.sh", Canary + "\n", "old\n", Token).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe(Canary + "\n");
        File.GetUnixFileMode(path).ShouldBe(mode);
        Directory.GetFileSystemEntries(fixture.Workspace, ".autonogrammer-*").ShouldBeEmpty();
        foreach (var call in fixture.Host.SshCalls())
        {
            call.ShouldAllBe(argument => !argument.Contains(Canary, StringComparison.Ordinal));
        }
    }

    /// <summary>A write keeps the byte order mark of the file it replaces.</summary>
    [Fact]
    public async Task AWriteKeepsTheByteOrderMark()
    {
        using var fixture = RemoteFixture.Create();
        var path = Path.Combine(fixture.Workspace, "bom.txt");
        await File.WriteAllBytesAsync(path, [0xEF, 0xBB, 0xBF, 0x68, 0x69], TestContext.Current.CancellationToken).ConfigureAwait(true);

        await fixture.FileSystem().WriteFileAsync("bom.txt", "new", "hi", Token).ConfigureAwait(true);

        (await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken).ConfigureAwait(true)).ShouldBe([0xEF, 0xBB, 0xBF, 0x6E, 0x65, 0x77]);
    }

    /// <summary>A write against text the file no longer holds is refused and the file is untouched.</summary>
    [Fact]
    public async Task AStaleWriteIsRefusedAndTheFileIsUntouched()
    {
        using var fixture = RemoteFixture.Create();
        var path = fixture.Write("f.txt", "one");

        await Should.ThrowAsync<StaleWorkspaceFileException>(() => fixture.FileSystem().WriteFileAsync("f.txt", "two", "something else", Token)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(path, Token).ConfigureAwait(true)).ShouldBe("one");
        Directory.GetFileSystemEntries(fixture.Workspace).Length.ShouldBe(1);
    }

    /// <summary>A write through a link that stays inside the root replaces the target and keeps the link.</summary>
    [Fact]
    public async Task AWriteThroughAnInsideLinkReplacesTheTarget()
    {
        using var fixture = RemoteFixture.Create();
        var target = fixture.Write("real.txt", "one");
        var link = Path.Combine(fixture.Workspace, "link.txt");
        File.CreateSymbolicLink(link, "real.txt");

        await fixture.FileSystem().WriteFileAsync("link.txt", "two", "one", Token).ConfigureAwait(true);

        (await File.ReadAllTextAsync(target, Token).ConfigureAwait(true)).ShouldBe("two");
        new FileInfo(link).LinkTarget.ShouldBe("real.txt");
    }

    /// <summary>Creating a file makes its parent directories, refuses an existing path and refuses to write through a link that leaves the root.</summary>
    [Fact]
    public async Task CreateMakesParentsRefusesExistingAndEscapingPaths()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("taken.txt", "x");
        File.CreateSymbolicLink(Path.Combine(fixture.Workspace, "escape"), fixture.Outside.Path);
        var fileSystem = fixture.FileSystem();

        await fileSystem.CreateFileAsync("new/deep/f.txt", "made", Token).ConfigureAwait(true);
        await Should.ThrowAsync<IOException>(() => fileSystem.CreateFileAsync("taken.txt", "again", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.CreateFileAsync("escape/new.txt", "leak", Token)).ConfigureAwait(true);
        await Should.ThrowAsync<UnauthorizedAccessException>(() => fileSystem.CreateFileAsync("escape/deeper/new.txt", "leak", Token)).ConfigureAwait(true);

        (await File.ReadAllTextAsync(Path.Combine(fixture.Workspace, "new", "deep", "f.txt"), Token).ConfigureAwait(true)).ShouldBe("made");
        (await File.ReadAllTextAsync(Path.Combine(fixture.Workspace, "taken.txt"), Token).ConfigureAwait(true)).ShouldBe("x");
        Directory.GetFileSystemEntries(fixture.Outside.Path).ShouldBeEmpty();
        Directory.GetFileSystemEntries(Path.Combine(fixture.Workspace, "new", "deep")).Length.ShouldBe(1);
    }

    /// <summary>Delete removes a file that still holds the expected text and refuses one that does not.</summary>
    [Fact]
    public async Task DeleteChecksTheExpectedText()
    {
        using var fixture = RemoteFixture.Create();
        var path = fixture.Write("gone.txt", "bye");
        _ = fixture.Write("kept.txt", "stay");
        var fileSystem = fixture.FileSystem();

        await Should.ThrowAsync<StaleWorkspaceFileException>(() => fileSystem.DeleteFileAsync("kept.txt", "other", Token)).ConfigureAwait(true);
        await fileSystem.DeleteFileAsync("gone.txt", "bye", Token).ConfigureAwait(true);
        await Should.ThrowAsync<FileNotFoundException>(() => fileSystem.DeleteFileAsync("gone.txt", "bye", Token)).ConfigureAwait(true);

        File.Exists(path).ShouldBeFalse();
        File.Exists(Path.Combine(fixture.Workspace, "kept.txt")).ShouldBeTrue();
    }

    /// <summary>A directory with more entries than the cap lists the cap and no more.</summary>
    [Fact]
    public async Task AHugeDirectoryIsBounded()
    {
        using var fixture = RemoteFixture.Create();
        for (var index = 0; index < RemoteWorkspaceFileSystem.MaxEntriesPerDirectory + 100; index++)
        {
            await File.WriteAllBytesAsync(Path.Combine(fixture.Workspace, "f" + index.ToString(System.Globalization.CultureInfo.InvariantCulture)), [], TestContext.Current.CancellationToken).ConfigureAwait(true);
        }

        var entries = await fixture.FileSystem().ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);

        entries.Count.ShouldBe(RemoteWorkspaceFileSystem.MaxEntriesPerDirectory);
    }

    /// <summary>Every call is one hardened ssh run: no terminal, no prompt, a double dash before the host, one quoted script line.</summary>
    [Fact]
    public async Task EveryCallIsOneHardenedSshRun()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("f.txt", "x");

        _ = await fixture.FileSystem().ReadFileAsync("f.txt", Cap, Token).ConfigureAwait(true);

        var call = fixture.Host.SshCalls().ShouldHaveSingleItem();
        call.ShouldContain("-T", StringComparer.Ordinal);
        call.ShouldContain("BatchMode=yes", StringComparer.Ordinal);
        call[^3].ShouldBe("--");
        call[^2].ShouldBe("fake-host");
        call[^1].ShouldStartWith("exec sh -c '", Case.Sensitive);
        call.ShouldNotContain("-tt", StringComparer.Ordinal);
    }

    /// <summary>A host that refuses the connection is an IOException that says so, never a hang or a different exception.</summary>
    [Fact]
    public async Task AnUnreachableHostIsAnIoExceptionWithTheDiagnosis()
    {
        using var fixture = RemoteFixture.Create(new FakeRemoteHostOptions { Mode = "refused" });

        var error = await Should.ThrowAsync<IOException>(() => fixture.FileSystem().ListDirectoryAsync(string.Empty, Token)).ConfigureAwait(true);

        error.Message.ShouldContain("cannot be reached", Case.Sensitive);
    }

    /// <summary>The sync adapter and the remote file system are interchangeable behind the async interface: the same tree lists the same names.</summary>
    [Fact]
    public async Task TheRemoteListingMatchesTheLocalAdapterListing()
    {
        using var fixture = RemoteFixture.Create();
        _ = fixture.Write("a.txt", "1");
        _ = fixture.Write("d/b.txt", "2");
        _ = fixture.Write("d/e/c.txt", "3");
        var local = new AsyncWorkspaceFileSystemAdapter(new LocalWorkspaceFileSystem(fixture.Workspace));
        var remote = fixture.FileSystem();

        foreach (var directory in new[] { string.Empty, "d", "d/e" })
        {
            var expected = (await local.ListDirectoryAsync(directory, Token).ConfigureAwait(true)).Select(entry => entry.RelativePath + "|" + entry.IsDirectory).Order(StringComparer.Ordinal).ToArray();
            var actual = (await remote.ListDirectoryAsync(directory, Token).ConfigureAwait(true)).Select(entry => entry.RelativePath + "|" + entry.IsDirectory).Order(StringComparer.Ordinal).ToArray();
            actual.ShouldBe(expected);
        }

        (await remote.ReadFileAsync("d/e/c.txt", Cap, Token).ConfigureAwait(true)).ShouldBe(await local.ReadFileAsync("d/e/c.txt", Cap, Token).ConfigureAwait(true));
    }
}
