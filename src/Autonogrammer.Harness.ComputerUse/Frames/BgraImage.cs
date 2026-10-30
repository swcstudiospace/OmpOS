namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>Top-down BGRA pixels. Tests build these without a display.</summary>
public sealed class BgraImage
{
    private readonly byte[] _pixels;

    /// <summary>Creates an image. <paramref name="pixels"/> is copied.</summary>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="pixels">BGRA bytes, row-major, tightly packed.</param>
    public BgraImage(int width, int height, byte[] pixels)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (pixels.Length != width * height * 4)
        {
            throw new ArgumentException("BGRA length must be width*height*4.", nameof(pixels));
        }

        Width = width;
        Height = height;
        _pixels = pixels;
    }

    /// <summary>Width in pixels.</summary>
    public int Width { get; }

    /// <summary>Height in pixels.</summary>
    public int Height { get; }

    /// <summary>Tightly packed BGRA bytes.</summary>
    public ReadOnlyMemory<byte> Pixels => _pixels;

    /// <summary>Reads one pixel.</summary>
    /// <param name="x">X.</param>
    /// <param name="y">Y.</param>
    /// <returns>BGRA.</returns>
    public (byte B, byte G, byte R, byte A) Get(int x, int y)
    {
        int i = ((y * Width) + x) * 4;
        return (_pixels[i], _pixels[i + 1], _pixels[i + 2], _pixels[i + 3]);
    }
}
