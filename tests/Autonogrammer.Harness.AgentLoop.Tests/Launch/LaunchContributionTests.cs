using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.AgentLoop.Tests.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>Tests generic launch contributor application in OmpLaunchPlanner.</summary>
public sealed class LaunchContributionTests
{
    private static AgentLoopSettings Settings() => new()
    {
        PluginsRoot = PlannerFixtures.PluginsRoot,
        StateRoot = PlannerFixtures.StateRoot,
        UltrathinkEnabled = false,
        SwarmEnabled = false,
        SubstrateEnabled = false,
        GsdEnabled = false,
    };

    private sealed class TestContributor : ILaunchContributor
    {
        private readonly string _para;
        private readonly string[] _overlay;

        public TestContributor(string para, params string[] overlay)
        {
            _para = para;
            _overlay = overlay;
        }

        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
        {
            return new LaunchContribution
            {
                Arguments = [],
                Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
                OverlayEntries = _overlay,
                SystemPromptParagraph = _para,
                FirstPromptBlock = string.Empty,
                ProcessWrapper = null,
            };
        }
    }

    [Fact]
    public void ContributorParagraphOver2KiBIsRefused()
    {
        var over = new string('x', OmpLaunchPlanner.ContributorParagraphBudgetBytes + 1);
        var contrib = new TestContributor(para: over);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { contrib }));
    }

    [Fact]
    public void ContributedOverlayEntryIsWrittenToTheFileThePlanNames()
    {
        using var fixture = new PluginFixtureDirectory();
        var settings = Settings() with { StateRoot = fixture.PathOf("state") };
        var contrib = new TestContributor(string.Empty, "memory:\n  backend: off\n");

        var plan = OmpLaunchPlanner.Plan(settings, Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { contrib });
        var sessionDirectory = SessionStateLayout.SessionDirectory(settings.StateRoot, "s1");
        using var writer = ConfigOverlayWriter.Write(settings, sessionDirectory, null, null, plan.OverlayEntries);

        plan.ExtraArguments.ShouldContain("--config", StringComparer.Ordinal);
        plan.ConfigOverlayPath.ShouldBe(SessionStateLayout.ConfigOverlayFile(sessionDirectory));
        writer.ShouldNotBeNull();
        writer.Path.ShouldBe(plan.ConfigOverlayPath);
        var lines = File.ReadAllLines(writer.Path);
        lines.ShouldContain("memory:", StringComparer.Ordinal);
        lines.ShouldContain("  backend: off", StringComparer.Ordinal);
    }

    [Fact]
    public void ConfigFlagIsEmittedOnlyForAnOverlayTheWriterWrites()
    {
        using var fixture = new PluginFixtureDirectory();
        var settings = Settings() with { StateRoot = fixture.PathOf("state") };

        var withEntry = OmpLaunchPlanner.Plan(settings, Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { new TestContributor(string.Empty, "memory:\n  backend: off\n") });
        var entrySession = SessionStateLayout.SessionDirectory(settings.StateRoot, "s1");
        using var written = ConfigOverlayWriter.Write(settings, entrySession, null, null, withEntry.OverlayEntries);

        written.ShouldNotBeNull();
        withEntry.ExtraArguments.ShouldContain("--config", StringComparer.Ordinal);
        withEntry.ConfigOverlayPath.ShouldBe(written.Path);
        File.ReadAllLines(written.Path).ShouldContain("  backend: off", StringComparer.Ordinal);

        var withoutEntry = OmpLaunchPlanner.Plan(settings, Array.Empty<ResolvedPlugin>(), "s2", "/w", null, contributors: new[] { new TestContributor(string.Empty) });
        var emptySession = SessionStateLayout.SessionDirectory(settings.StateRoot, "s2");

        withoutEntry.ExtraArguments.ShouldNotContain("--config", StringComparer.Ordinal);
        withoutEntry.ConfigOverlayPath.ShouldBeNull();
        ConfigOverlayWriter.Write(settings, emptySession, null, null, withoutEntry.OverlayEntries).ShouldBeNull();
        File.Exists(SessionStateLayout.ConfigOverlayFile(emptySession)).ShouldBeFalse();
    }
}
