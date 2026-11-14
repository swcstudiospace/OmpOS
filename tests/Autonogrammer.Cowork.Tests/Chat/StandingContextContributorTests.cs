using Autonogrammer.Cowork.Chat;
using Autonogrammer.Cowork.Instructions;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>
/// Tests StandingContextContributor: the paragraph bound, the session's own block as the first-prompt resource, memory.backend off for Cowork only,
/// and the planner path with identical data for Code.
/// </summary>
public sealed class StandingContextContributorTests
{
    private const string GlobalCanary = "global-canary-rule";
    private const string ProjectCanary = "project-canary-rule";
    private const string BriefCanary = "memory-canary-note";

    [Fact]
    public void CoworkSurfaceGetsTheSessionBlockParagraphAtMost2KiBAndMemoryBackendOff()
    {
        var c = new StandingContextContributor(StandingContextBuilder.Build(GlobalCanary, ProjectCanary, null, BriefCanary));
        var req = new LaunchRequest { Surface = "cowork", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };

        var ct = c.Contribute(req, TestContext.Current.CancellationToken);

        ct.SystemPromptParagraph.ShouldNotBeNullOrWhiteSpace();
        System.Text.Encoding.UTF8.GetByteCount(ct.SystemPromptParagraph).ShouldBeLessThanOrEqualTo(2 * 1024);
        ct.FirstPromptBlock.ShouldContain(GlobalCanary);
        ct.FirstPromptBlock.ShouldContain(ProjectCanary);
        ct.FirstPromptBlock.ShouldContain(BriefCanary);
        ct.FirstPromptBlock.ShouldContain("<untrusted>");
        ct.OverlayEntries.ShouldContain(o => o.Contains("memory:") && o.Contains("backend: off"));
    }

    [Fact]
    public void CoworkSurfaceWithoutAContextHasNoBlockButStillTurnsOmpMemoryOff()
    {
        var c = new StandingContextContributor();
        var req = new LaunchRequest { Surface = "cowork", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };

        var ct = c.Contribute(req, TestContext.Current.CancellationToken);

        ct.FirstPromptBlock.ShouldBe(string.Empty);
        ct.OverlayEntries.ShouldContain(StandingContextContributor.MemoryOffOverlayEntry, StringComparer.Ordinal);
    }

    [Fact]
    public void CodeSurfaceGetsEmptyContribution()
    {
        var c = new StandingContextContributor(StandingContextBuilder.Build(GlobalCanary, ProjectCanary, null, BriefCanary));
        var req = new LaunchRequest { Surface = "code", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };

        var ct = c.Contribute(req, TestContext.Current.CancellationToken);

        ct.SystemPromptParagraph.ShouldBe(string.Empty);
        ct.FirstPromptBlock.ShouldBe(string.Empty);
        ct.OverlayEntries.ShouldBeEmpty();
    }

    [Fact]
    public void BlockIsByteStableAndContainsUntrustedFence()
    {
        var c = new StandingContextContributor(StandingContextBuilder.Build(GlobalCanary, ProjectCanary, null, BriefCanary));
        var req = new LaunchRequest { Surface = "cowork", SessionId = "s1", WorkingDirectory = "/w", TrustedFolders = Array.Empty<string>() };
        var ct1 = c.Contribute(req, TestContext.Current.CancellationToken);
        var ct2 = c.Contribute(req, TestContext.Current.CancellationToken);
        ct1.FirstPromptBlock.ShouldBe(ct2.FirstPromptBlock);
        ct1.FirstPromptBlock.ShouldContain("<untrusted>");
        ct1.FirstPromptBlock.ShouldContain("</untrusted>");
    }

    [Fact]
    public void CodePlanWithIdenticalDataCarriesNoCoworkBlockOrOverlay()
    {
        var settings = new AgentLoopSettings
        {
            PluginsRoot = string.Empty,
            StateRoot = Path.Combine(Path.GetTempPath(), "cowork-code-isolation"),
        };
        var contributors = new ILaunchContributor[]
        {
            new StandingContextContributor(StandingContextBuilder.Build(GlobalCanary, ProjectCanary, null, BriefCanary)),
        };

        var plan = OmpLaunchPlanner.Plan(settings, Array.Empty<ResolvedPlugin>(), "code-session", Path.GetFullPath("."), null, contributors: contributors, surface: "code");

        plan.OverlayEntries.ShouldBeEmpty();
        plan.SystemPromptParagraphs.ShouldBeEmpty();
        plan.FirstPromptBlocks.ShouldBeEmpty();
        plan.ConfigOverlayPath.ShouldBeNull();
        plan.ExtraArguments.ShouldNotContain("--config", StringComparer.Ordinal);
        string.Join('\n', plan.ExtraArguments).ShouldNotContain(GlobalCanary);
    }
}
