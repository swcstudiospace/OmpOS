using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Frames;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Frames;

/// <summary>Masking keeps granted windows and blacks the rest. No display is opened.</summary>
public sealed class VisibleRegionMaskerTests
{
    [Fact]
    public void TopWindowOccludesAndUngrantedOwnAndDesktopAreBlackWithALabel()
    {
        BgraImage source = Solid(40, 30, 10, 20, 200, 255);
        WindowInfo lower = Window("lower", "granted.app", 0, 0, 30, 30, own: false);
        WindowInfo top = Window("top", "other.app", 15, 0, 20, 20, own: false);
        WindowInfo own = Window("own", "granted.app", 0, 20, 12, 8, own: true);
        HashSet<string> grants = new(StringComparer.Ordinal) { "granted.app" };
        BgraImage masked = VisibleRegionMasker.Mask(source, new PixelRect { X = 0, Y = 0, Width = 40, Height = 30 }, [lower, top, own], grants);

        (byte b, byte g, byte r, byte a) = masked.Get(1, 1);
        b.ShouldBe((byte)10);
        g.ShouldBe((byte)20);
        r.ShouldBe((byte)200);
        a.ShouldBe((byte)255);
        masked.Get(22, 8).B.ShouldBe((byte)0);
        masked.Get(22, 8).R.ShouldBe((byte)0);
        HasLabel(masked, 16, 1).ShouldBeTrue();
        masked.Get(2, 22).B.ShouldBe((byte)0);
        HasLabel(masked, 1, 21).ShouldBeTrue();
        masked.Get(32, 10).B.ShouldBe((byte)0);
        HasLabel(masked, 35, 1).ShouldBeTrue();
    }

    private static bool HasLabel(BgraImage image, int x, int y)
    {
        (byte b, byte g, byte r, byte a) = image.Get(x, y);
        return b == 255 && g == 255 && r == 255 && a == 255;
    }

    private static BgraImage Solid(int width, int height, byte b, byte g, byte r, byte a)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }

        return new BgraImage(width, height, pixels);
    }

    private static WindowInfo Window(string id, string app, int x, int y, int width, int height, bool own) => new()
    {
        Id = id,
        Pid = 1,
        AppIdentity = app,
        Title = id,
        Bounds = new PixelRect { X = x, Y = y, Width = width, Height = height },
        Minimized = false,
        Focused = false,
        IsOwnProcess = own,
    };
}
