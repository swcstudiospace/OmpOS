using OmpOS.Contracts.Agents;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;
using OmpOS.Harness.AgentLoop.Safety;
using OmpOS.Native.Engine;
using OmpOS.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Composition;

/// <summary>What the head adds to an engine: the MCP servers of the user's files after the plan's, the hook gate, and the approval kinds of the run mode. The peer is scripted: no omp is started.</summary>
public sealed class EngineCompositionOptionsTests
{
    private static OmpLaunchPlan Plan(params string[] serverNames) => new()
    {
        ExtraArguments = [],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = [.. serverNames.Select(static name => new AgentMcpServerSpec { Name = name, Url = new Uri("http://127.0.0.1:1/mcp"), Headers = new Dictionary<string, string>(StringComparer.Ordinal) })],
        WorkingDirectory = "/work",
        PlanningEvents = [],
    };

    private static AcpHttpMcpServer Server(string name) => new(name, new Uri("http://127.0.0.1:2/mcp"), new Dictionary<string, string>(StringComparer.Ordinal));

    private static async Task<AcpClientOptions> LaunchAsync(EngineCompositionOptions? options, OmpLaunchPlan plan, CancellationToken cancellationToken)
    {
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var engine = EngineComposition.Compose(["read"], plan, omp.TransportFactory, options: options);
        await using var host = engine.Host.ConfigureAwait(true);
        await engine.Host.ConnectAsync(cancellationToken).ConfigureAwait(true);
        return omp.Launches.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task WithoutOptionsTheServersAreThePlansOnly()
    {
        var launch = await LaunchAsync(null, Plan("substrate"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        launch.McpServers.Select(static server => server.Name).ShouldBe(["substrate"]);
        launch.HookGate.ShouldBeNull();
    }

    [Fact]
    public async Task TheUsersServersFollowThePlansAndThePlanWinsANameClash()
    {
        var options = new EngineCompositionOptions { ExtraMcpServers = [Server("substrate"), Server("github"), Server("github")] };

        var launch = await LaunchAsync(options, Plan("substrate"), TestContext.Current.CancellationToken).ConfigureAwait(true);

        launch.McpServers.Select(static server => server.Name).ShouldBe(["substrate", "github"]);
        ((AcpHttpMcpServer)launch.McpServers[0]).Url.Port.ShouldBe(1);
    }

    [Fact]
    public async Task RunEverythingAdmitsEveryToolKindAndTheOtherModesKeepTheAllowedOnes()
    {
        var ct = TestContext.Current.CancellationToken;
        var all = new RunModePolicy(RunMode.RunEverything, EffectivePermissions.Empty, EffectiveSandbox.Default, "/work", null);
        var ask = new RunModePolicy(RunMode.AutoReview, EffectivePermissions.Empty, EffectiveSandbox.Default, "/work", null);
        var everything = await LaunchAsync(new EngineCompositionOptions { RunModePolicy = () => all }, Plan(), ct).ConfigureAwait(true);
        var reviewed = await LaunchAsync(new EngineCompositionOptions { RunModePolicy = () => ask }, Plan(), ct).ConfigureAwait(true);

        everything.ApprovalPolicy.Allows(new AcpPermissionRequest("c", "fetch", "fetch", "{}")).ShouldBeTrue();
        reviewed.ApprovalPolicy.Allows(new AcpPermissionRequest("c", "fetch", "fetch", "{}")).ShouldBeFalse();
        reviewed.ApprovalPolicy.Allows(new AcpPermissionRequest("c", "read", "read", "{}")).ShouldBeTrue();
    }
}
