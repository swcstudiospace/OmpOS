using Autonogrammer.Native.Agents;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The rule that picks the agent's workspace at launch: a terminal launch inside a project keeps that project, while a launch from the
/// install folder, a system folder or a directory that no longer exists falls back to the user's home directory.
/// </summary>
public sealed class LaunchDirectoryTests
{
    private const string Home = "/home/someone";

    private static string Make(TempTree tree, params string[] segments)
    {
        var path = Path.Combine([tree.Root, .. segments]);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string Resolve(string current, string baseDirectory, string[] roots, StringComparison comparison = StringComparison.Ordinal) =>
        LaunchDirectory.ResolveWith(current, baseDirectory, roots, Home, comparison);

    /// <summary>A launch from the folder the application is installed in must not make that folder the workspace.</summary>
    [Fact]
    public void TheInstallFolderFallsBackToHome()
    {
        using var tree = new TempTree();
        var install = Make(tree, "Program Files", "Autonogrammer");

        Resolve(install, install, []).ShouldBe(Home);
    }

    /// <summary>A subfolder of the install folder is inside it, so it is refused too.</summary>
    [Fact]
    public void ASubfolderOfTheInstallFolderFallsBackToHome()
    {
        using var tree = new TempTree();
        var install = Make(tree, "Program Files", "Autonogrammer");
        var inside = Make(tree, "Program Files", "Autonogrammer", "runtimes", "win-x64");

        Resolve(inside, install, []).ShouldBe(Home);
    }

    /// <summary>A directory whose name merely starts with the install folder's name is a sibling, not a child, and is kept.</summary>
    [Fact]
    public void ASiblingWithTheSamePrefixIsKept()
    {
        using var tree = new TempTree();
        var install = Make(tree, "Autonogrammer");
        var sibling = Make(tree, "Autonogrammer-projects");

        Resolve(sibling, install, []).ShouldBe(sibling);
    }

    /// <summary>A protected root and everything below it are refused, for a root such as System32 or the Windows directory.</summary>
    [Fact]
    public void ASystemRootAndItsChildrenFallBackToHome()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var system32 = Make(tree, "Windows", "System32");
        var drivers = Make(tree, "Windows", "System32", "drivers");
        var windows = Path.Combine(tree.Root, "Windows");
        string[] roots = [windows, system32];

        Resolve(system32, install, roots).ShouldBe(Home);
        Resolve(drivers, install, roots).ShouldBe(Home);
        Resolve(windows, install, roots).ShouldBe(Home);
    }

    /// <summary>Every listed root counts, not only the first.</summary>
    [Fact]
    public void TheLastProtectedRootIsChecked()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var programFilesX86 = Make(tree, "Program Files (x86)");
        string[] roots = [Path.Combine(tree.Root, "Windows"), Path.Combine(tree.Root, "Program Files"), programFilesX86];

        Resolve(programFilesX86, install, roots).ShouldBe(Home);
    }

    /// <summary>A current directory that does not exist cannot be a workspace.</summary>
    [Fact]
    public void AMissingDirectoryFallsBackToHome()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");

        Resolve(Path.Combine(tree.Root, "gone"), install, []).ShouldBe(Home);
    }

    /// <summary>A terminal launch inside a project keeps that project, unchanged.</summary>
    [Fact]
    public void AProjectFolderIsKept()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var project = Make(tree, "src", "my-project");
        string[] roots = [Path.Combine(tree.Root, "Windows")];

        Resolve(project, install, roots).ShouldBe(project);
    }

    /// <summary>Trailing separators on any of the three paths neither hide a match nor change the kept path.</summary>
    [Fact]
    public void TrailingSeparatorsAreIgnored()
    {
        using var tree = new TempTree();
        var separator = Path.DirectorySeparatorChar;
        var install = Make(tree, "install");
        var inside = Make(tree, "install", "bin");
        var system = Make(tree, "system");
        var project = Make(tree, "project");

        Resolve(install + separator, install, []).ShouldBe(Home);
        Resolve(inside + separator, install + separator, []).ShouldBe(Home);
        Resolve(system, tree.Root + separator + "install", [system + separator]).ShouldBe(Home);
        Resolve(project + separator, install + separator, [system + separator]).ShouldBe(project);
    }

    /// <summary>Redundant separators and dot segments are normalised before the comparison.</summary>
    [Fact]
    public void DotSegmentsAreNormalised()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var inside = Make(tree, "install", "bin");
        var dotted = Path.Combine(tree.Root, "install", "bin", "..", "bin");

        Resolve(dotted, install, []).ShouldBe(Home);
        Resolve(inside, Path.Combine(tree.Root, "other", "..", "install"), []).ShouldBe(Home);
    }

    /// <summary>With a case-insensitive comparison (Windows, macOS) a different case still matches; with an ordinal one it does not.</summary>
    [Fact]
    public void CaseIsIgnoredOnlyWhenTheComparisonIgnoresIt()
    {
        using var tree = new TempTree();
        var install = Make(tree, "Install");
        var otherCase = Path.Combine(tree.Root, "INSTALL");

        Resolve(install, otherCase, [], StringComparison.OrdinalIgnoreCase).ShouldBe(Home);
        Resolve(install, otherCase, [], StringComparison.Ordinal).ShouldBe(install);
        Resolve(install, Path.Combine(tree.Root, "elsewhere"), [otherCase], StringComparison.OrdinalIgnoreCase).ShouldBe(Home);
    }

    /// <summary>The platform overload applies the running platform's own comparison, and a plain project folder is kept either way.</summary>
    [Fact]
    public void ThePlatformOverloadKeepsAProjectFolder()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var project = Make(tree, "project");

        LaunchDirectory.Resolve(project, install, [], Home).ShouldBe(project);
        LaunchDirectory.Resolve(install, install, [], Home).ShouldBe(Home);
    }

    /// <summary>Empty, blank, null and malformed input never throws and never yields a protected directory.</summary>
    [Fact]
    public void OddInputNeverThrows()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var project = Make(tree, "project");

        LaunchDirectory.Resolve(null, install, [], Home).ShouldBe(Home);
        LaunchDirectory.Resolve(string.Empty, install, [], Home).ShouldBe(Home);
        LaunchDirectory.Resolve("   ", install, [], Home).ShouldBe(Home);
        LaunchDirectory.Resolve("bad\0path", install, [], Home).ShouldBe(Home);
        LaunchDirectory.Resolve(project, null, null, Home).ShouldBe(project);
        LaunchDirectory.Resolve(project, string.Empty, ["", "bad\0root"], Home).ShouldBe(project);
    }

    /// <summary>When even the home directory is unknown the result is still a writable location, never the refused directory.</summary>
    [Fact]
    public void ABlankHomeFallsBackToTheTemporaryDirectory()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");

        var result = LaunchDirectory.Resolve(install, install, [], null);

        result.ShouldBe(Path.TrimEndingDirectorySeparator(Path.GetTempPath()));
    }

    /// <summary>The production list names the system locations of the running platform and never an empty entry.</summary>
    [Fact]
    public void TheProductionRootsAreNonEmptyAndPlatformSpecific()
    {
        var roots = LaunchDirectory.SystemProtectedRoots();

        roots.ShouldNotBeEmpty();
        roots.Any(string.IsNullOrWhiteSpace).ShouldBeFalse();
        if (!OperatingSystem.IsWindows())
        {
            roots.Contains("/usr/bin", StringComparer.Ordinal).ShouldBeTrue();
            roots.Contains("/Applications", StringComparer.Ordinal).ShouldBeTrue();
            roots.Contains("/usr", StringComparer.Ordinal).ShouldBeFalse();
            roots.Contains("/opt", StringComparer.Ordinal).ShouldBeFalse();
            roots.Contains("/usr/local", StringComparer.Ordinal).ShouldBeFalse();
        }
    }

    /// <summary>The production overload refuses the real application folder, so a launch from there never leaves the agent in the install tree.</summary>
    [Fact]
    public void TheProductionOverloadNeverReturnsTheApplicationFolder()
    {
        var result = LaunchDirectory.Resolve(AppContext.BaseDirectory, AppContext.BaseDirectory, LaunchDirectory.SystemProtectedRoots(), Home);

        result.ShouldBe(Home);
    }

    /// <summary>A launch from a filesystem root (<c>/</c> after a Finder or Dock launch, a drive root after a shortcut without a start folder) falls back to home.</summary>
    [Fact]
    public void AFilesystemRootFallsBackToHome()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");
        var driveOrFilesystemRoot = Path.GetPathRoot(tree.Root)!;

        Resolve(driveOrFilesystemRoot, install, []).ShouldBe(Home);
        Resolve(driveOrFilesystemRoot, install, [], StringComparison.OrdinalIgnoreCase).ShouldBe(Home);
        LaunchDirectory.Resolve(driveOrFilesystemRoot, install, [], Home).ShouldBe(Home);
        if (!OperatingSystem.IsWindows())
        {
            Resolve("/", install, []).ShouldBe(Home);
            Resolve("//", install, []).ShouldBe(Home);
        }
    }

    /// <summary>A folder that is not itself a filesystem root is kept, even when it sits directly below one.</summary>
    [Fact]
    public void AFolderThatIsNotARootIsKept()
    {
        using var tree = new TempTree();
        var install = Make(tree, "install");

        Resolve(tree.Root, install, []).ShouldBe(tree.Root);
        Resolve(tree.Workspace, install, []).ShouldBe(tree.Workspace);
    }

    /// <summary>Projects below <c>/opt</c> or <c>/usr/local</c> are kept, while the exact system directories of the production list are refused (the list is mirrored under a temporary root).</summary>
    [Fact]
    public void ProjectsBelowOptAndUsrLocalAreKeptButSystemDirectoriesAreRefused()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var tree = new TempTree();
        var install = Make(tree, "install");
        var mirrored = LaunchDirectory.SystemProtectedRoots().Select(root => Path.Combine(tree.Root, root.TrimStart('/'))).ToArray();
        var optProject = Make(tree, "opt", "work", "app");
        var usrLocalProject = Make(tree, "usr", "local", "src", "app");
        var usr = Make(tree, "usr");
        var usrBin = Make(tree, "usr", "bin", "tools");
        var applications = Make(tree, "Applications", "Some.app");

        Resolve(optProject, install, mirrored).ShouldBe(optProject);
        Resolve(usrLocalProject, install, mirrored).ShouldBe(usrLocalProject);
        Resolve(usr, install, mirrored).ShouldBe(usr);
        Resolve(usrBin, install, mirrored).ShouldBe(Home);
        Resolve(applications, install, mirrored).ShouldBe(Home);
    }
}
