using Autonogrammer.Cowork.Chat;
using Autonogrammer.Harness.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Tests.Chat;

/// <summary>Skip needs FilesystemIsolated or a per-session acknowledgement, and Auto maps to always-ask-cached.</summary>
public sealed class CoworkSkipAcknowledgementTests
{
    [Fact]
    public async Task SkipWithoutIsolationOrAcknowledgementIsManualAndReportsSkipNeedsAcknowledgement()
    {
        using var world = new SkipWorld();
        var resolver = world.Resolver(filesystemIsolated: false, skipAcknowledged: false);
        resolver.SkipUnavailableReason.ShouldBe(CoworkPermissionMode.SkipUnavailableReason);
        resolver.SkipUnavailableReason!.ShouldContain(CoworkPermissionMode.SkipNeedsAcknowledgement);
        (await resolver.ResolveAsync(world.Read(), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(1);

        var acknowledged = world.Resolver(filesystemIsolated: false, skipAcknowledged: true);
        acknowledged.SkipUnavailableReason.ShouldBeNull();
        world.Ask.Calls = 0;
        (await acknowledged.ResolveAsync(world.Read(), CancellationToken.None)).ShouldBe(PermissionDecision.Allow);
        world.Ask.Calls.ShouldBe(0);
    }

    [Fact]
    public void AutoMapsToAlwaysAskCached()
    {
        CoworkPermissionMode.ApprovalModeArgument(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Auto).ShouldBe("always-ask-cached");
        CoworkPermissionMode.ApprovalModeArgument(Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip).ShouldBe("always-ask");
    }

    private sealed class SkipWorld : IDisposable
    {
        public SkipWorld()
        {
            Root = Path.Combine(Path.GetTempPath(), "cowork-skip-" + Guid.NewGuid().ToString("N"));
            Work = Path.Combine(Root, "work");
            Directory.CreateDirectory(Work);
            Ask = new RecordingAsk();
        }

        public string Root { get; }

        public string Work { get; }

        public RecordingAsk Ask { get; }

        public CoworkPermissionResolver Resolver(bool filesystemIsolated, bool skipAcknowledged) =>
            new(Ask, new CoworkPathBoundary(Root, Root), [Work], null, Work, Autonogrammer.Cowork.Tools.CoworkPermissionMode.Skip, static () => false, filesystemIsolated, skipAcknowledged);

        public PermissionRequestEvent Read() =>
            new("call", "read", "{\"toolCall\":{\"kind\":\"read\",\"toolName\":\"read\",\"rawInput\":{\"path\":\"" + Path.Combine(Work, "note.txt").Replace("\\", "\\\\", StringComparison.Ordinal) + "\"}}}", "req");

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
