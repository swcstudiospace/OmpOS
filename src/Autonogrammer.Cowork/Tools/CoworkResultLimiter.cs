using Autonogrammer.Cowork.Abstractions;

namespace Autonogrammer.Cowork.Tools;

/// <summary>Turns an over-cap result into a fixed-text failure. It never truncates.</summary>
public static class CoworkResultLimiter
{
    /// <summary>The most characters of text a result may carry.</summary>
    public const int MaxTextChars = 65536;

    /// <summary>The most images a result may carry.</summary>
    public const int MaxImages = 2;

    /// <summary>The most bytes in one image.</summary>
    public const int MaxImageBytes = 3 * 1024 * 1024;

    /// <summary>The most bytes in all images together.</summary>
    public const int MaxTotalImageBytes = 6 * 1024 * 1024;

    /// <summary>The fixed failure the model reads when a cap is exceeded.</summary>
    public const string CappedMessage = "The tool result exceeded a size cap.";

    /// <summary>
    /// Applies the caps.
    /// </summary>
    /// <param name="result">The provider result.</param>
    /// <returns>The same result when it fits, otherwise a failure. Nothing is truncated.</returns>
    public static CoworkToolResult Apply(CoworkToolResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var textChars = 0;
        var images = 0;
        long imageBytes = 0;
        foreach (var block in result.Content)
        {
            switch (block)
            {
                case CoworkTextBlock text:
                    textChars += text.Text.Length;
                    break;
                case CoworkImageBlock image:
                    images++;
                    imageBytes += image.Data.Length;
                    if (!IsAllowedMime(image.MimeType) || image.Data.Length > MaxImageBytes)
                    {
                        return CoworkToolResult.Failure(CappedMessage);
                    }

                    break;
                default:
                    return CoworkToolResult.Failure(CappedMessage);
            }
        }

        if (textChars > MaxTextChars || images > MaxImages || imageBytes > MaxTotalImageBytes)
        {
            return CoworkToolResult.Failure(CappedMessage);
        }

        return result;
    }

    private static bool IsAllowedMime(string mimeType) =>
        mimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
        || mimeType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase)
        || mimeType.Equals("image/webp", StringComparison.OrdinalIgnoreCase);
}
