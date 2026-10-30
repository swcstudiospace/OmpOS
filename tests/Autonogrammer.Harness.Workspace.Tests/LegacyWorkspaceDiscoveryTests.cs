using System.Security.Cryptography;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Workspace.Conventions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Workspace.Tests;

/// <summary>Legacy convention discovery: order, read-only fallback, conflict notices, and a catalog reload that does not write.</summary>
public sealed class LegacyWorkspaceDiscoveryTests
{
    private readonly TimeProvider _clock = new FixedClock();

    /// <summary>The candidate order is current, legacy, engine for a workspace root and for a home, with no file-system access.</summary>
    [Fact]
    public void OrderedCandidatesReturnsNewThenLegacyThenEngine()
    {
        var stamp = _clock.GetUtcNow();
        stamp.Year.ShouldBe(2026);
        var root = Path.Combine(Path.GetTempPath(), "ws-" + stamp.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        var home = Path.Combine(Path.GetTempPath(), "home-" + stamp.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));

        Names(LegacyConventionDiscovery.OrderedCandidates(root)).ShouldBe([LegacyConventionDiscovery.NewDirectoryName, LegacyBrandNames.ConventionDirectory, LegacyConventionDiscovery.EngineDirectoryName]);
        Names(LegacyConventionDiscovery.OrderedCandidates(home)).ShouldBe([LegacyConventionDiscovery.NewDirectoryName, LegacyBrandNames.ConventionDirectory, LegacyConventionDiscovery.EngineDirectoryName]);
        LegacyConventionDiscovery.OrderedFiles(root, "setup.json")[1].ShouldEndWith(Path.Combine(LegacyBrandNames.ConventionDirectory, "setup.json"));
        LegacyConventionDiscovery.OrderedFiles(home, "ignore")[2].ShouldEndWith(Path.Combine(LegacyConventionDiscovery.EngineDirectoryName, "ignore"));
    }

    /// <summary>A legacy-only tree loads the same command, skill, rule and subagent names as the current directory.</summary>
    [Fact]
    public async Task LegacyOnlyDirectoryLoadsTheSameSnapshotShape()
    {
        var ct = TestContext.Current.CancellationToken;
        using var legacy = new TempWorkspace();
        using var current = new TempWorkspace();
        WriteTree(legacy, LegacyBrandNames.ConventionDirectory);
        WriteTree(current, ".autonogrammer");

        var fromLegacy = await DiscoverAsync(legacy, ct).ConfigureAwait(true);
        var fromCurrent = await DiscoverAsync(current, ct).ConfigureAwait(true);

        fromLegacy.Commands.Select(item => item.Name).ShouldBe(fromCurrent.Commands.Select(item => item.Name));
        fromLegacy.Skills.Select(item => item.Name).ShouldBe(fromCurrent.Skills.Select(item => item.Name));
        fromLegacy.Rules.Select(item => item.Name).ShouldBe(fromCurrent.Rules.Select(item => item.Name));
        fromLegacy.Subagents.Select(item => item.Name).ShouldBe(fromCurrent.Subagents.Select(item => item.Name));
        fromLegacy.HookFiles.ShouldNotBeEmpty();
        fromLegacy.McpFiles.ShouldNotBeEmpty();
    }

    /// <summary>A legacy-only read returns one notice. A current-only tree and an empty tree return none.</summary>
    [Fact]
    public void LegacyOnlyReadReturnsANoticeAndTheOthersDoNot()
    {
        var root = Path.Combine(Path.GetTempPath(), "notice-" + Guid.NewGuid().ToString("N"));
        var legacy = Path.Combine(root, LegacyBrandNames.ConventionDirectory);
        var current = Path.Combine(root, ".autonogrammer");
        var onlyLegacy = LegacyConventionDiscovery.FindLegacyOnly(root, "project", path => string.Equals(path, legacy, StringComparison.Ordinal), ["rules/style.md"]);
        var notice = onlyLegacy.ShouldHaveSingleItem();
        notice.Scope.ShouldBe("project");
        notice.LegacyPath.ShouldBe(legacy);
        notice.NewPath.ShouldBe(current);
        notice.MoveCommand.ShouldBe(LegacyConventionDiscovery.MoveCommand("project"));
        notice.MoveCommand.ShouldStartWith("git mv");
        LegacyConventionDiscovery.MoveCommand("user").ShouldStartWith("mv --");
        LegacyConventionDiscovery.FindLegacyOnly(root, "project", path => string.Equals(path, current, StringComparison.Ordinal)).ShouldBeEmpty();
        LegacyConventionDiscovery.FindLegacyOnly(root, "project", _ => false).ShouldBeEmpty();
    }

    /// <summary>When both directories exist the current file wins and the notice names the conflicting legacy file.</summary>
    [Fact]
    public void BothPresentNamesTheConflictAndDoesNotMerge()
    {
        using var workspace = new TempWorkspace();
        workspace.Write(".autonogrammer/rules/style.md", "current");
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/rules/style.md", "legacy");
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/rules/only.md", "kept");
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.RootPath));

        catalog.Current.Rules.Single(rule => string.Equals(rule.Id, "style", StringComparison.Ordinal)).Text.ShouldBe("current");
        var notice = catalog.LegacyNotices.ShouldHaveSingleItem();
        notice.Conflict.ShouldBeTrue();
        notice.ConflictingFiles.ShouldContain(path => path.EndsWith("style.md", StringComparison.Ordinal));
        notice.ConflictingFiles.ShouldNotContain(path => path.EndsWith("only.md", StringComparison.Ordinal));
    }

    /// <summary>Discovery and a catalog reload leave the legacy tree byte-identical.</summary>
    [Fact]
    public async Task DiscoveryNeverWritesTheLegacyTree()
    {
        var ct = TestContext.Current.CancellationToken;
        using var workspace = new TempWorkspace();
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/rules/style.md", "legacy");
        workspace.Write(LegacyBrandNames.ConventionDirectory + "/mcp.json", "{}");
        var before = Snapshot(Path.Combine(workspace.RootPath, LegacyBrandNames.ConventionDirectory));

        await DiscoverAsync(workspace, ct).ConfigureAwait(true);
        using var catalog = new WorkspaceCatalog(new WorkspaceRoot(workspace.RootPath));
        catalog.Reload();

        Snapshot(Path.Combine(workspace.RootPath, LegacyBrandNames.ConventionDirectory)).ShouldBe(before);
        catalog.LegacyNotices.ShouldNotBeEmpty();
    }

    private static Task<ConventionSet> DiscoverAsync(TempWorkspace workspace, CancellationToken cancellationToken) =>
        ConventionCatalog.DiscoverAsync([workspace.RootPath], userHome: null, ConventionSources.Local([workspace.RootPath], null), cancellationToken: cancellationToken);

    private static void WriteTree(TempWorkspace workspace, string directory)
    {
        workspace.Write(directory + "/commands/hello.md", "---\ndescription: Says hello\n---\nHello\n");
        workspace.Write(directory + "/skills/triage/SKILL.md", "---\nname: triage\ndescription: Triage\n---\nTriage\n");
        workspace.Write(directory + "/rules/style.md", "Use the house style.\n");
        workspace.Write(directory + "/agents/reviewer.md", "---\nname: reviewer\ndescription: Reviews\n---\nReview\n");
        workspace.Write(directory + "/hooks.json", "{}");
        workspace.Write(directory + "/mcp.json", "{}");
    }

    private static string[] Names(IReadOnlyList<string> paths) => [.. paths.Select(path => Path.GetFileName(path)!)];

    private static Dictionary<string, string> Snapshot(string root)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            map[Path.GetRelativePath(root, file)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)));
        }

        return map;
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero);
    }
}
