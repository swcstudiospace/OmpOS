using Autonogrammer.Harness.AgentLoop.Stages;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.AgentLoop.Tests.Stages;

/// <summary>
/// The run vocabulary maps the vendor's cancel word to the cancelled phase. Lookup ignores case, so the upper-case word the
/// vendor emits and the lower-case word of <c>swarm.live.v1</c> are the same entry; the historical <c>CANCELED</c> spelling is kept.
/// </summary>
public sealed class SwarmRunStatesTests
{
    [Theory]
    [InlineData("CANCELLED")]
    [InlineData("cancelled")]
    [InlineData("CANCELED")]
    public void TheVendorCancelWordsMapToTheCancelledPhase(string word) =>
        SwarmRunStates.Parse(word).ShouldBe(SwarmRunPhase.Cancelled);
}
