using System.IO.Compression;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;
using Autonogrammer.Harness.ComputerUse.Frames;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.ComputerUse.Tests.Frames;

/// <summary>PNG round-trip and the byte-cap loop. The reader is test-only.</summary>
public sealed class PngEncoderTests
{
    [Fact]
    public void RoundTripKeepsPixels()
    {
        byte[] pixels = new byte[4 * 2 * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i + 3);
        }

        BgraImage source = new(4, 2, pixels);
        PngEncodeResult encoded = PngEncoder.Encode(source, int.MaxValue);
        encoded.Denial.ShouldBeNull();
        BgraImage back = Read(encoded.Bytes.ToArray());
        back.Width.ShouldBe(4);
        back.Height.ShouldBe(2);
        back.Pixels.ToArray().ShouldBe(pixels);
    }

    [Fact]
    public void OversizedFrameReturnsFrameTooLargeAfterFourSteps()
    {
        byte[] pixels = new byte[8 * 8 * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(i * 17);
        }

        PngEncodeResult encoded = PngEncoder.Encode(new BgraImage(8, 8, pixels), maxImageBytes: 8);
        encoded.Denial.ShouldBe(DenialCode.FrameTooLarge);
        encoded.Bytes.IsEmpty.ShouldBeTrue();
        encoded.Reductions.ShouldBe(4);
    }

    private static BgraImage Read(byte[] png)
    {
        using MemoryStream stream = new(png);
        Span<byte> signature = stackalloc byte[8];
        stream.ReadExactly(signature);
        int width = 0;
        int height = 0;
        byte[]? idat = null;
        while (stream.Position < stream.Length)
        {
            int length = ReadInt(stream);
            byte[] type = new byte[4];
            stream.ReadExactly(type);
            byte[] data = new byte[length];
            stream.ReadExactly(data);
            stream.ReadExactly(new byte[4]);
            string name = Encoding.ASCII.GetString(type);
            if (string.Equals(name, "IHDR", StringComparison.Ordinal))
            {
                width = ReadInt(data, 0);
                height = ReadInt(data, 4);
            }
            else if (string.Equals(name, "IDAT", StringComparison.Ordinal))
            {
                idat = data;
            }
            else if (string.Equals(name, "IEND", StringComparison.Ordinal))
            {
                break;
            }
        }

        using MemoryStream compressed = new(idat!);
        using ZLibStream zlib = new(compressed, CompressionMode.Decompress);
        using MemoryStream raw = new();
        zlib.CopyTo(raw);
        byte[] scan = raw.ToArray();
        byte[] pixels = new byte[width * height * 4];
        int at = 0;
        for (int y = 0; y < height; y++)
        {
            at++;
            for (int x = 0; x < width; x++)
            {
                int i = ((y * width) + x) * 4;
                pixels[i + 2] = scan[at++];
                pixels[i + 1] = scan[at++];
                pixels[i] = scan[at++];
                pixels[i + 3] = scan[at++];
            }
        }

        return new BgraImage(width, height, pixels);
    }

    private static int ReadInt(Stream stream)
    {
        byte[] data = new byte[4];
        stream.ReadExactly(data);
        return ReadInt(data, 0);
    }

    private static int ReadInt(byte[] data, int offset) =>
        (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
}
