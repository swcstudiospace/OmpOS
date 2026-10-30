using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// The only mapping from session lifecycle, live runtime status and the closed flag to a rail row status (46-UI-SPEC, Status mapping).
/// Every lifecycle is covered, with and without a live status and with and without a closed engine.
/// </summary>
public sealed class AgentRowStatusMapTests
{
    /// <summary>Gets one case for every lifecycle and live-status combination the precedence rules distinguish.</summary>
    public static TheoryData<SessionLifecycle, RuntimeStatus?, bool, AgentRowStatus, RuntimeStatus?, AgentRowGroup> Cases => new()
    {
        { SessionLifecycle.Created, null, false, AgentRowStatus.Ready, RuntimeStatus.Queued, AgentRowGroup.Idle },
        { SessionLifecycle.Created, null, true, AgentRowStatus.Ready, RuntimeStatus.Queued, AgentRowGroup.Idle },
        { SessionLifecycle.Created, RuntimeStatus.Running, false, AgentRowStatus.Running, RuntimeStatus.Running, AgentRowGroup.Running },
        { SessionLifecycle.Created, RuntimeStatus.Queued, false, AgentRowStatus.Queued, RuntimeStatus.Queued, AgentRowGroup.Running },
        { SessionLifecycle.Running, null, false, AgentRowStatus.Running, RuntimeStatus.Running, AgentRowGroup.Running },
        { SessionLifecycle.Running, RuntimeStatus.NeedsAttention, false, AgentRowStatus.NeedsApproval, RuntimeStatus.NeedsAttention, AgentRowGroup.NeedsYou },
        { SessionLifecycle.Running, RuntimeStatus.AwaitingInput, false, AgentRowStatus.WaitingForYou, RuntimeStatus.AwaitingInput, AgentRowGroup.NeedsYou },
        { SessionLifecycle.Running, RuntimeStatus.Paused, false, AgentRowStatus.Paused, RuntimeStatus.Paused, AgentRowGroup.Idle },
        { SessionLifecycle.Running, RuntimeStatus.Completed, false, AgentRowStatus.Completed, RuntimeStatus.Completed, AgentRowGroup.Finished },
        { SessionLifecycle.Running, RuntimeStatus.Failed, false, AgentRowStatus.Failed, RuntimeStatus.Failed, AgentRowGroup.Finished },
        { SessionLifecycle.Running, RuntimeStatus.Unreachable, false, AgentRowStatus.Lost, RuntimeStatus.Unreachable, AgentRowGroup.Lost },
        { SessionLifecycle.Paused, null, false, AgentRowStatus.Paused, RuntimeStatus.Paused, AgentRowGroup.Idle },
        { SessionLifecycle.Paused, null, true, AgentRowStatus.Closed, RuntimeStatus.Paused, AgentRowGroup.Idle },
        { SessionLifecycle.Paused, RuntimeStatus.NeedsAttention, false, AgentRowStatus.Paused, RuntimeStatus.Paused, AgentRowGroup.Idle },
        { SessionLifecycle.Completed, null, false, AgentRowStatus.Completed, RuntimeStatus.Completed, AgentRowGroup.Finished },
        { SessionLifecycle.Completed, null, true, AgentRowStatus.Closed, RuntimeStatus.Completed, AgentRowGroup.Idle },
        { SessionLifecycle.Failed, null, false, AgentRowStatus.Failed, RuntimeStatus.Failed, AgentRowGroup.Finished },
        { SessionLifecycle.Failed, null, true, AgentRowStatus.Failed, RuntimeStatus.Failed, AgentRowGroup.Finished },
        { SessionLifecycle.Lost, null, false, AgentRowStatus.Lost, RuntimeStatus.Unreachable, AgentRowGroup.Lost },
        { SessionLifecycle.Lost, RuntimeStatus.Running, false, AgentRowStatus.Lost, RuntimeStatus.Unreachable, AgentRowGroup.Lost },
        { (SessionLifecycle)99, null, false, AgentRowStatus.Unknown, null, AgentRowGroup.Idle },
        { SessionLifecycle.Running, (RuntimeStatus)99, false, AgentRowStatus.Unknown, null, AgentRowGroup.Idle },
    };

    /// <summary>The mapping follows the precedence rules for every case.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void LifecycleLiveStatusAndClosedMapToOneRowStatus(
        SessionLifecycle lifecycle,
        RuntimeStatus? live,
        bool closed,
        AgentRowStatus expectedStatus,
        RuntimeStatus? expectedRuntime,
        AgentRowGroup expectedGroup)
    {
        var mapping = AgentRowStatusMap.Resolve(lifecycle, live, closed);

        mapping.Status.ShouldBe(expectedStatus);
        mapping.RuntimeStatus.ShouldBe(expectedRuntime);
        mapping.Group.ShouldBe(expectedGroup);
    }

    /// <summary>Each status reads as its words and glyph, and an unknown status reads Status unknown with the ring.</summary>
    [Theory]
    [InlineData(AgentRowStatus.NeedsApproval, "Needs approval", StatusIcon.Alert)]
    [InlineData(AgentRowStatus.WaitingForYou, "Waiting for you", StatusIcon.Bubble)]
    [InlineData(AgentRowStatus.Running, "Running", StatusIcon.Running)]
    [InlineData(AgentRowStatus.Queued, "Queued", StatusIcon.Clock)]
    [InlineData(AgentRowStatus.Lost, "Lost", StatusIcon.Question)]
    [InlineData(AgentRowStatus.Ready, "Ready", StatusIcon.Ring)]
    [InlineData(AgentRowStatus.Paused, "Paused", StatusIcon.Paused)]
    [InlineData(AgentRowStatus.Closed, "Closed", StatusIcon.Closed)]
    [InlineData(AgentRowStatus.Completed, "Completed", StatusIcon.Check)]
    [InlineData(AgentRowStatus.Failed, "Failed", StatusIcon.Cross)]
    [InlineData(AgentRowStatus.Unknown, "Status unknown", StatusIcon.Ring)]
    public void EachStatusHasWordsAndAGlyph(AgentRowStatus status, string text, string icon)
    {
        AgentRowStatusMap.TextOf(status).ShouldBe(text);
        AgentRowStatusMap.IconOf(status).ShouldBe(icon);
    }

    /// <summary>A status value from a future build reads Status unknown and never throws.</summary>
    [Fact]
    public void AFutureStatusValueReadsAsUnknown()
    {
        AgentRowStatusMap.TextOf((AgentRowStatus)77).ShouldBe("Status unknown");
        AgentRowStatusMap.IconOf((AgentRowStatus)77).ShouldBe(StatusIcon.Ring);
    }

    /// <summary>The group headers read as the spec words them.</summary>
    [Theory]
    [InlineData(AgentRowGroup.NeedsYou, "Needs you")]
    [InlineData(AgentRowGroup.Running, "Running")]
    [InlineData(AgentRowGroup.Lost, "Lost")]
    [InlineData(AgentRowGroup.Idle, "Idle")]
    [InlineData(AgentRowGroup.Finished, "Finished")]
    public void GroupHeadersReadAsTheSpecWordsThem(AgentRowGroup group, string header)
    {
        AgentRowStatusMap.HeaderOf(group).ShouldBe(header);
    }
}
