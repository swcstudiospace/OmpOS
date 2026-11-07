using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Windows.Tests;

/// <summary>Per-monitor DPI factors feed the virtual-desktop normaliser once.</summary>
public sealed class CoordinateScaleTests
{
    [Fact]
    public void FractionalDpiUsesTheSameNormaliser()
    {
        SendInputBuilder.AbsolutePoint scaled = SendInputBuilder.NormalizeAfterDpi(100, 40, 1.5, 0, 0, 1920, 1080);
        SendInputBuilder.AbsolutePoint physical = SendInputBuilder.NormalizeAbsolute(150, 60, 0, 0, 1920, 1080);
        scaled.ShouldBe(physical);
    }

    [Fact]
    public void FactorOneMatchesUnscaledPixels()
    {
        SendInputBuilder.AbsolutePoint scaled = SendInputBuilder.NormalizeAfterDpi(80, 20, 1, -10, 5, 1000, 500);
        SendInputBuilder.AbsolutePoint physical = SendInputBuilder.NormalizeAbsolute(80, 20, -10, 5, 1000, 500);
        scaled.ShouldBe(physical);
    }

    [Fact]
    public void MidpointFactorRoundsToEvenBeforeNormalising()
    {
        SendInputBuilder.AbsolutePoint scaled = SendInputBuilder.NormalizeAfterDpi(1, 1, 2.5, 0, 0, 100, 100);
        SendInputBuilder.AbsolutePoint physical = SendInputBuilder.NormalizeAbsolute(2, 2, 0, 0, 100, 100);
        scaled.ShouldBe(physical);
        scaled.X.ShouldBeGreaterThan(0);
        scaled.X.ShouldBeLessThan(65535);
    }
}
