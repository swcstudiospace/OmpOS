using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.MacOS.Tests;

/// <summary>Capture pixels come from CoordinateMap once. Retina is factor 2. A 1x display is factor 1.</summary>
public sealed class CoordinateScaleTests
{
    [Fact]
    public void RetinaPointsTimesScaleFactorAreCapturePixels()
    {
        DisplayInfo display = Display(2);
        var points = new ModelPoint { X = 10, Y = 20 };
        NativePoint pixels = ScreenCaptureArguments.ToCapturePixels(display, points);
        NativePoint authority = CoordinateMap.ScaleToNative(display, points);
        pixels.ShouldBe(authority);
        pixels.X.ShouldBe(20);
        pixels.Y.ShouldBe(40);
    }

    [Fact]
    public void FactorOneLeavesPointsUnchanged()
    {
        DisplayInfo display = Display(1);
        var points = new ModelPoint { X = 10, Y = 20 };
        NativePoint pixels = ScreenCaptureArguments.ToCapturePixels(display, points);
        pixels.ShouldBe(CoordinateMap.ScaleToNative(display, points));
        pixels.X.ShouldBe(10);
        pixels.Y.ShouldBe(20);
    }

    [Fact]
    public void ScaleIsNotAppliedASecondTime()
    {
        DisplayInfo display = Display(2);
        var points = new ModelPoint { X = 10, Y = 20 };
        NativePoint once = ScreenCaptureArguments.ToCapturePixels(display, points);
        var alreadyPixels = new ModelPoint { X = once.X, Y = once.Y };
        NativePoint twice = CoordinateMap.ScaleToNative(display, alreadyPixels);
        twice.X.ShouldBe(40);
        once.X.ShouldNotBe(twice.X);
    }

    private static DisplayInfo Display(double scale)
    {
        return new DisplayInfo
        {
            Id = "main",
            Bounds = new PixelRect { X = 0, Y = 0, Width = 800, Height = 600 },
            ScaleFactor = scale,
            Primary = true,
        };
    }
}
