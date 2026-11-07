using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.PluginBundles.Tests.Support;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.PluginBundles.Tests;

/// <summary>Bundle skills reach a launch only as overlay entries, and disabled skills are filtered or reported.</summary>
public sealed class BundleLaunchContributorTests
{
    /// <summary>A bundle with <c>skills/foo/SKILL.md</c> contributes exactly one overlay entry for the skills root.</summary>
    [Fact]
    public void ContributesOneOverlayForTheSkillsRoot()
    {
        using var fixture = new Fixture();
        var bundle = Path.Combine(fixture.Plugins, "p");
        BundleFixtureBuilder.WriteFolder(bundle, "foo");
        var contribution = fixture.Contributor().Contribute(Request(), CancellationToken.None);
        var skills = Path.GetFullPath(Path.Combine(bundle, "skills"));
        contribution.OverlayEntries.ShouldBe([BundleLaunchContributor.OverlayPrefix + skills]);
        contribution.Environment.ShouldBeEmpty();
        contribution.ProcessWrapper.ShouldBeNull();
        contribution.Arguments.ShouldBeEmpty();
    }

    /// <summary>A disabled skill is named in <c>--skills</c> by omission, and a skill that cannot be excluded is reported and not overlaid.</summary>
    [Fact]
    public void DisabledSkillsAreFilteredOrReported()
    {
        using var fixture = new Fixture();
        var bundle = Path.Combine(fixture.Plugins, "p");
        BundleFixtureBuilder.WriteFolder(bundle, "foo");
        BundleFixtureBuilder.WriteFolder(bundle, "bar");
        fixture.Skills.SetEnabled("p", "bar", false);
        var filtered = fixture.Contributor().Contribute(Request(), CancellationToken.None);
        filtered.Arguments.ShouldBe(["--skills", "foo"]);
        filtered.OverlayEntries.Count.ShouldBe(1);

        var hostile = Path.Combine(fixture.Plugins, "q");
        Directory.CreateDirectory(Path.Combine(hostile, "skills", "bad*name"));
        File.WriteAllText(Path.Combine(hostile, "skills", "bad*name", "SKILL.md"), "# no\n");
        fixture.Skills.SetEnabled("q", "bad*name", false);
        var contributor = fixture.Contributor();
        var reported = contributor.Contribute(Request(), CancellationToken.None);
        contributor.ExclusionReports.ShouldContain(report => report.SkillId == "bad*name");
        reported.OverlayEntries.ShouldNotContain(entry => entry.Contains("/q/skills", StringComparison.Ordinal));
        reported.SystemPromptParagraph.ShouldContain("cannot be excluded");
    }

    /// <summary>Commands, agents and hooks are listed. The hook state is not loaded and no hook path is an argument.</summary>
    [Fact]
    public void CommandsAgentsAndHooksAreListedNotRun()
    {
        using var fixture = new Fixture();
        var bundle = Path.Combine(fixture.Plugins, "p");
        Directory.CreateDirectory(Path.Combine(bundle, "commands"));
        Directory.CreateDirectory(Path.Combine(bundle, "agents"));
        Directory.CreateDirectory(Path.Combine(bundle, "hooks"));
        File.WriteAllText(Path.Combine(bundle, "commands", "a.md"), "# A\n");
        File.WriteAllText(Path.Combine(bundle, "agents", "x.md"), "# X\n");
        File.WriteAllText(Path.Combine(bundle, "hooks", "hooks.json"), """{"hooks":[{"command":"do-not-run"}]}""");

        new BundleCommandSource().ListCommands(bundle, "p").Single().PaletteId.ShouldBe("/p:a");
        BundleCommandSource.ListAgents(bundle, "p").Single().QualifiedName.ShouldBe("p:x");
        var hook = BundleCommandSource.ListHooks(bundle).Single();
        hook.State.ShouldBe("BundleComponentNotLoaded(hooks)");
        hook.Content.ShouldContain("do-not-run");

        var contribution = fixture.Contributor().Contribute(Request(), CancellationToken.None);
        contribution.Arguments.ShouldNotContain("do-not-run", StringComparer.Ordinal);
        contribution.OverlayEntries.ShouldNotContain(entry => entry.Contains("hooks", StringComparison.Ordinal));
    }

    private static LaunchRequest Request() => new()
    {
        Surface = "desktop",
        SessionId = "s",
        WorkingDirectory = "/work",
        TrustedFolders = [],
    };

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(Path.GetTempPath(), "ag-bundle-" + Guid.NewGuid().ToString("N"));
            Plugins = Path.Combine(Root, "plugins");
            Directory.CreateDirectory(Plugins);
            Skills = new SkillStateStore(Path.Combine(Root, "skills-state.json"));
            Bundles = new BundleStateStore(Path.Combine(Root, "plugins-state.json"));
        }

        public string Root { get; }

        public string Plugins { get; }

        public SkillStateStore Skills { get; }

        public BundleStateStore Bundles { get; }

        public BundleLaunchContributor Contributor() => new([Plugins], Skills, Bundles);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
