using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.AgentLoop.Safety;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>Auto runs an allow rule or a known-safe command, refuses a deny rule, and asks otherwise.</summary>
public sealed class CoworkAutoModeTests
{
    [Fact]
    public async Task AllowRunsDenyRefusesAndNeitherAsksWithCoworkStageNames()
    {
        using var world = new AutoWorld();
        world.WriteRules("""{"permissions":{"allow":["Shell(ls)"]}}""");
        var audit = new RunModeAuditLog();
        var allow = world.Resolver(audit);
        (await allow.ResolveAsync(world.Execute("ls"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
        audit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkAutoRule && entry.Decision.Outcome == RunModeOutcome.Allow);

        world.WriteRules("""{"permissions":{"deny":["Shell(ls)"]}}""");
        var denyAudit = new RunModeAuditLog();
        var deny = world.Resolver(denyAudit);
        (await deny.ResolveAsync(world.Execute("ls"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        denyAudit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkAutoRule && entry.Decision.Outcome == RunModeOutcome.Deny);

        world.WriteRules("""{"permissions":{"allow":[]}}""");
        var askAudit = new RunModeAuditLog();
        var ask = world.Resolver(askAudit);
        world.Ask.Calls = 0;
        (await ask.ResolveAsync(world.Execute("python script.py"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
        askAudit.Snapshot().ShouldContain(entry => entry.Decision.Stage == RunModeStage.CoworkAutoRule && entry.Decision.Outcome == RunModeOutcome.Ask);
    }

    [Fact]
    public async Task KnownSafeInsideTrustedFoldersRunsAndOutsideAsks()
    {
        using var world = new AutoWorld();
        var resolver = world.Resolver(new RunModeAuditLog());
        (await resolver.ResolveAsync(world.Execute("pwd"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);

        world.Ask.Calls = 0;
        (await resolver.ResolveAsync(world.Execute("ls " + world.Outside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task ProviderAndMcpAskInAutoAndATaintedSessionAsksForActuatingCalls()
    {
        using var world = new AutoWorld();
        var auto = world.Resolver(new RunModeAuditLog());
        (await auto.ResolveAsync(AutoWorld.Kind("other", "provider_tool"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        (await auto.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(2);

        var tainted = world.Resolver(new RunModeAuditLog(), tainted: true);
        world.Ask.Calls = 0;
        (await tainted.ResolveAsync(world.Execute("pwd"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
        tainted.SkipUnavailableReason.ShouldBeNull();
    }

    private static PermissionRequestEvent Mcp(string name) => new(
        "call",
        name,
        "{\"toolCall\":{\"kind\":\"other\",\"toolName\":\"" + name + "\",\"rawInput\":{}}}",
        "req");

    private sealed class AutoWorld : IDisposable
    {
        public AutoWorld()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-auto-" + Guid.NewGuid().ToString("N"));
            Home = Path.Combine(Root, "home");
            Work = Path.Combine(Root, "work");
            Directory.CreateDirectory(Path.Combine(Home, ".autonogrammer"));
            Directory.CreateDirectory(Work);
            Outside = Path.Combine(Root, "outside.txt");
            File.WriteAllText(Outside, "o");
            Ask = new RecordingAsk();
            Boundary = new CoworkPathBoundary(Root, Home);
        }

        public string Root { get; }

        public string Home { get; }

        public string Work { get; }

        public string Outside { get; }

        public RecordingAsk Ask { get; }

        public CoworkPathBoundary Boundary { get; }

        public void WriteRules(string json) =>
            File.WriteAllText(Path.Combine(Home, ".autonogrammer", "permissions.json"), json);

        public CoworkPermissionResolver Resolver(RunModeAuditLog audit, bool tainted = false) =>
            new(Ask, Boundary, [Work], storageFolder: null, Work, Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto, () => tainted, audit: audit, permissionsHome: Home);

        public PermissionRequestEvent Execute(string command)
        {
            var json = "{\"toolCall\":{\"kind\":\"execute\",\"toolName\":\"execute\",\"rawInput\":{\"command\":\"" + command.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\",\"cwd\":\"" + Work.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"}}}";
            return new PermissionRequestEvent("call", "execute", json, "req");
        }

        public static PermissionRequestEvent Kind(string kind, string name) =>
            new("call", name, "{\"toolCall\":{\"kind\":\"" + kind + "\",\"toolName\":\"" + name + "\",\"rawInput\":{}}}", "req");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class RecordingAsk : IPermissionResolver
    {
        public int Calls { get; set; }

        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(PermissionDecision.Allow);
        }
    }
}
