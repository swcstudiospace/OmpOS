using System.Reflection;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// Pins the planner application site for Phase 82 Task 2: what each <see cref="LaunchContribution"/> field does in
/// <see cref="OmpLaunchPlanner.Plan"/>, so the Task 4 parity diff compares against proven ACP behaviour.
/// Production code is unchanged by this task; these tests freeze current behaviour.
/// </summary>
public sealed class LaunchContributionSiteTests
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

    /// <summary>
    /// Planner site: Arguments, Environment and OverlayEntries are merged into the plan; the prompt paragraph, the
    /// first-prompt block and the process wrapper are not carried anywhere (the plan type has no such member).
    /// </summary>
    [Fact]
    public void PlannerMergesArgumentsEnvironmentAndOverlayButNotPromptOrWrapper()
    {
        var wrapper = new PassthroughWrapper();
        var contributor = new AllFieldsContributor(wrapper);
        var plan = OmpLaunchPlanner.Plan(
            Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null,
            contributors: new[] { contributor });
        var sessionDirectory = SessionStateLayout.SessionDirectory(Settings().StateRoot, "s1");
        plan.ExtraArguments.Skip(1).ShouldBe(["--contributed", "value", "--config", SessionStateLayout.ConfigOverlayFile(sessionDirectory)]);
        // Arguments merged, after the planner's own leading "--no-extensions".
        plan.ExtraArguments[0].ShouldBe("--no-extensions");

        // Environment merged: contributed entry present, the nulled planner entry removed.
        plan.Environment["CONTRIBUTED_VARIABLE"].ShouldBe("1");
        plan.Environment.ShouldNotContainKey("ULTRATHINK_STATE_DIR");

        // OverlayEntries merged and forcing "--config" naming the session config overlay file.
        plan.OverlayEntries.ShouldBe(["skills.customDirectories=/skills-root"]);
        plan.ConfigOverlayPath.ShouldBe(SessionStateLayout.ConfigOverlayFile(sessionDirectory));
        plan.ExtraArguments.ShouldContain("--config", StringComparer.Ordinal);
        plan.ExtraArguments.ShouldContain(plan.ConfigOverlayPath!, StringComparer.Ordinal);

        // Prompt paragraph and first-prompt block reach no plan surface.
        string.Join('\n', plan.ExtraArguments).ShouldNotContain("PARAGRAPH-MARKER", Case.Sensitive);
        string.Join('\n', plan.ExtraArguments).ShouldNotContain("FIRSTPROMPT-MARKER", Case.Sensitive);
        string.Join('\n', plan.OverlayEntries).ShouldNotContain("PARAGRAPH-MARKER", Case.Sensitive);
        plan.Environment.Values.ShouldNotContain("PARAGRAPH-MARKER", StringComparer.Ordinal);

        // The plan type itself has no prompt or wrapper member to carry them into.
        var propertyNames = typeof(OmpLaunchPlan).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name);
        propertyNames.ShouldNotContain("SystemPromptParagraph", StringComparer.Ordinal);
        propertyNames.ShouldNotContain("FirstPromptBlock", StringComparer.Ordinal);
        propertyNames.ShouldNotContain("ProcessWrapper", StringComparer.Ordinal);
    }

    /// <summary>Without overlay entries, the planner emits no "--config" and names no overlay file.</summary>
    [Fact]
    public void PlannerEmitsNoConfigFlagWithoutOverlayEntries()
    {
        var plan = OmpLaunchPlanner.Plan(
            Settings(), Array.Empty<ResolvedPlugin>(), "s2", "/w", null);

        plan.OverlayEntries.ShouldBeEmpty();
        plan.ConfigOverlayPath.ShouldBeNull();
        plan.ExtraArguments.ShouldNotContain("--config", StringComparer.Ordinal);
    }

    private sealed class AllFieldsContributor(IProcessLaunchWrapper wrapper) : ILaunchContributor
    {
        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken) => new()
        {
            Arguments = ["--contributed", "value"],
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["CONTRIBUTED_VARIABLE"] = "1",
                ["ULTRATHINK_STATE_DIR"] = null,
            },
            OverlayEntries = ["skills.customDirectories=/skills-root"],
            SystemPromptParagraph = "PARAGRAPH-MARKER",
            FirstPromptBlock = "FIRSTPROMPT-MARKER",
            ProcessWrapper = wrapper,
        };
    }

    private sealed class PassthroughWrapper : IProcessLaunchWrapper
    {
        public ProcessLaunch Wrap(ProcessLaunch intended) => intended;
    }
}
