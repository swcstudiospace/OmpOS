using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>The resolver is the second governance seam: folders, deletes, modes and taint.</summary>
public sealed class CoworkPermissionResolverTests
{
    [Theory]
    [InlineData("read", false)]
    [InlineData("edit", false)]
    [InlineData("delete", true)]
    [InlineData("move", false)]
    [InlineData("search", false)]
    [InlineData("execute", false)]
    public async Task ManualAsksInsideAndDeniesOutside(string kind, bool deleteAsks)
    {
        using var world = new World();
        var resolver = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual);
        var inside = await resolver.ResolveAsync(world.Request(kind, world.Inside), CancellationToken.None);
        var outside = await resolver.ResolveAsync(world.Request(kind, world.Outside), CancellationToken.None);

        inside.ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
        outside.ShouldBe(PermissionDecision.Deny);
        world.Ask.Calls.ShouldBe(1);
        _ = deleteAsks;
    }

    [Fact]
    public async Task AutoBehavesLikeManualAndSkipAllowsInsideExceptDelete()
    {
        using var world = new World();
        var auto = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto);
        (await auto.ResolveAsync(world.Request("read", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);

        world.Ask.Calls = 0;
        var skip = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, filesystemIsolated: true);
        (await skip.ResolveAsync(world.Request("read", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
        (await skip.ResolveAsync(world.Request("delete", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
        (await skip.ResolveAsync(world.Request("read", world.Outside), CancellationToken.None)).ShouldBe(PermissionDecision.Deny);
    }

    [Fact]
    public async Task SkipWithoutIsolationOrAcknowledgementIsManualAndSaysWhy()
    {
        using var world = new World();
        var resolver = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip);
        resolver.SkipUnavailableReason.ShouldBe(CoworkPermissionMode.SkipUnavailableReason);
        (await resolver.ResolveAsync(world.Request("read", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);

        var acknowledged = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, skipAcknowledged: true);
        acknowledged.SkipUnavailableReason.ShouldBeNull();
        world.Ask.Calls = 0;
        (await acknowledged.ResolveAsync(world.Request("edit", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("delete")]
    [InlineData("move")]
    [InlineData("execute")]
    public async Task ATaintedSessionTreatsActuatingKindsAsManualEvenInSkip(string kind)
    {
        using var world = new World();
        var resolver = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, filesystemIsolated: true, tainted: true);
        (await resolver.ResolveAsync(world.Request(kind, world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
        (await resolver.ResolveAsync(world.Request("read", world.Inside), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task CoworkToolsAreAllowedAndCatalogMcpAsksWhenTainted()
    {
        using var world = new World();
        var skip = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, filesystemIsolated: true, tainted: true, catalog: ["github"]);
        (await skip.ResolveAsync(Mcp("mcp__cowork__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
        (await skip.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);

        world.Ask.Calls = 0;
        var untainted = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, filesystemIsolated: true, catalog: ["github"]);
        (await untainted.ResolveAsync(Mcp("mcp__github__list"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("think")]
    [InlineData("switch_mode")]
    public async Task ThinkAndSwitchModeAreAllowed(string kind)
    {
        using var world = new World();
        var manual = world.Resolver(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual);
        (await manual.ResolveAsync(KindOnly(kind), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual, true)]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto, true)]
    [InlineData(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, false)]
    public async Task FetchAndOtherFollowTheMode(Autonogrammer.Cowork.Tools.CoworkPermissionMode mode, bool asks)
    {
        using var world = new World();
        var resolver = world.Resolver(mode, filesystemIsolated: true);
        (await resolver.ResolveAsync(KindOnly("fetch"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        (await resolver.ResolveAsync(KindOnly("other"), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(asks ? 2 : 0);
    }

    [Fact]
    public void TheStoredModeKeyDefaultsToManual()
    {
        CoworkPermissionMode.SettingKey.ShouldBe("cowork.permissionMode");
        CoworkPermissionMode.Default.ShouldBe(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Manual);
    }

    private static PermissionRequestEvent Mcp(string name) => new(
        "call",
        name,
        "{\"toolCall\":{\"kind\":\"other\",\"toolName\":\"" + name + "\",\"rawInput\":{}}}",
        "req");

    private static PermissionRequestEvent KindOnly(string kind) => new(
        "call",
        kind,
        "{\"toolCall\":{\"kind\":\"" + kind + "\",\"toolName\":\"" + kind + "\",\"rawInput\":{}}}",
        "req");

    private sealed class World : IDisposable
    {
        public World()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-resolver-" + Guid.NewGuid().ToString("N"));
            State = Path.Combine(Root, "state");
            Home = Path.Combine(Root, "home");
            Work = Path.Combine(State, "work");
            Storage = Path.Combine(State, "Storage");
            Directory.CreateDirectory(Work);
            Directory.CreateDirectory(Storage);
            Directory.CreateDirectory(Home);
            Inside = Path.Combine(Work, "note.txt");
            Outside = Path.Combine(Root, "outside.txt");
            File.WriteAllText(Inside, "n");
            File.WriteAllText(Outside, "o");
        }

        public string Root { get; }

        public string State { get; }

        public string Home { get; }

        public string Work { get; }

        public string Storage { get; }

        public string Inside { get; }

        public string Outside { get; }

        public RecordingAsk Ask { get; } = new();

        public CoworkPermissionResolver Resolver(
            Autonogrammer.Cowork.Tools.CoworkPermissionMode mode,
            bool filesystemIsolated = false,
            bool skipAcknowledged = false,
            bool tainted = false,
            IReadOnlyCollection<string>? catalog = null) =>
            new(
                Ask,
                new CoworkPathBoundary(State, Home),
                [Work],
                Storage,
                Work,
                mode,
                () => tainted,
                filesystemIsolated,
                skipAcknowledged,
                catalog);

        public PermissionRequestEvent Request(string kind, string path)
        {
            _ = Root;
            var json = string.Equals(kind, "execute", StringComparison.Ordinal)
                ? "{\"toolCall\":{\"kind\":\"execute\",\"toolName\":\"execute\",\"rawInput\":{\"command\":\"ls\",\"cwd\":\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"}}}"
                : "{\"toolCall\":{\"kind\":\"" + kind + "\",\"toolName\":\"" + kind + "\",\"rawInput\":{\"path\":\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal) + "\"}}}";
            return new PermissionRequestEvent("call", kind, json, "req");
        }

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
