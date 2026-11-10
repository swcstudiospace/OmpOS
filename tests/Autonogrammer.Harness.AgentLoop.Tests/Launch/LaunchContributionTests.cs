using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.AgentLoop.Plugins;
using Autonogrammer.Harness.AgentLoop.Tests.Launch;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Launch;

/// <summary>
/// RED tests for the neutral ILaunchContributor seam and its generic application in OmpLaunchPlanner.
/// Byte-exact argv and overlay for one contributor; refusal above 2 KiB paragraph budget; multiple contributors compose without order dependence.
/// </summary>
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
        private readonly IReadOnlyList<string> _args;
        private readonly IReadOnlyDictionary<string, string?> _env;
        private readonly IReadOnlyList<string> _overlay;
        private readonly string _para;
        private readonly string _block;

        public TestContributor(
            IReadOnlyList<string>? args = null,
            IReadOnlyDictionary<string, string?>? env = null,
            IReadOnlyList<string>? overlay = null,
            string para = "",
            string block = "")
        {
            _args = args ?? Array.Empty<string>();
            _env = env ?? new Dictionary<string, string?>();
            _overlay = overlay ?? Array.Empty<string>();
            _para = para;
            _block = block;
        }

        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
        {
            return new LaunchContribution
            {
                Arguments = _args,
                Environment = _env,
                OverlayEntries = _overlay,
                SystemPromptParagraph = _para,
                FirstPromptBlock = _block,
                ProcessWrapper = null,
            };
        }
    }

    [Fact]
    public void OneContributorProducesByteExactArgvAndForcesOverlayWhenOverlayEntriesPresent()
    {
        var contrib = new TestContributor(
            args: new[] { "--cowork", "1" },
            overlay: new[] { "memory:\n  backend: off\n" }
        );

        var plan = OmpLaunchPlanner.Plan(
            Settings(),
            PlannerFixtures.AllReady().Take(0).ToList(), // no plugins
            "s1",
            "/work",
            null,
            contributors: new[] { contrib },
            surface: "cowork");

        // byte exact argv starts with base then contributed
        plan.ExtraArguments.ShouldBe(new[] { "--no-extensions", "--cowork", "1" });
        // overlay forced
        plan.ConfigOverlayPath.ShouldNotBeNull();
        plan.ConfigOverlayPath.ShouldContain("config-overlay");
    }

    [Fact]
    public void ContributorParagraphOver2KiBIsRefused()
    {
        var over = new string('x', OmpLaunchPlanner.ContributorParagraphBudgetBytes + 1);
        var contrib = new TestContributor(para: over);

        var ex = Should.Throw<ArgumentOutOfRangeException>(() =>
            OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { contrib }));

        ex.ParamName.ShouldBe("SystemPromptParagraph");
    }

    [Fact]
    public void MultipleContributorsComposeWithoutOrderDependence()
    {
        var c1 = new TestContributor(args: new[] { "a1" }, env: new Dictionary<string, string?> { ["E1"] = "v1" });
        var c2 = new TestContributor(args: new[] { "a2" }, env: new Dictionary<string, string?> { ["E2"] = "v2" });

        var p1 = OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { c1, c2 });
        var p2 = OmpLaunchPlanner.Plan(Settings(), Array.Empty<ResolvedPlugin>(), "s1", "/w", null, contributors: new[] { c2, c1 });

        p1.ExtraArguments.ShouldBe(p2.ExtraArguments);
        p1.Environment.Keys.OrderBy(k => k).ShouldBe(p2.Environment.Keys.OrderBy(k => k));
    }

    [Fact]
    public void ContributorReceivesCorrectLaunchRequestFields()
    {
        var received = new List<LaunchRequest>();
        var spy = new TestSpyContributor(r => received.Add(r));

        OmpLaunchPlanner.Plan(
            Settings(),
            Array.Empty<ResolvedPlugin>(),
            "sess-xyz",
            "/abs/work/dir",
            null,
            contributors: new[] { spy },
            surface: "test-surface",
            additionalDirectories: new[] { "/trusted" });

        received.Count.ShouldBe(1);
        received[0].Surface.ShouldBe("test-surface");
        received[0].SessionId.ShouldBe("sess-xyz");
        received[0].WorkingDirectory.ShouldBe("/abs/work/dir");
        received[0].TrustedFolders.ShouldContain("/trusted");
    }

    private sealed class TestSpyContributor : ILaunchContributor
    {
        private readonly Action<LaunchRequest> _onCall;
        public TestSpyContributor(Action<LaunchRequest> onCall) => _onCall = onCall;
        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken)
        {
            _onCall(request);
            return new LaunchContribution { Arguments = [], Environment = new Dictionary<string, string?>(), OverlayEntries = [], SystemPromptParagraph = "", FirstPromptBlock = "" };
        }
    }
}
