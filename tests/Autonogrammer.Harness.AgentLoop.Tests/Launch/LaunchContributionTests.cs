using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
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

        public TestContributor(string para) => _para = para;

        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
        {
            return new LaunchContribution
            {
                Arguments = [],
                Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
                OverlayEntries = [],
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
}
