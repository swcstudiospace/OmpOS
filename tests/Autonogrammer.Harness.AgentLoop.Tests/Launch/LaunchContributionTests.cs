using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>Tests generic launch contributor application in OmpLaunchPlanner: every field reaches the plan, and a conflict is refused.</summary>
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

    private static LaunchContribution Contribution(
        IReadOnlyList<string>? arguments = null,
        IReadOnlyDictionary<string, string?>? environment = null,
        IReadOnlyList<string>? overlay = null,
        string paragraph = "",
        string block = "",
        IProcessLaunchWrapper? wrapper = null) => new()
        {
            Arguments = arguments ?? Array.Empty<string>(),
            Environment = environment ?? new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = overlay ?? Array.Empty<string>(),
            SystemPromptParagraph = paragraph,
            FirstPromptBlock = block,
            ProcessWrapper = wrapper,
        };

    private sealed class Fixed(LaunchContribution contribution) : ILaunchContributor
    {
        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken) => contribution;
    }

    private sealed class Wrapper : IProcessLaunchWrapper
    {
        public ProcessLaunch Wrap(ProcessLaunch intended) => throw new NotSupportedException("The planner never wraps a launch.");
    }

    [Fact]
    public void ContributorParagraphOver2KiBIsRefused()
    {
        var over = new string('x', OmpLaunchPlanner.ContributorParagraphBudgetBytes + 1);
        var contrib = new Fixed(Contribution(paragraph: over));

        Should.Throw<ArgumentOutOfRangeException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { contrib }));
    }

    [Fact]
    public void EveryContributionFieldReachesThePlanInContributorOrder()
    {
        var wrapper = new Wrapper();
        var contributors = new ILaunchContributor[]
        {
            new Fixed(Contribution(
                arguments: ["--first"],
                environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["KEPT"] = "1", ["REMOVED"] = null },
                overlay: ["alpha:\n  on: true\n"],
                paragraph: "first paragraph",
                block: "first block",
                wrapper: wrapper)),
            new Fixed(Contribution(
                arguments: ["--second"],
                environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["KEPT"] = "1" },
                overlay: ["beta:\n  on: true\n"],
                paragraph: "second paragraph",
                block: "second block",
                wrapper: wrapper)),
        };

        var plan = OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: contributors);

        plan.ExtraArguments.ShouldBe(["--no-extensions", "--first", "--second"]);
        plan.Environment["KEPT"].ShouldBe("1");
        plan.Environment.ContainsKey("REMOVED").ShouldBeFalse();
        plan.EnvironmentRemovals.ShouldBe(["REMOVED"]);
        plan.OverlayEntries.ShouldBe(["alpha:\n  on: true\n", "beta:\n  on: true\n"]);
        plan.SystemPromptParagraphs.ShouldBe(["first paragraph", "second paragraph"]);
        plan.FirstPromptBlocks.ShouldBe(["first block", "second block"]);
        plan.ProcessWrapper.ShouldBeSameAs(wrapper);
        plan.ConfigOverlayPath.ShouldBeNull();
    }

    [Fact]
    public void TwoContributorsSettingOneVariableToDifferentValuesAreRefused()
    {
        var contributors = new ILaunchContributor[]
        {
            new Fixed(Contribution(environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["SHARED"] = "one" })),
            new Fixed(Contribution(environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["SHARED"] = "two" })),
        };

        var refused = Should.Throw<InvalidOperationException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: contributors));
        refused.Message.ShouldContain("SHARED");
    }

    [Fact]
    public void TwoContributorsSettingOneVariableToTheSameValueAreCompatible()
    {
        var contributors = new ILaunchContributor[]
        {
            new Fixed(Contribution(environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["SHARED"] = "same" })),
            new Fixed(Contribution(environment: new Dictionary<string, string?>(StringComparer.Ordinal) { ["SHARED"] = "same" })),
        };

        var plan = OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: contributors);

        plan.Environment["SHARED"].ShouldBe("same");
    }

    [Fact]
    public void TwoContributorsSettingOneTopLevelOverlayKeyAreRefused()
    {
        var contributors = new ILaunchContributor[]
        {
            new Fixed(Contribution(overlay: ["memory:\n  backend: off\n"])),
            new Fixed(Contribution(overlay: ["memory:\n  backend: local\n"])),
        };

        Should.Throw<InvalidOperationException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: contributors));
    }

    [Fact]
    public void TwoDifferentProcessWrappersAreRefused()
    {
        var contributors = new ILaunchContributor[]
        {
            new Fixed(Contribution(wrapper: new Wrapper())),
            new Fixed(Contribution(wrapper: new Wrapper())),
        };

        Should.Throw<InvalidOperationException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: contributors));
    }

    [Fact]
    public void APlanWithoutContributorsCarriesNoContributedField()
    {
        var plan = OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null);

        plan.OverlayEntries.ShouldBeEmpty();
        plan.SystemPromptParagraphs.ShouldBeEmpty();
        plan.FirstPromptBlocks.ShouldBeEmpty();
        plan.EnvironmentRemovals.ShouldBeEmpty();
        plan.ProcessWrapper.ShouldBeNull();
        plan.ExtraArguments.ShouldBe(["--no-extensions"]);
    }
}
