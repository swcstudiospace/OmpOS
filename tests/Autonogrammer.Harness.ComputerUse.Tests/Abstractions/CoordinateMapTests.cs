using Autonogrammer.Harness.ComputerUse.Abstractions;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Abstractions;

/// <summary>CoordinateMap is pure and is constructed from display records, never from a backend.</summary>
public sealed class CoordinateMapTests
{
    [Fact]
    public void RoundTripStaysWithinOnePixel()
    {
        DisplayInfo display = Display(id: "primary", x: 0, y: 0, width: 1920, height: 1080, scale: 1.25, primary: true);
        CoordinateMap map = new([display]);
        ModelPoint model = new() { X = 10.5, Y = 20.25 };
        NativePoint native = map.ScaleToNative(display.Id, model);
        ModelPoint back = map.ScaleToModel(display.Id, native);
        PixelDelta(model, back, display.ScaleFactor).ShouldBeLessThanOrEqualTo(1);
    }

    [Fact]
    public void NegativeVirtualDesktopOriginsStayOnThatDisplay()
    {
        DisplayInfo left = Display("left", x: -1920, y: 0, width: 1920, height: 1080, scale: 1, primary: false);
        CoordinateMap map = new([left]);
        NativePoint native = map.ScaleToNative(left.Id, new ModelPoint { X = 10, Y = 20 });
        native.X.ShouldBe(-1910);
        native.Y.ShouldBe(20);
        CoordinateMap.IsInside(left, native).ShouldBeTrue();
        CoordinateMap.IsInside(left, new NativePoint { X = 10, Y = 20 }).ShouldBeFalse();
    }

    [Fact]
    public void TwoDisplaysWithDifferentOffsetsDoNotSharePoints()
    {
        DisplayInfo left = Display("left", x: -1920, y: 0, width: 1920, height: 1080, scale: 1, primary: false);
        DisplayInfo right = Display("right", x: 0, y: 100, width: 1280, height: 800, scale: 1, primary: true);
        CoordinateMap map = new([left, right]);
        NativePoint onLeft = map.ScaleToNative("left", new ModelPoint { X = 10, Y = 10 });
        NativePoint onRight = map.ScaleToNative("right", new ModelPoint { X = 10, Y = 10 });
        CoordinateMap.IsInside(left, onLeft).ShouldBeTrue();
        CoordinateMap.IsInside(right, onLeft).ShouldBeFalse();
        onRight.X.ShouldBe(10);
        onRight.Y.ShouldBe(110);
        CoordinateMap.IsInside(right, onRight).ShouldBeTrue();
        CoordinateMap.IsInside(left, onRight).ShouldBeFalse();
    }

    [Fact]
    public void MacOsScaleFactorTwoMapsPointsToPixels()
    {
        DisplayInfo retina = Display("retina", x: 0, y: 0, width: 2000, height: 1000, scale: 2, primary: true);
        CoordinateMap map = new([retina]);
        ModelPoint points = new() { X = 10, Y = 20 };
        NativePoint pixels = map.ScaleToNative(retina.Id, points);
        pixels.X.ShouldBe(20);
        pixels.Y.ShouldBe(40);
        ModelPoint back = CoordinateMap.ScaleToModel(retina, pixels);
        back.X.ShouldBe(10);
        back.Y.ShouldBe(20);
        PixelDelta(points, back, retina.ScaleFactor).ShouldBeLessThanOrEqualTo(1);
        CoordinateMap.IsInside(retina, points).ShouldBeTrue();
    }

    private static double PixelDelta(ModelPoint left, ModelPoint right, double scale)
    {
        double dx = Math.Abs((left.X - right.X) * scale);
        double dy = Math.Abs((left.Y - right.Y) * scale);
        return Math.Max(dx, dy);
    }

    private static DisplayInfo Display(string id, int x, int y, int width, int height, double scale, bool primary) =>
        new()
        {
            Id = id,
            Bounds = new PixelRect { X = x, Y = y, Width = width, Height = height },
            ScaleFactor = scale,
            Primary = primary,
        };
}
