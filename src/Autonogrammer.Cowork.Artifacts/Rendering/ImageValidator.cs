namespace Autonogrammer.Cowork.Artifacts.Rendering;


/// <summary>Validates PNG, JPEG, GIF and WebP container headers against <c>Cowork:Artifacts:MaxImageBytes</c>.</summary>
public static class ImageValidator
{
    /// <summary>Header-only image check. Pixels are never decoded, and SVG is never an image.</summary>
    internal sealed class ImageValidation
    {
        private ImageValidation(bool isValid, string? code, int width, int height)
        {
            IsValid = isValid;
            Code = code;
            Width = width;
            Height = height;
        }

        /// <summary>Gets a value indicating whether the bytes match the declared raster type and the caps.</summary>
        public bool IsValid { get; }

        /// <summary>Gets the refusal code when <see cref="IsValid"/> is false.</summary>
        public string? Code { get; }

        /// <summary>Gets the declared width when the header was read.</summary>
        public int Width { get; }

        /// <summary>Gets the declared height when the header was read.</summary>
        public int Height { get; }

        internal static ImageValidation Ok(int width, int height) => new(true, null, width, height);

        internal static ImageValidation Fail(string code) => new(false, code, 0, 0);
    }

    /// <summary>Maximum width or height, in pixels.</summary>
    public const int MaxDimension = 8192;

    /// <summary>Maximum pixel count (50 megapixels).</summary>
    public const long MaxPixels = 50_000_000;

    private static ReadOnlySpan<byte> PngSignature => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Checks magic number, dimensions and size. Does not decode pixel data.</summary>
    /// <param name="bytes">The image bytes.</param>
    /// <param name="declaredMediaType">The declared type: image/png, image/jpeg, image/gif or image/webp.</param>
    /// <param name="maxImageBytes">The <c>Cowork:Artifacts:MaxImageBytes</c> cap.</param>
    /// <returns>A validation result. SVG bytes fail as <c>never_svg</c>.</returns>
    internal static ImageValidation Validate(ReadOnlySpan<byte> bytes, string declaredMediaType, int maxImageBytes)
    {
        ArgumentNullException.ThrowIfNull(declaredMediaType);
        if (bytes.Length > maxImageBytes)
        {
            return ImageValidation.Fail("too_large");
        }

        if (IsSvg(bytes, declaredMediaType))
        {
            return ImageValidation.Fail("never_svg");
        }

        var type = declaredMediaType.Trim();
        if (!TryRead(bytes, type, out var width, out var height, out var code))
        {
            return ImageValidation.Fail(code);
        }

        if (width > MaxDimension || height > MaxDimension)
        {
            return ImageValidation.Fail("dimensions");
        }

        if ((long)width * height > MaxPixels)
        {
            return ImageValidation.Fail("pixels");
        }

        return ImageValidation.Ok(width, height);
    }

    private static bool IsSvg(ReadOnlySpan<byte> bytes, string declaredMediaType)
    {
        if (declaredMediaType.Contains("svg", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var head = bytes.Length > 512 ? bytes[..512] : bytes;
        var text = System.Text.Encoding.UTF8.GetString(head).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
        return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && text.Contains("<svg", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryRead(ReadOnlySpan<byte> bytes, string type, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (type.Equals("image/png", StringComparison.OrdinalIgnoreCase))
        {
            return TryPng(bytes, out width, out height, out code);
        }

        if (type.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) || type.Equals("image/jpg", StringComparison.OrdinalIgnoreCase))
        {
            return TryJpeg(bytes, out width, out height, out code);
        }

        if (type.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return TryGif(bytes, out width, out height, out code);
        }

        if (type.Equals("image/webp", StringComparison.OrdinalIgnoreCase))
        {
            return TryWebp(bytes, out width, out height, out code);
        }

        code = "unsupported";
        return false;
    }

    private static bool TryPng(ReadOnlySpan<byte> bytes, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 24 || !bytes.StartsWith(PngSignature))
        {
            code = "magic_mismatch";
            return false;
        }

        if (bytes[12] != (byte)'I' || bytes[13] != (byte)'H' || bytes[14] != (byte)'D' || bytes[15] != (byte)'R')
        {
            code = "magic_mismatch";
            return false;
        }

        width = ReadBe32(bytes[16..]);
        height = ReadBe32(bytes[20..]);
        if (width <= 0 || height <= 0)
        {
            code = "dimensions";
            return false;
        }

        code = "";
        return true;
    }

    private static bool TryGif(ReadOnlySpan<byte> bytes, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 10 || !IsGif(bytes))
        {
            code = "magic_mismatch";
            return false;
        }

        width = bytes[6] | (bytes[7] << 8);
        height = bytes[8] | (bytes[9] << 8);
        if (width <= 0 || height <= 0)
        {
            code = "dimensions";
            return false;
        }

        code = "";
        return true;
    }

    private static bool IsGif(ReadOnlySpan<byte> bytes) =>
        bytes[0] == (byte)'G' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F'
        && bytes[3] == (byte)'8' && (bytes[4] == (byte)'7' || bytes[4] == (byte)'9') && bytes[5] == (byte)'a';

    private static bool TryWebp(ReadOnlySpan<byte> bytes, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 16 || !IsRiffWebp(bytes))
        {
            code = "magic_mismatch";
            return false;
        }

        if (bytes.Length >= 30 && Match(bytes, 12, "VP8X"))
        {
            width = 1 + bytes[24] + (bytes[25] << 8) + (bytes[26] << 16);
            height = 1 + bytes[27] + (bytes[28] << 8) + (bytes[29] << 16);
            code = width > 0 && height > 0 ? "" : "dimensions";
            return code.Length == 0;
        }

        code = "magic_mismatch";
        return false;
    }

    private static bool IsRiffWebp(ReadOnlySpan<byte> bytes) =>
        Match(bytes, 0, "RIFF") && Match(bytes, 8, "WEBP");

    private static bool TryJpeg(ReadOnlySpan<byte> bytes, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (bytes.Length < 4 || bytes[0] != 0xFF || bytes[1] != 0xD8)
        {
            code = "magic_mismatch";
            return false;
        }

        var index = 2;
        var limit = Math.Min(bytes.Length, 65536);
        while (index + 8 < limit)
        {
            if (bytes[index] != 0xFF)
            {
                code = "magic_mismatch";
                return false;
            }

            while (index < limit && bytes[index] == 0xFF)
            {
                index++;
            }

            if (index >= limit)
            {
                break;
            }

            var marker = bytes[index];
            index++;
            if (marker is 0xD8 or 0xD9 or (>= 0xD0 and <= 0xD7))
            {
                continue;
            }

            if (index + 1 >= limit)
            {
                break;
            }

            var length = (bytes[index] << 8) | bytes[index + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                return TrySof(bytes, index, out width, out height, out code);
            }

            if (length < 2)
            {
                code = "magic_mismatch";
                return false;
            }

            index += length;
        }

        code = "truncated_header";
        return false;
    }
    private static bool TrySof(ReadOnlySpan<byte> bytes, int index, out int width, out int height, out string code)
    {
        width = 0;
        height = 0;
        if (index + 7 >= bytes.Length)
        {
            code = "truncated_header";
            return false;
        }

        height = (bytes[index + 3] << 8) | bytes[index + 4];
        width = (bytes[index + 5] << 8) | bytes[index + 6];
        if (width <= 0 || height <= 0)
        {
            code = "dimensions";
            return false;
        }

        code = "";
        return true;
    }


    private static int ReadBe32(ReadOnlySpan<byte> bytes) =>
        (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];

    private static bool Match(ReadOnlySpan<byte> bytes, int offset, string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (bytes[offset + index] != (byte)text[index])
            {
                return false;
            }
        }

        return true;
    }
}
