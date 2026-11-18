using Autonogrammer.GenUi;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace Autonogrammer.Native.Views.Blocks;

/// <summary>
/// Draws inline images. Only an allowed inline base64 data URI whose bytes start like the image type it declares is decoded;
/// every other source, and every payload whose header is not complete, yields null, so no remote content is ever fetched and a
/// truncated header never reaches the decoder. A payload the decoder rejects also yields null.
/// </summary>
internal static class DataImage
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    /// <summary>The production decoder: Avalonia's codec. Tests inject a replacement through <see cref="TryCreatePicture"/>.</summary>
    private static readonly Func<Stream, Bitmap> DefaultDecoder = static stream => new Bitmap(stream);

    /// <summary>Decodes an allowed inline base64 image data URI into a picture control.</summary>
    /// <param name="source">The image source text.</param>
    /// <param name="decode">Decodes the validated bytes. Null uses the production codec.</param>
    /// <returns>An image control that owns the decoded bitmap, or null when the source is not an allowed, decodable inline image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static Image? TryCreatePicture(string source, Func<Stream, Bitmap>? decode = null)
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

        return CreatePicture(bytes, decode ?? DefaultDecoder);
    }

    /// <summary>Decodes the bytes into a picture control. Returns null when the decoder rejects them or they decode to no pixels; otherwise the returned image owns the bitmap.</summary>
    private static Image? CreatePicture(byte[] bytes, Func<Stream, Bitmap> decode)
    {
        using var stream = new MemoryStream(bytes);
        Bitmap? bitmap = null;
        try
        {
            try
            {
                bitmap = decode(stream);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
            {
                return null;
            }

            // A decode that yields no pixels is not drawn either.
            if (bitmap.PixelSize.Width <= 0 || bitmap.PixelSize.Height <= 0)
            {
                return null;
            }

            var image = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Uniform,
                MaxWidth = BlockTokens.MaxImageWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            // The image now owns the bitmap, so the finally block must not dispose it.
            bitmap = null;
            return image;
        }
        finally
        {
            bitmap?.Dispose();
        }
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
