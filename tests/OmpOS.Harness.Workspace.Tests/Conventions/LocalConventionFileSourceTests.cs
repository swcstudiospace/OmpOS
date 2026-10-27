using OmpOS.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace OmpOS.Workspace.Tests.Conventions;

/// <summary>The local file source stays inside the roots it was given and reports every failure as data.</summary>
public sealed class LocalConventionFileSourceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <inheritdoc/>
    public void Dispose() => _workspace.Dispose();

    /// <summary>A listing is in ordinal name order, marks folders and symbolic links, and is cut at the entry limit.</summary>
    [Fact]
    public async Task ListingIsOrderedMarkedAndCut()
    {
        _workspace.Write("b.txt", "b");
        _workspace.Write("a.txt", "a");
        _workspace.Write("sub/c.txt", "c");
        File.CreateSymbolicLink(Path.Combine(_workspace.RootPath, "link.txt"), Path.Combine(_workspace.RootPath, "a.txt"));
        var source = new LocalConventionFileSource([_workspace.RootPath]);

        var all = await source.ListAsync(_workspace.RootPath, 100, TestContext.Current.CancellationToken).ConfigureAwait(true);
        var cut = await source.ListAsync(_workspace.RootPath, 2, TestContext.Current.CancellationToken).ConfigureAwait(true);

        all.Problem.ShouldBeNull();
        all.Entries.Select(entry => entry.Name).ShouldBe(["a.txt", "b.txt", "link.txt", "sub"]);
        all.Entries.Single(entry => string.Equals(entry.Name, "sub", StringComparison.Ordinal)).IsDirectory.ShouldBeTrue();
        all.Entries.Single(entry => string.Equals(entry.Name, "link.txt", StringComparison.Ordinal)).IsSymbolicLink.ShouldBeTrue();
        all.Entries.Single(entry => string.Equals(entry.Name, "a.txt", StringComparison.Ordinal)).IsSymbolicLink.ShouldBeFalse();
        cut.Entries.Select(entry => entry.Name).ShouldBe(["a.txt", "b.txt"]);
    }

    /// <summary>A folder that does not exist lists no entries and no problem.</summary>
    [Fact]
    public async Task AMissingFolderListsNothing()
    {
        var source = new LocalConventionFileSource([_workspace.RootPath]);

        var result = await source.ListAsync(Path.Combine(_workspace.RootPath, "nope"), 10, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.Entries.ShouldBeEmpty();
        result.Problem.ShouldBeNull();
    }

    /// <summary>A path outside every root is refused for listing, reading and the existence check, even though the file exists.</summary>
    [Fact]
    public async Task APathOutsideTheRootsIsRefused()
    {
        using var outside = new TempWorkspace();
        outside.Write("secret.txt", "secret");
        var source = new LocalConventionFileSource([_workspace.RootPath]);
        var token = TestContext.Current.CancellationToken;

        var read = await source.ReadTextAsync(Path.Combine(outside.RootPath, "secret.txt"), 1000, token).ConfigureAwait(true);
        var listed = await source.ListAsync(outside.RootPath, 10, token).ConfigureAwait(true);
        var exists = await source.ExistsAsync(Path.Combine(outside.RootPath, "secret.txt"), token).ConfigureAwait(true);

        read.IsRead.ShouldBeFalse();
        read.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
        listed.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
        exists.ShouldBeFalse();
    }

    /// <summary>A path that climbs out of the root with dots is refused.</summary>
    [Fact]
    public async Task ADotDotPathIsRefused()
    {
        using var outside = new TempWorkspace();
        outside.Write("secret.txt", "secret");
        var source = new LocalConventionFileSource([_workspace.RootPath]);
        var climbing = _workspace.RootPath + "/../" + Path.GetFileName(outside.RootPath) + "/secret.txt";

        var read = await source.ReadTextAsync(climbing, 1000, TestContext.Current.CancellationToken).ConfigureAwait(true);

        read.IsRead.ShouldBeFalse();
        read.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
    }

    /// <summary>Reading returns the text without a byte order mark, reports a missing file and an oversize file as problems, and checks existence of files and folders.</summary>
    [Fact]
    public async Task ReadingReportsEveryFailureAsData()
    {
        _workspace.WriteBytes("bom.txt", [0xEF, 0xBB, 0xBF, 0x68, 0x69]);
        _workspace.Write("big.txt", new string('x', 20));
        _workspace.Write("dir/inner.txt", "i");
        var source = new LocalConventionFileSource([_workspace.RootPath]);
        var token = TestContext.Current.CancellationToken;

        (await source.ReadTextAsync(Path.Combine(_workspace.RootPath, "bom.txt"), 100, token).ConfigureAwait(true)).Text.ShouldBe("hi", Case.Sensitive);
        var missing = await source.ReadTextAsync(Path.Combine(_workspace.RootPath, "none.txt"), 100, token).ConfigureAwait(true);
        var big = await source.ReadTextAsync(Path.Combine(_workspace.RootPath, "big.txt"), 10, token).ConfigureAwait(true);

        missing.ProblemKind.ShouldBe(ConventionDiagnosticKind.Unreadable);
        big.ProblemKind.ShouldBe(ConventionDiagnosticKind.TooLarge);
        (await source.ExistsAsync(Path.Combine(_workspace.RootPath, "bom.txt"), token).ConfigureAwait(true)).ShouldBeTrue();
        (await source.ExistsAsync(Path.Combine(_workspace.RootPath, "dir"), token).ConfigureAwait(true)).ShouldBeTrue();
        (await source.ExistsAsync(Path.Combine(_workspace.RootPath, "none.txt"), token).ConfigureAwait(true)).ShouldBeFalse();
    }

    /// <summary>A source with no usable root allows nothing, and a null root list is refused.</summary>
    [Fact]
    public async Task ASourceWithoutRootsAllowsNothing()
    {
        var source = new LocalConventionFileSource([Path.Combine(_workspace.RootPath, "missing"), "  "]);

        var result = await source.ListAsync(_workspace.RootPath, 10, TestContext.Current.CancellationToken).ConfigureAwait(true);

        result.ProblemKind.ShouldBe(ConventionDiagnosticKind.EscapesRoot);
        Should.Throw<ArgumentNullException>(() => new LocalConventionFileSource(null!));
    }
}
