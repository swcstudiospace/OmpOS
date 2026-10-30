using Xunit;
using Autonogrammer.Contracts.Dispatch;
using Shouldly;

namespace Autonogrammer.Contracts.Tests.Dispatch;

/// <summary>The shared jitter seam stays inside the half-open unit interval.</summary>
public sealed class DispatchJitterTests
{
    /// <summary>System samples are in [0, 1).</summary>
    [Fact]
    public void SystemJitterStaysInHalfOpenUnitInterval()
    {
        var source = new SystemJitterSource();
        for (var i = 0; i < 64; i++)
        {
            var sample = source.NextDouble();
            sample.ShouldBeGreaterThanOrEqualTo(0d);
            sample.ShouldBeLessThan(1d);
        }
    }
}
