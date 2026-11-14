using Autonogrammer.Contracts.Dispatch;
using Shouldly;
using Xunit;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>The D-23 compare-and-set table, including the hub rule that completion is never self-applied.</summary>
public sealed class DispatchTransitionTests
{
    /// <summary>Every state pair matches the closed table. Replaying a completed command is a rejection.</summary>
    [Fact]
    public void TableMatchesClosedEdges()
    {
        var allowed = new HashSet<(DispatchCommandState From, DispatchCommandState To)>
        {
            (DispatchCommandState.Queued, DispatchCommandState.Delivered),
            (DispatchCommandState.Queued, DispatchCommandState.Expired),
            (DispatchCommandState.Queued, DispatchCommandState.Cancelled),
            (DispatchCommandState.Delivered, DispatchCommandState.Received),
            (DispatchCommandState.Delivered, DispatchCommandState.Expired),
            (DispatchCommandState.Delivered, DispatchCommandState.Cancelled),
            (DispatchCommandState.Delivered, DispatchCommandState.Queued),
            (DispatchCommandState.Received, DispatchCommandState.AwaitingApproval),
            (DispatchCommandState.Received, DispatchCommandState.Accepted),
            (DispatchCommandState.Received, DispatchCommandState.Failed),
            (DispatchCommandState.Received, DispatchCommandState.Expired),
            (DispatchCommandState.AwaitingApproval, DispatchCommandState.Accepted),
            (DispatchCommandState.AwaitingApproval, DispatchCommandState.Failed),
            (DispatchCommandState.Accepted, DispatchCommandState.Running),
            (DispatchCommandState.Accepted, DispatchCommandState.Failed),
            (DispatchCommandState.Running, DispatchCommandState.Completed),
            (DispatchCommandState.Running, DispatchCommandState.Failed),
            (DispatchCommandState.Running, DispatchCommandState.Cancelled),
        };

        foreach (var from in Enum.GetValues<DispatchCommandState>())
        {
            foreach (var to in Enum.GetValues<DispatchCommandState>())
            {
                DispatchTransitions.Allows(from, to).ShouldBe(allowed.Contains((from, to)));
            }
        }

        DispatchTransitions.Allows(DispatchCommandState.Completed, DispatchCommandState.Running).ShouldBeFalse();
        DispatchTransitions.HubMayApply(DispatchCommandState.Running, DispatchCommandState.Completed).ShouldBeFalse();
        DispatchTransitions.HubMayApply(DispatchCommandState.Queued, DispatchCommandState.Delivered).ShouldBeTrue();
        DispatchTransitions.HubMayApply(DispatchCommandState.Delivered, DispatchCommandState.Queued).ShouldBeFalse();
    }
}
