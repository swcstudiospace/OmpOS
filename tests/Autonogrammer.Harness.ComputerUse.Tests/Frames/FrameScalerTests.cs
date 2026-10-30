using Autonogrammer.Harness.ComputerUse.Frames;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Frames;

/// <summary>Area-filter downscale preserves aspect within one pixel.</summary>
public sealed class FrameScalerTests
{
    [Fact]
    public void DefaultLongEdgeIs1280AndOverrideIs2576()
    {
        BgraImage wide = Solid(2560, 1440);
        BgraImage scaled = FrameScaler.Downscale(wide, 1280);
        scaled.Width.ShouldBe(1280);
        scaled.Height.ShouldBe(720);
        AspectWithinOnePixel(2560, 1440, scaled.Width, scaled.Height).ShouldBeTrue();

        BgraImage larger = Solid(4000, 2000);
        BgraImage model = FrameScaler.Downscale(larger, 2576);
        model.Width.ShouldBe(2576);
        model.Height.ShouldBe(1288);
        AspectWithinOnePixel(4000, 2000, model.Width, model.Height).ShouldBeTrue();
        model.Get(0, 0).B.ShouldBe((byte)7);
    }

    private static bool AspectWithinOnePixel(int sw, int sh, int dw, int dh)
    {
        int idealH = (int)Math.Round(sh * (dw / (double)sw));
        int idealW = (int)Math.Round(sw * (dh / (double)sh));
        return Math.Abs(idealH - dh) <= 1 || Math.Abs(idealW - dw) <= 1;
    }

    private static BgraImage Solid(int width, int height)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = 7;
            pixels[i + 3] = 255;
        }

        return new BgraImage(width, height, pixels);
    }
}
