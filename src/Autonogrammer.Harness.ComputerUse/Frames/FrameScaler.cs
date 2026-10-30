namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>
/// Area-filter downscale to a long edge. <see cref="Abstractions.CoordinateMap"/> remains the only coordinate scale.
/// </summary>
public static class FrameScaler
{
    /// <summary>Downscales so the long edge is <paramref name="longEdgePx"/>. Smaller sources are copied.</summary>
    /// <param name="source">Source image.</param>
    /// <param name="longEdgePx">Target long edge.</param>
    /// <returns>The scaled image.</returns>
    public static BgraImage Downscale(BgraImage source, int longEdgePx)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(longEdgePx);
        (int width, int height) = TargetSize(source.Width, source.Height, longEdgePx);
        if (width == source.Width && height == source.Height)
        {
            return new BgraImage(source.Width, source.Height, source.Pixels.ToArray());
        }

        byte[] pixels = new byte[width * height * 4];
        ReadOnlySpan<byte> sourcePixels = source.Pixels.Span;
        for (int y = 0; y < height; y++)
        {
            int y0 = y * source.Height / height;
            int y1 = Math.Max(y0 + 1, (y + 1) * source.Height / height);
            for (int x = 0; x < width; x++)
            {
                int x0 = x * source.Width / width;
                int x1 = Math.Max(x0 + 1, (x + 1) * source.Width / width);
                Average(source, sourcePixels, pixels, ((y * width) + x) * 4, x0, x1, y0, y1);
            }
        }

        return new BgraImage(width, height, pixels);
    }

    /// <summary>Target size with the long edge exact and aspect rounded to a pixel.</summary>
    /// <param name="sourceWidth">Source width.</param>
    /// <param name="sourceHeight">Source height.</param>
    /// <param name="longEdgePx">Target long edge.</param>
    /// <returns>The target size.</returns>
    public static (int Width, int Height) TargetSize(int sourceWidth, int sourceHeight, int longEdgePx)
    {
        int longEdge = Math.Max(sourceWidth, sourceHeight);
        if (longEdge <= longEdgePx)
        {
            return (sourceWidth, sourceHeight);
        }

        if (sourceWidth >= sourceHeight)
        {
            int height = Math.Max(1, (int)Math.Round(sourceHeight * (longEdgePx / (double)sourceWidth)));
            return (longEdgePx, height);
        }

        int width = Math.Max(1, (int)Math.Round(sourceWidth * (longEdgePx / (double)sourceHeight)));
        return (width, longEdgePx);
    }

    private static void Average(BgraImage source, ReadOnlySpan<byte> sourcePixels, byte[] dest, int at, int x0, int x1, int y0, int y1)
    {
        long b = 0;
        long g = 0;
        long r = 0;
        long a = 0;
        int count = 0;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int i = ((y * source.Width) + x) * 4;
                b += sourcePixels[i];
                g += sourcePixels[i + 1];
                r += sourcePixels[i + 2];
                a += sourcePixels[i + 3];
                count++;
            }
        }

        dest[at] = (byte)(b / count);
        dest[at + 1] = (byte)(g / count);
        dest[at + 2] = (byte)(r / count);
        dest[at + 3] = (byte)(a / count);
    }
}
