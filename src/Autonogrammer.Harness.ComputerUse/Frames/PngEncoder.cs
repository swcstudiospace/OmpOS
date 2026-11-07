using System.IO.Compression;
using System.Text;
using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>PNG encoder on <see cref="ZLibStream"/>. No JPEG and no extra package.</summary>
public static class PngEncoder
{
    private static readonly byte[] Signature = [137, 80, 78, 71, 13, 10, 26, 10];

    /// <summary>Encodes RGBA PNG, re-scaling by 0.85 up to four times when over <paramref name="maxImageBytes"/>.</summary>
    /// <param name="image">BGRA image.</param>
    /// <param name="maxImageBytes">Byte cap.</param>
    /// <returns>The encode result.</returns>
    public static PngEncodeResult Encode(BgraImage image, int maxImageBytes)
    {
        ArgumentNullException.ThrowIfNull(image);
        BgraImage current = image;
        for (int step = 0; step <= 4; step++)
        {
            byte[] png = EncodeOnce(current);
            if (png.Length <= maxImageBytes)
            {
                return new PngEncodeResult(png, null, step);
            }

            if (step == 4)
            {
                return new PngEncodeResult(default, DenialCode.FrameTooLarge, 4);
            }

            int longEdge = Math.Max(current.Width, current.Height);
            int next = Math.Max(1, (int)Math.Floor(longEdge * 0.85));
            current = FrameScaler.Downscale(current, next);
        }

        return new PngEncodeResult(default, DenialCode.FrameTooLarge, 4);
    }

    private static byte[] EncodeOnce(BgraImage image)
    {
        byte[] scan = new byte[image.Height * (1 + (image.Width * 4))];
        int at = 0;
        ReadOnlySpan<byte> pixels = image.Pixels.Span;
        for (int y = 0; y < image.Height; y++)
        {
            scan[at++] = 0;
            for (int x = 0; x < image.Width; x++)
            {
                int i = ((y * image.Width) + x) * 4;
                scan[at++] = pixels[i + 2];
                scan[at++] = pixels[i + 1];
                scan[at++] = pixels[i];
                scan[at++] = pixels[i + 3];
            }
        }

        byte[] idat = Deflate(scan);
        using MemoryStream output = new();
        output.Write(Signature);
        WriteChunk(output, "IHDR", Header(image.Width, image.Height));
        WriteChunk(output, "IDAT", idat);
        WriteChunk(output, "IEND", []);
        return output.ToArray();
    }

    private static byte[] Header(int width, int height)
    {
        byte[] data = new byte[13];
        WriteInt(data, 0, width);
        WriteInt(data, 4, height);
        data[8] = 8;
        data[9] = 6;
        return data;
    }

    private static byte[] Deflate(byte[] scan)
    {
        using MemoryStream raw = new();
        using (ZLibStream zlib = new(raw, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            zlib.Write(scan);
        }

        return raw.ToArray();
    }

    private static void WriteChunk(Stream output, string type, byte[] data)
    {
        byte[] length = new byte[4];
        WriteInt(length, 0, data.Length);
        output.Write(length);
        byte[] name = Encoding.ASCII.GetBytes(type);
        output.Write(name);
        output.Write(data);
        byte[] crc = new byte[4];
        WriteInt(crc, 0, Crc(name, data));
        output.Write(crc);
    }

    private static int Crc(byte[] name, byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        crc = Update(crc, name);
        crc = Update(crc, data);
        return (int)(crc ^ 0xFFFFFFFF);
    }

    private static uint Update(uint crc, byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            crc ^= data[i];
            for (int bit = 0; bit < 8; bit++)
            {
                uint mask = (crc & 1) == 1 ? 0xEDB88320u : 0;
                crc = (crc >> 1) ^ mask;
            }
        }

        return crc;
    }

    private static void WriteInt(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)(value >> 24);
        buffer[offset + 1] = (byte)(value >> 16);
        buffer[offset + 2] = (byte)(value >> 8);
        buffer[offset + 3] = (byte)value;
    }
}
