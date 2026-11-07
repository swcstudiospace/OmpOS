using Autonogrammer.Harness.ComputerUse.Abstractions;

namespace Autonogrammer.Harness.ComputerUse.Frames;

/// <summary>
/// Masks a captured frame to granted windows. Defence in depth: the capture may already be clipped,
/// and this pass still blacks the desktop, ungranted windows and this process. It does not read a display.
/// </summary>
public static class VisibleRegionMasker
{
    /// <summary>Label stamped into every masked region.</summary>
    public const string Label = "masked";

    /// <summary>Returns a new image. Granted windows keep source pixels. Later windows cover earlier ones.</summary>
    /// <param name="source">Captured BGRA, origin at the display origin.</param>
    /// <param name="displayOrigin">Display origin in virtual-desktop pixels.</param>
    /// <param name="windowsBottomToTop">Windows, index 0 bottom-most.</param>
    /// <param name="grantedApps">Application identities the session may show.</param>
    /// <returns>The masked image.</returns>
    public static BgraImage Mask(
        BgraImage source,
        PixelRect displayOrigin,
        IReadOnlyList<WindowInfo> windowsBottomToTop,
        IReadOnlySet<string> grantedApps)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(windowsBottomToTop);
        ArgumentNullException.ThrowIfNull(grantedApps);
        byte[] pixels = new byte[source.Pixels.Length];

        Fill(pixels, 0, 0, 0, 255);
        Stamp(pixels, source.Width, source.Height, Math.Max(1, source.Width - 5), 1);

        for (int i = 0; i < windowsBottomToTop.Count; i++)
        {
            WindowInfo window = windowsBottomToTop[i];
            if (window.Minimized)
            {
                continue;
            }

            bool show = grantedApps.Contains(window.AppIdentity) && !window.IsOwnProcess;
            Blit(source, pixels, displayOrigin, window.Bounds, show);
        }

        return new BgraImage(source.Width, source.Height, pixels);
    }

    private static void Blit(BgraImage source, byte[] dest, PixelRect origin, PixelRect bounds, bool copySource)
    {
        int x0 = Math.Max(0, bounds.X - origin.X);
        int y0 = Math.Max(0, bounds.Y - origin.Y);
        int x1 = Math.Min(source.Width, bounds.X + bounds.Width - origin.X);
        int y1 = Math.Min(source.Height, bounds.Y + bounds.Height - origin.Y);
        if (x1 <= x0 || y1 <= y0)
        {
            return;
        }

        ReadOnlySpan<byte> sourcePixels = source.Pixels.Span;
        for (int y = y0; y < y1; y++)
        {
            for (int x = x0; x < x1; x++)
            {
                int i = ((y * source.Width) + x) * 4;
                if (copySource)
                {
                    dest[i] = sourcePixels[i];
                    dest[i + 1] = sourcePixels[i + 1];
                    dest[i + 2] = sourcePixels[i + 2];
                    dest[i + 3] = sourcePixels[i + 3];
                }
                else
                {
                    dest[i] = 0;
                    dest[i + 1] = 0;
                    dest[i + 2] = 0;
                    dest[i + 3] = 255;
                }
            }
        }

        if (!copySource)
        {
            Stamp(dest, source.Width, source.Height, x0 + 1, y0 + 1);
        }
    }

    private static void Fill(byte[] pixels, byte b, byte g, byte r, byte a)
    {
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = b;
            pixels[i + 1] = g;
            pixels[i + 2] = r;
            pixels[i + 3] = a;
        }
    }

    private static void Stamp(byte[] pixels, int width, int height, int x, int y)
    {
        ReadOnlySpan<byte> rows = [0b11111, 0b10001, 0b10001, 0b10001, 0b11111];
        for (int row = 0; row < rows.Length; row++)
        {
            for (int col = 0; col < 5; col++)
            {
                int px = x + col;
                int py = y + row;
                if (px < 0 || py < 0 || px >= width || py >= height)
                {
                    continue;
                }

                if ((rows[row] & (1 << (4 - col))) == 0)
                {
                    continue;
                }

                int i = ((py * width) + px) * 4;
                pixels[i] = 255;
                pixels[i + 1] = 255;
                pixels[i + 2] = 255;
                pixels[i + 3] = 255;
            }
        }
    }
}
