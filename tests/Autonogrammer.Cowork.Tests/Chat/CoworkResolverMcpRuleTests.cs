using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>MCP allow and deny rules from the permission files decide catalog calls. The resolver does not invent a rule.</summary>
public sealed class CoworkResolverMcpRuleTests
{
    [Theory]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual)]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto)]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip)]
    public async Task ABlockedMcpRuleRefusesInEveryMode(Autonogrammer.Cowork.Tools.CoworkPermissionMode mode)
    {
        using var world = new RuleWorld();
        world.Write("""{"permissions":{"deny":["Mcp(github:list)"]}}""");
        var resolver = world.Resolver(mode);
        (await resolver.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual)]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto)]
    public async Task AnAllowRuleRunsInManualAndAuto(Autonogrammer.Cowork.Tools.CoworkPermissionMode mode)
    {
        using var world = new RuleWorld();
        world.Write("""{"permissions":{"allow":["Mcp(github:list)"]}}""");
        var resolver = world.Resolver(mode);
        (await resolver.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task WithNoRuleAutoAsksLikeManualAndDoesNotInventARule()
    {
        using var world = new RuleWorld();
        var auto = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto);
        var manual = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual);
        (await auto.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        (await manual.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(2);

        world.Ask.AllowNames.Add("provider_tool");
        world.Ask.Calls = 0;
        (await auto.ResolveAsync(Other("provider_tool"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        (await auto.ResolveAsync(Other("unknown_tool"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        (await manual.ResolveAsync(Other("provider_tool"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        (await manual.ResolveAsync(Other("unknown_tool"), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(4);
    }

    [Fact]
    public async Task DeleteAsksInManualAutoAndSkip()
    {
        foreach (var mode in new[] { Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual, Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto, Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip })
        {
            using var world = new RuleWorld();
            var resolver = world.Resolver(mode);
            (await resolver.ResolveAsync(world.Delete(), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
            world.Ask.Calls.ShouldBe(1);
        }
    }

    private static PermissionRequestEvent Mcp(string name) => new(
        "call",
        name,
        "{\"toolCall\":{\"kind\":\"other\",\"toolName\":\"" + name + "\",\"rawInput\":{}}}",
        "req");

    private static PermissionRequestEvent Other(string name) => new(
        "call",
        name,
        "{\"toolCall\":{\"kind\":\"other\",\"toolName\":\"" + name + "\",\"rawInput\":{}}}",
        "req");

    private sealed class RuleWorld : IDisposable
    {
        public RuleWorld()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-mcp-" + Guid.NewGuid().ToString("N"));
            Home = Path.Combine(Root, "home");
            Work = Path.Combine(Root, "work");
            Directory.CreateDirectory(Path.Combine(Home, ".autonogrammer"));
            Directory.CreateDirectory(Work);
            Ask = new SelectiveAsk();
        }

        public string Root { get; }

        public string Home { get; }

        public string Work { get; }

        public SelectiveAsk Ask { get; }

        public void Write(string json) => File.WriteAllText(Path.Combine(Home, ".autonogrammer", "permissions.json"), json);

        public CoworkPermissionResolver Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode mode) =>
            new(Ask, new CoworkPathBoundary(Root, Home), [Work], null, Work, mode, static () => false, filesystemIsolated: true, catalogServers: ["github"], permissionsHome: Home);

        public PermissionRequestEvent Delete() =>
            new("call", "delete", "{\"toolCall\":{\"kind\":\"delete\",\"toolName\":\"delete\",\"rawInput\":{\"path\":\"" + Path.Combine(Work, "note.txt").Replace("\\", "\\\\", StringComparison.Ordinal) + "\"}}}", "req");

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class SelectiveAsk : IPermissionResolver
    {
        public int Calls { get; set; }

        public HashSet<string> AllowNames { get; } = new(StringComparer.Ordinal);

        public Task<PermissionDecision> ResolveAsync(PermissionRequestEvent request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(AllowNames.Contains(request.Name) ? PermissionDecision.Allow : PermissionDecision.Deny);
        }
    }
}
