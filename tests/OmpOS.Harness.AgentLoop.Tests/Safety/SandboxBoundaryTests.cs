using OmpOS.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.AgentLoop.Tests.Safety;

/// <summary>The read boundary on the real file system: real paths, symlink escapes, <c>~</c>, folders and globs.</summary>
public sealed class SandboxBoundaryTests
{
    private static bool Readable(SafetyRig rig, string path, string? sandboxJson)
    {
        if (sandboxJson is not null)
        {
            rig.WriteProject(".ompos/sandbox.json", sandboxJson);
        }

        var paths = new SafetyPaths(rig.Project, rig.Home);
        return new SandboxBoundary(rig.Sandbox(), paths).IsReadable(paths.Real(path, null));
    }

    [Fact]
    public void TheSystemBoundaryReadsAnywhereThatResolves()
    {
        using var rig = new SafetyRig();

        Readable(rig, Path.Combine(rig.Outside, "file.txt"), null).ShouldBeTrue();
    }

    [Fact]
    public void TheWorkspaceBoundaryReadsInsideTheWorkspaceAndRefusesOutsideIt()
    {
        using var rig = new SafetyRig();
        const string Workspace = """{ "readBoundary": "workspace" }""";

        Readable(rig, "src/a.ts", Workspace).ShouldBeTrue();
        Readable(rig, rig.InProject("src/a.ts"), Workspace).ShouldBeTrue();
        Readable(rig, rig.Project, Workspace).ShouldBeTrue();
        Readable(rig, Path.Combine(rig.Outside, "secret.txt"), Workspace).ShouldBeFalse();
        Readable(rig, rig.Project + "-sibling/x", Workspace).ShouldBeFalse("a folder that only shares the name prefix is outside");
        Readable(rig, "../outside/secret.txt", Workspace).ShouldBeFalse("a relative path that climbs out is outside");
    }

    [Fact]
    public void ASymlinkInsideTheWorkspaceThatPointsOutsideIsOutside()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        File.WriteAllText(Path.Combine(rig.Outside, "secret.txt"), "x");
        SafetyRig.LinkFolder(rig.InProject("link"), rig.Outside);
        const string Workspace = """{ "readBoundary": "workspace" }""";

        Readable(rig, "link/secret.txt", Workspace).ShouldBeFalse();
        Readable(rig, "link", Workspace).ShouldBeFalse();
    }

    [Fact]
    public void DotDotAfterASymlinkIsAppliedToTheLinkTargetNotTheLink()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        Directory.CreateDirectory(Path.Combine(rig.Outside, "deep"));
        SafetyRig.LinkFolder(rig.InProject("link"), Path.Combine(rig.Outside, "deep"));
        const string Workspace = """{ "readBoundary": "workspace" }""";

        // link/.. is the outside folder, not the workspace: a lexical reading would call it inside.
        Readable(rig, "link/../x.txt", Workspace).ShouldBeFalse();
    }

    [Fact]
    public void ASymlinkLoopCannotBeResolvedAndIsNeverReadable()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links needs a privilege on Windows.");
        using var rig = new SafetyRig();
        SafetyRig.LinkFolder(rig.InProject("a"), rig.InProject("b"));
        SafetyRig.LinkFolder(rig.InProject("b"), rig.InProject("a"));
        var paths = new SafetyPaths(rig.Project, rig.Home);

        paths.Real("a/file", null).ShouldBeNull();
        new SandboxBoundary(rig.Sandbox(), paths).IsReadable(null).ShouldBeFalse();
    }

    [Fact]
    public void AnAdditionalFolderOpensEverythingBelowItAndATildeIsExpanded()
    {
        using var rig = new SafetyRig();
        var sandbox = """{ "readBoundary": "workspace", "additionalReadPaths": ["~/shared", "OUTSIDE"] }""".Replace("OUTSIDE", rig.Outside.Replace('\\', '/'), StringComparison.Ordinal);

        Readable(rig, rig.InHome("shared/design/tokens.json"), sandbox).ShouldBeTrue();
        Readable(rig, "~/shared/a.txt", null).ShouldBeTrue();
        Readable(rig, Path.Combine(rig.Outside, "x", "y.txt"), null).ShouldBeTrue();
        Readable(rig, rig.InHome("other/a.txt"), null).ShouldBeFalse();
        Readable(rig, rig.InHome("shared-extra/a.txt"), null).ShouldBeFalse();
    }

    [Fact]
    public void AGlobInTheReadAllowlistOpensTheFilesItMatches()
    {
        using var rig = new SafetyRig();
        const string Sandbox = """{ "readBoundary": "workspace", "additionalReadPaths": ["~/notes/*.md", "~/logs/**/*.log"] }""";

        Readable(rig, rig.InHome("notes/today.md"), Sandbox).ShouldBeTrue();
        Readable(rig, rig.InHome("notes/today.txt"), null).ShouldBeFalse();
        Readable(rig, rig.InHome("notes/sub/today.md"), null).ShouldBeFalse();
        Readable(rig, rig.InHome("logs/a/b/c.log"), null).ShouldBeTrue();
        Readable(rig, rig.InHome("logs/a/b/c.txt"), null).ShouldBeFalse();
    }

    [Fact]
    public void TheSkillsRulesAndPluginsFoldersOfTheHomeAreAlwaysReadableInTheWorkspaceBoundary()
    {
        using var rig = new SafetyRig();
        const string Workspace = """{ "readBoundary": "workspace" }""";

        Readable(rig, rig.InHome(".ompos/skills/a/SKILL.md"), Workspace).ShouldBeTrue();
        Readable(rig, rig.InHome(".cursor/rules/style.mdc"), null).ShouldBeTrue();
        Readable(rig, rig.InHome(".ompos/permissions.json"), null).ShouldBeFalse();
        Readable(rig, rig.InHome(".ssh/id_ed25519"), null).ShouldBeFalse();
    }

    [Fact]
    public void ARelativeAllowlistEntryIsScopedToTheWorkspace()
    {
        using var rig = new SafetyRig();
        const string Sandbox = """{ "readBoundary": "workspace", "additionalReadPaths": ["../outside/**"] }""";

        // The entry is anchored at the workspace root, so it names the sibling folder by its real path.
        Readable(rig, Path.Combine(rig.Outside, "a.txt"), Sandbox).ShouldBeTrue();
    }

    [Fact]
    public void TildeExpansionNeedsAHomeDirectory()
    {
        using var rig = new SafetyRig();

        new SafetyPaths(rig.Project, null).ExpandHome("~/x").ShouldBe("~/x");
        new SafetyPaths(rig.Project, rig.Home).ExpandHome("~").ShouldBe(SafetyPaths.Portable(Path.GetFullPath(rig.Home)));
        new SafetyPaths(rig.Project, rig.Home).ExpandHome("~other/x").ShouldBe("~other/x");
    }
}
