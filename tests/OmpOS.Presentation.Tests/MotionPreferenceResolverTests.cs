using Shouldly;
using Xunit;

namespace OmpOS.Presentation.Tests;

/// <summary>The animation-scale decision a head reads from the operating system (phase 25.7, M-22).</summary>
public sealed class MotionPreferenceResolverTests
{
    /// <summary>A scale of zero is the "Remove animations" setting, so motion is reduced.</summary>
    [Fact]
    public void ZeroScaleIsReduced()
    {
        MotionPreferenceResolver.FromAnimationDurationScale(0f).ShouldBe(MotionPreference.Reduced);
    }

    /// <summary>A negative scale is not a valid speed; it is treated as animations off.</summary>
    [Fact]
    public void NegativeScaleIsReduced()
    {
        MotionPreferenceResolver.FromAnimationDurationScale(-1f).ShouldBe(MotionPreference.Reduced);
    }

    /// <summary>Any positive scale, slowed, normal or fast, keeps full motion.</summary>
    [Theory]
    [InlineData(0.5f)]
    [InlineData(1f)]
    [InlineData(10f)]
    public void PositiveScaleIsFull(float scale)
    {
        MotionPreferenceResolver.FromAnimationDurationScale(scale).ShouldBe(MotionPreference.Full);
    }
}
