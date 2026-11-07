using Autonogrammer.Cowork.Artifacts.Rendering;
using Shouldly;
using Xunit;

namespace Autonogrammer.Cowork.Artifacts.Tests.Rendering;

/// <summary>Magic, size and pixel caps. SVG is never an image.</summary>
public sealed class ImageValidatorTests
{
    [Fact]
    public void FakePngMagicFails()
    {
        var bytes = File.ReadAllBytes(Fixture("fake-png.bin"));
        var result = ImageValidator.Validate(bytes, "image/png", new ArtifactOptions().MaxImageBytes);
        result.IsValid.ShouldBeFalse();
        result.Code.ShouldBe("magic_mismatch");
    }

    [Fact]
    public void OversizedDimensionsFail()
    {
        var bytes = File.ReadAllBytes(Fixture("oversized-dims.bin"));
        var result = ImageValidator.Validate(bytes, "image/png", new ArtifactOptions().MaxImageBytes);
        result.IsValid.ShouldBeFalse();
        result.Code.ShouldBe("dimensions");
    }

    [Fact]
    public void PixelCapAndByteCapAndSvgFail()
    {
        var wide = Png(8000, 8000);
        var pixels = ImageValidator.Validate(wide, "image/png", new ArtifactOptions().MaxImageBytes);
        pixels.Code.ShouldBe("pixels");

        var options = new ArtifactOptions { MaxImageBytes = 8 };
        var size = ImageValidator.Validate(Png(1, 1), "image/png", options.MaxImageBytes);
        size.Code.ShouldBe("too_large");

        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"></svg>"u8.ToArray();
        var never = ImageValidator.Validate(svg, "image/png", new ArtifactOptions().MaxImageBytes);
        never.Code.ShouldBe("never_svg");
    }

    [Fact]
    public void SmallPngPasses()
    {
        var result = ImageValidator.Validate(Png(2, 3), "image/png", new ArtifactOptions().MaxImageBytes);
        result.IsValid.ShouldBeTrue();
        result.Width.ShouldBe(2);
        result.Height.ShouldBe(3);
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "hostile", "image", name);

    private static byte[] Png(int width, int height)
    {
        var bytes = new byte[24];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;
        bytes[12] = (byte)'I';
        bytes[13] = (byte)'H';
        bytes[14] = (byte)'D';
        bytes[15] = (byte)'R';
        WriteBe(bytes, 16, width);
        WriteBe(bytes, 20, height);
        return bytes;
    }

    private static void WriteBe(byte[] bytes, int offset, int value)
    {
        bytes[offset] = (byte)(value >> 24);
        bytes[offset + 1] = (byte)(value >> 16);
        bytes[offset + 2] = (byte)(value >> 8);
        bytes[offset + 3] = (byte)value;
    }
}
