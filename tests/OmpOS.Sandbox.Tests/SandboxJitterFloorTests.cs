using Shouldly;
using Xunit;

namespace OmpOS.Sandbox.Tests;

/// <summary>
/// Deterministic lower-bound tests for equal-jitter teardown backoff (WR-07).
/// </summary>
public sealed class SandboxJitterFloorTests
{
    /// <summary>
    /// The smallest possible draw yields exactly half the base delay; the floor is never undercut (WR-07).
    /// </summary>
    [Fact]
    public void EqualJitter_ZeroDraw_YieldsExactlyHalfBase()
    {
        var delay = SandboxLifecycle.ComputeEqualJitterDelay(1000, new FixedDrawRandom(0.0));

        delay.ShouldBe(TimeSpan.FromMilliseconds(500));
    }

    /// <summary>
    /// A mid-range draw lands halfway between the floor and the full base (WR-07).
    /// </summary>
    [Fact]
    public void EqualJitter_HalfDraw_YieldsThreeQuartersBase()
    {
        var delay = SandboxLifecycle.ComputeEqualJitterDelay(1000, new FixedDrawRandom(0.5));

        delay.ShouldBe(TimeSpan.FromMilliseconds(750));
    }

    /// <summary>
    /// Even a draw arbitrarily close to 1 stays at or below the full base and never below half (WR-07).
    /// </summary>
    [Fact]
    public void EqualJitter_MaxDraw_StaysWithinUpperHalf()
    {
        var delay = SandboxLifecycle.ComputeEqualJitterDelay(1000, new FixedDrawRandom(0.999_999));

        delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(500));
        delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(1000));
    }
}
