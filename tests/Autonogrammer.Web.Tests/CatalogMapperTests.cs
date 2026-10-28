using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Web.Services;
using Shouldly;
using Xunit;

namespace Autonogrammer.Web.Tests;

/// <summary>The API's rail status survives the trip through <see cref="SessionSummary"/> and <see cref="AgentRowStatusMap"/>: the web rail shows what the API says.</summary>
public sealed class CatalogMapperTests
{
    /// <summary>Every API row status resolves to the same rail status through the Presentation mapping.</summary>
    /// <param name="status">The API status.</param>
    /// <param name="expected">The rail status.</param>
    /// <param name="group">The rail group.</param>
    [Theory]
    [InlineData(CatalogRowStatus.NeedsApproval, AgentRowStatus.NeedsApproval, AgentRowGroup.NeedsYou)]
    [InlineData(CatalogRowStatus.WaitingForYou, AgentRowStatus.WaitingForYou, AgentRowGroup.NeedsYou)]
    [InlineData(CatalogRowStatus.Running, AgentRowStatus.Running, AgentRowGroup.Running)]
    [InlineData(CatalogRowStatus.Queued, AgentRowStatus.Queued, AgentRowGroup.Running)]
    [InlineData(CatalogRowStatus.Lost, AgentRowStatus.Lost, AgentRowGroup.Lost)]
    [InlineData(CatalogRowStatus.Ready, AgentRowStatus.Ready, AgentRowGroup.Idle)]
    [InlineData(CatalogRowStatus.Paused, AgentRowStatus.Paused, AgentRowGroup.Idle)]
    [InlineData(CatalogRowStatus.Closed, AgentRowStatus.Closed, AgentRowGroup.Idle)]
    [InlineData(CatalogRowStatus.Completed, AgentRowStatus.Completed, AgentRowGroup.Finished)]
    [InlineData(CatalogRowStatus.Failed, AgentRowStatus.Failed, AgentRowGroup.Finished)]
    [InlineData(CatalogRowStatus.Unknown, AgentRowStatus.Unknown, AgentRowGroup.Idle)]
    public void RowStatusResolvesToTheSameRailStatus(CatalogRowStatus status, AgentRowStatus expected, AgentRowGroup group)
    {
        var entry = Entry("s-1", status, closed: status == CatalogRowStatus.Closed);
        var summary = CatalogMapper.ToSummary(entry);

        var resolved = AgentRowStatusMap.Resolve(summary.Lifecycle, CatalogMapper.LiveStatusOf(status), entry.Closed);

        resolved.Status.ShouldBe(expected);
        resolved.Group.ShouldBe(group);
    }

    /// <summary>The title is the first prompt on one line, cut at 48 characters; a terminal and an empty session have their own titles.</summary>
    [Fact]
    public void TitlesComeFromThePromptTheKindOrTheId()
    {
        var long47 = new string('x', 60);
        CatalogMapper.TitleOf(Entry("abcdef0123456789", CatalogRowStatus.Ready, [new Message { Role = "user", Content = "  Fix   the\n failing   test ", Timestamp = DateTimeOffset.UnixEpoch }]))
            .ShouldBe("Fix the failing test");
        CatalogMapper.TitleOf(Entry("abcdef0123456789", CatalogRowStatus.Ready, [new Message { Role = "user", Content = long47, Timestamp = DateTimeOffset.UnixEpoch }]))
            .ShouldBe(new string('x', CatalogMapper.MaxTitleChars) + "\u2026");
        CatalogMapper.TitleOf(Entry("abcdef0123456789", CatalogRowStatus.Ready)).ShouldBe("Agent abcdef01");
        CatalogMapper.TitleOf(Entry("abcdef0123456789", CatalogRowStatus.Running, kind: CatalogSessionKind.Terminal)).ShouldBe("omp terminal abcdef01");
        CatalogMapper.TitleOf(Entry("short", CatalogRowStatus.Ready, [new Message { Role = "assistant", Content = "not a prompt", Timestamp = DateTimeOffset.UnixEpoch }]))
            .ShouldBe("Agent short");
    }

    /// <summary>The kind maps to the Presentation session kind; a running or approval status means a run is active.</summary>
    [Fact]
    public void KindAndRunActivityAreMapped()
    {
        CatalogMapper.ToSummary(Entry("t", CatalogRowStatus.Running, kind: CatalogSessionKind.Terminal)).Kind.ShouldBe(SessionKind.Terminal);
        CatalogMapper.ToSummary(Entry("c", CatalogRowStatus.Ready, kind: CatalogSessionKind.Cloud)).Kind.ShouldBe(SessionKind.Chat);
        CatalogMapper.IsRunActive(SessionStatus.Running).ShouldBeTrue();
        CatalogMapper.IsRunActive(SessionStatus.AwaitingToolApproval).ShouldBeTrue();
        CatalogMapper.IsRunActive(SessionStatus.Completed).ShouldBeFalse();
    }

    private static SessionCatalogEntry Entry(string id, CatalogRowStatus status, IReadOnlyList<Message>? messages = null, bool closed = false, CatalogSessionKind kind = CatalogSessionKind.Agent) => new()
    {
        Id = id,
        OwnerId = "o",
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
        Status = SessionStatus.Idle,
        Messages = messages ?? [],
        Events = [],
        Kind = kind,
        RowStatus = status,
        Group = CatalogRowGroup.Idle,
        LastActivityAt = DateTimeOffset.UnixEpoch,
        Closed = closed,
    };
}
