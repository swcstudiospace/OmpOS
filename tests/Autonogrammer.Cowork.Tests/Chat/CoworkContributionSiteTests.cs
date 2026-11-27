using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>
/// Pins the Cowork application site for Phase 82 Task 2: <see cref="CoworkSessionProfile"/> strips the planner's own
/// <c>--config</c>, writes its own overlay via <c>RenderOverlay</c>, and exposes the gateway MCP server only when URL
/// and bearer are both set.
/// </summary>
public sealed class CoworkContributionSiteTests
{
    /// <summary>A contributor's overlay entry forces a planner "--config", which the profile strips in favour of its own.</summary>
    [Fact]
    public void PlannerConfigIsStrippedInFavourOfTheCoworkOverlay()
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var session = fixture.Directory("session");
        var profile = new CoworkSessionProfile(session, folder, [folder]);

        var plan = profile.Create(contributors: [new OverlayContributor()]);

        var overlay = SessionStateLayout.ConfigOverlayFile(Path.GetFullPath(session));
        plan.ExtraArguments.Count(argument => string.Equals(argument, "--config", StringComparison.Ordinal)).ShouldBe(1);
        plan.ExtraArguments[plan.ExtraArguments.Count - 1].ShouldBe(overlay);
        plan.ConfigOverlayPath.ShouldBe(overlay);
        plan.OverlayEntries.ShouldBe(["skills.customDirectories=/cowork-skills"]);
        CoworkSessionProfile.RenderOverlay(plan.OverlayEntries).ShouldContain("skills.customDirectories=/cowork-skills", Case.Sensitive);
    }

    /// <summary>The gateway server appears only when URL and bearer are both set; either one alone is refused.</summary>
    [Fact]
    public void GatewayServerNeedsBothUrlAndBearer()
    {
        using var fixture = new TempTree();
        var folder = fixture.Directory("work");
        var session = fixture.Directory("session");

        var urlOnly = Should.Throw<InvalidOperationException>(
            () => new CoworkSessionProfile(session, folder, [folder], gatewayUrl: new Uri("http://127.0.0.1:9/mcp")).Create());
        urlOnly.Message.ShouldContain("both a URL and a bearer", Case.Sensitive);

        var bearerOnly = Should.Throw<InvalidOperationException>(
            () => new CoworkSessionProfile(session, folder, [folder], gatewayBearer: "secret-bearer").Create());
        bearerOnly.Message.ShouldContain("both a URL and a bearer", Case.Sensitive);

        var both = new CoworkSessionProfile(
            session, folder, [folder], gatewayUrl: new Uri("http://127.0.0.1:9/mcp"), gatewayBearer: "secret-bearer").Create();
        both.McpServers.Count.ShouldBe(1);
        both.McpServers[0].Name.ShouldBe("cowork");

        var neither = new CoworkSessionProfile(session, folder, [folder]).Create();
        neither.McpServers.ShouldBeEmpty();
    }

    private sealed class OverlayContributor : ILaunchContributor
    {
        public LaunchContribution Contribute(LaunchRequest request, CancellationToken cancellationToken) => new()
        {
            Arguments = [],
            Environment = new Dictionary<string, string?>(StringComparer.Ordinal),
            OverlayEntries = ["skills.customDirectories=/cowork-skills"],
            SystemPromptParagraph = string.Empty,
            FirstPromptBlock = string.Empty,
        };
    }

    private sealed class TempTree : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), "cowork-site-" + Guid.NewGuid().ToString("N"));

        public void Dispose() => System.IO.Directory.Delete(_root, recursive: true);

        public string Directory(string name)
        {
            var path = Path.Combine(_root, name);
            System.IO.Directory.CreateDirectory(path);
            return path;
        }
    }
}
