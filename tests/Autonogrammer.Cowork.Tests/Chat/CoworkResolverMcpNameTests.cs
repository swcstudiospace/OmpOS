using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>An actuating call is decided by its ACP kind, so an MCP-shaped or cowork-shaped name cannot skip the shell and delete gates.</summary>
public sealed class CoworkResolverMcpNameTests
{
    [Fact]
    public async Task AnExecuteCallTitledAsAnMcpToolStillPassesTheShellAndDeleteGates()
    {
        using var world = new NameWorld();
        world.Write("""{"permissions":{"deny":["Shell(rm)"],"allow":["Mcp(github:list)"]}}""");
        var audit = new RunModeAuditLog();
        var resolver = world.Resolver(audit);
        var request = new PermissionRequestEvent(
            "call",
            "mcp__github__list",
            """{"toolCall":{"kind":"execute","title":"mcp__github__list","rawInput":{"command":"rm -rf ./x"}}}""",
            "req");

        (await resolver.ResolveAsync(request, CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(1);
        audit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkDeleteProtection && entry.Decision.Outcome == RunModeOutcome.Ask);
        audit.Snapshot().ShouldNotContain(entry => entry.Decision.Outcome == RunModeOutcome.Allow);
    }

    [Fact]
    public async Task AnExecuteCallWithACoworkNameStillPassesTheShellAndDeleteGates()
    {
        using var world = new NameWorld();
        var audit = new RunModeAuditLog();
        var resolver = world.Resolver(audit);
        var request = new PermissionRequestEvent(
            "call",
            "mcp__cowork_x",
            """{"toolCall":{"kind":"execute","title":"mcp__cowork_x","rawInput":{"command":"rm -rf /"}}}""",
            "req");

        (await resolver.ResolveAsync(request, CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(1);
        audit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkDeleteProtection && entry.Decision.Outcome == RunModeOutcome.Ask);
        audit.Snapshot().ShouldNotContain(entry => entry.Decision.Outcome == RunModeOutcome.Allow);
    }

    [Fact]
    public async Task ANonActuatingCallWithACoworkNameIsStillAllowed()
    {
        using var world = new NameWorld();
        var audit = new RunModeAuditLog();
        var resolver = world.Resolver(audit);
        var request = new PermissionRequestEvent(
            "call",
            "mcp__cowork_x",
            """{"toolCall":{"kind":"read","title":"mcp__cowork_x","rawInput":{}}}""",
            "req");

        (await resolver.ResolveAsync(request, CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
        audit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkAutoRule && entry.Decision.Outcome == RunModeOutcome.Allow);
    }

    private sealed class NameWorld : IDisposable
    {
        public NameWorld()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-mcp-name-" + Guid.NewGuid().ToString("N"));
            Home = Path.Combine(Root, "home");
            Work = Path.Combine(Root, "work");
            Directory.CreateDirectory(Path.Combine(Home, ".autonogrammer"));
            Directory.CreateDirectory(Work);
            Ask = new RefusingAsk();
        }

        public string Root { get; }

        public string Home { get; }

        public string Work { get; }

        public RefusingAsk Ask { get; }

        public void Write(string json) =>
            File.WriteAllText(Path.Combine(Home, ".autonogrammer", "permissions.json"), json);

        public CoworkPermissionResolver Resolver(RunModeAuditLog audit) =>
            new(Ask, new CoworkPathBoundary(Root, Home), [Work], storageFolder: null, Work, Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto, static () => false, audit: audit, permissionsHome: Home);

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RefusingAsk : IPermissionResolver
    {
        public int Calls { get; set; }

        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(PermissionDecision.Deny);
        }
    }
}
