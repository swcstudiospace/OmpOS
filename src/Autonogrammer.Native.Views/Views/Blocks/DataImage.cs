using Autonogrammer.GenUi;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>
/// Draws inline images. Only an allowed inline base64 data URI whose bytes start like the image type it declares is decoded;
/// every other source, and every payload that is not a complete image header, yields null, so no remote content is ever fetched
/// and a truncated payload never reaches the decoder.
/// </summary>
internal static class DataImage
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>Decodes an allowed inline base64 image data URI into a picture control.</summary>
    /// <param name="source">The image source text.</param>
    /// <returns>An image control that owns the decoded bitmap, or null when the source is not an allowed, decodable inline image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static Image? TryCreatePicture(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!A2uiSurfaceCatalog.IsAllowedImageSource(source))
        {
            return null;
        }

        var text = source.TrimStart();
        var comma = text.IndexOf(',', StringComparison.Ordinal);
        if (comma < 0 || !text[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(text[(comma + 1)..]);
        }
        catch (FormatException)
        {
            return null;
        }

        var mediaType = text["data:".Length..comma].Split(';')[0];
        if (!HasImageHeader(bytes, mediaType))
        {
            return null;
        }

        using var stream = new MemoryStream(bytes);
        var bitmap = new Bitmap(stream);

        // A decode that yields no pixels is not drawn either.
        if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
        {
            bitmap.Dispose();
            return null;
        }

        return new Image
        {
            Source = bitmap,
            Stretch = Stretch.Uniform,
            MaxWidth = BlockTokens.MaxImageWidth,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
    }

    /// <summary>Checks that the bytes begin with the header of the declared raster type, including the chunk or segment that carries its size.</summary>
    private static bool HasImageHeader(byte[] bytes, string mediaType) => mediaType switch
    {
        "image/png" => bytes.Length >= 33
            && bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature)
            && bytes.AsSpan(12, 4).SequenceEqual("IHDR"u8),
        "image/jpeg" => bytes.Length >= JpegSignature.Length && bytes.AsSpan(0, JpegSignature.Length).SequenceEqual(JpegSignature),
        "image/gif" => bytes.Length >= 6
            && (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
        "image/webp" => bytes.Length >= 12
            && bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
        _ => false,
    };
}
