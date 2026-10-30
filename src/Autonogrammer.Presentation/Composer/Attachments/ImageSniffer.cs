namespace Autonogrammer.Presentation.Composer.Attachments;

/// <summary>Identifies the four allowed image formats by their leading bytes. File names and declared types are never trusted.</summary>
public static class ImageSniffer
{
    /// <summary>The MIME type of PNG.</summary>
    public const string Png = "image/png";

    /// <summary>The MIME type of JPEG.</summary>
    public const string Jpeg = "image/jpeg";

    /// <summary>The MIME type of GIF.</summary>
    public const string Gif = "image/gif";

    /// <summary>The MIME type of WebP.</summary>
    public const string WebP = "image/webp";

    /// <summary>Detects the format of an encoded image.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The MIME type, or <see langword="null"/> when the bytes are none of PNG, JPEG, GIF or WebP.</returns>
    public static string? Detect(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return Png;
        }

        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return Jpeg;
        }

        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
        {
            return Gif;
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return WebP;
        }

        return null;
    }
}
