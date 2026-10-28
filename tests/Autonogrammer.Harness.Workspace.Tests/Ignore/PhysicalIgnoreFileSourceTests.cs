using Autonogrammer.Workspace.Ignore;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests.Ignore;

/// <summary>The local ignore-file source reads inside its root only, never follows symbolic links, and feeds a real fixture tree to the matcher.</summary>
public sealed class PhysicalIgnoreFileSourceTests : IDisposable
{
    private readonly TempWorkspace _workspace = new();

    /// <inheritdoc/>
    public void Dispose() => _workspace.Dispose();

    /// <summary>A fixture tree with a root file, a nested <c>.gitignore</c> and a <c>.autonogrammer/ignore</c> file is loaded and matched as the files say.</summary>
    [Fact]
    public void AFixtureTreeIsLoadedFromDisk()
    {
        _workspace.Write(".cursorignore", "# generated\n*.gen\n!keep.gen\nsecrets/\n");
        _workspace.Write(".autonogrammer/ignore", "*.omp\n");
        _workspace.Write("pkg/.gitignore", "out/\n/local.txt\n");
        _workspace.Write("pkg/src/a.cs", "class A;");
        var source = new PhysicalIgnoreFileSource(_workspace.RootPath);

        var matcher = IgnoreMatcher.Load(source, ["", "pkg", "pkg/src", "absent"], new IgnoreOptions { CaseSensitive = true });

        matcher.Diagnostics.ShouldBeEmpty();
        matcher.IsIgnored("x.gen", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("keep.gen", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("secrets/token.txt", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("a.omp", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("pkg/out", isDirectory: true).ShouldBeTrue();
        matcher.IsIgnored("pkg/local.txt", isDirectory: false).ShouldBeTrue();
        matcher.IsIgnored("pkg/src/local.txt", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored("pkg/src/a.cs", isDirectory: false).ShouldBeFalse();
        matcher.IsIgnored(".env", isDirectory: false).ShouldBeTrue();
    }

    /// <summary>A missing file reads as null, a present file reads as text, and a file over the cap comes back longer than the cap without being read in full.</summary>
    [Fact]
    public void ReadingReportsMissingPresentAndOversizeFiles()
    {
        _workspace.Write(".cursorignore", "*.log\n");
        _workspace.Write("big/.cursorignore", new string('a', 5000));
        var source = new PhysicalIgnoreFileSource(_workspace.RootPath);

        source.ReadText("missing/.cursorignore", 1024).ShouldBeNull();
        source.ReadText(".cursorignore", 1024).ShouldBe("*.log\n");
        source.ReadText("big/.cursorignore", 100).ShouldBe(new string('a', 101));
        source.IsSymbolicLink(".cursorignore").ShouldBeFalse();
        source.IsSymbolicLink("missing/.cursorignore").ShouldBeFalse();
    }

    /// <summary>A path that leaves the root, or names nothing, is refused for reading and reported as unsafe.</summary>
    [Fact]
    public void APathOutsideTheRootIsRefused()
    {
        var siblingPath = _workspace.RootPath + "-sibling.txt";
        File.WriteAllText(siblingPath, "*.log\n");
        try
        {
            var source = new PhysicalIgnoreFileSource(_workspace.RootPath);

            source.ReadText("../" + Path.GetFileName(siblingPath), 1024).ShouldBeNull();
            source.ReadText("sub/../../x", 1024).ShouldBeNull();
            source.ReadText("/etc/passwd", 1024).ShouldBeNull();
            source.IsSymbolicLink("../x").ShouldBeTrue();
            source.IsSymbolicLink("/etc/passwd").ShouldBeTrue();
            source.IsSymbolicLink(string.Empty).ShouldBeTrue();
        }
        finally
        {
            File.Delete(siblingPath);
        }
    }

    /// <summary>An ignore file that is a symbolic link is not followed: it reads as missing, the source reports it, and the matcher records a diagnostic.</summary>
    [Fact]
    public void ASymbolicLinkedIgnoreFileIsNotFollowed()
    {
        _workspace.Write("real.txt", "*.log\n");
        Directory.CreateDirectory(Path.Combine(_workspace.RootPath, "sub"));
        if (!TryLink(() => File.CreateSymbolicLink(Path.Combine(_workspace.RootPath, "sub", ".cursorignore"), Path.Combine(_workspace.RootPath, "real.txt"))))
        {
            return;
        }

        var source = new PhysicalIgnoreFileSource(_workspace.RootPath);
        var matcher = IgnoreMatcher.Load(source, ["sub"], new IgnoreOptions { CaseSensitive = true, UseDefaultList = false });

        source.IsSymbolicLink("sub/.cursorignore").ShouldBeTrue();
        source.ReadText("sub/.cursorignore", 1024).ShouldBeNull();
        matcher.IsIgnored("sub/a.log", isDirectory: false).ShouldBeFalse();
        matcher.Diagnostics.Count.ShouldBe(1);
        matcher.Diagnostics[0].Source.ShouldBe("sub/.cursorignore");
    }

    /// <summary>An ignore file inside a folder that is a symbolic link is not followed either.</summary>
    [Fact]
    public void AnIgnoreFileBehindASymbolicLinkedFolderIsNotFollowed()
    {
        _workspace.Write("elsewhere/.cursorignore", "*.log\n");
        if (!TryLink(() => Directory.CreateSymbolicLink(Path.Combine(_workspace.RootPath, "linked"), Path.Combine(_workspace.RootPath, "elsewhere"))))
        {
            return;
        }

        var source = new PhysicalIgnoreFileSource(_workspace.RootPath);

        source.IsSymbolicLink("linked/.cursorignore").ShouldBeTrue();
        source.ReadText("linked/.cursorignore", 1024).ShouldBeNull();
        source.ReadText("elsewhere/.cursorignore", 1024).ShouldBe("*.log\n");
    }

    /// <summary>A blank root is refused.</summary>
    [Fact]
    public void ABlankRootIsRefused() =>
        Should.Throw<ArgumentException>(() => { _ = new PhysicalIgnoreFileSource(" "); });

    private static bool TryLink(Func<FileSystemInfo> create)
    {
        try
        {
            _ = create();
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }
}
