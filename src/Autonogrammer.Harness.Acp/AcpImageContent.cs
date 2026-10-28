using System.Globalization;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Builds ACP <c>image</c> content blocks: <c>{"type":"image","data":"&lt;base64&gt;","mimeType":"image/png"}</c>. The shape is the one omp's ACP agent
/// reads (<c>#convertPromptBlocks</c> in <c>modes/acp/acp-agent.ts</c> takes <c>block.data</c> and <c>block.mimeType</c>, and it advertises
/// <c>promptCapabilities.image: true</c>). An image that breaks a limit or has another type is left out and named in a note.
/// </summary>
public static class AcpImageContent
{
    private static readonly string[] AllowedTypes = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    /// <summary>Builds the image blocks.</summary>
    /// <param name="images">The images in attachment order.</param>
    /// <param name="limits">The limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The image blocks, then one text note when any image was left out.</returns>
    public static IReadOnlyList<AcpPromptBlock> Build(IReadOnlyList<AcpPromptImage> images, AcpImageLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(images);

        var effective = limits ?? new AcpImageLimits();
        var blocks = new List<AcpPromptBlock>();
        var omitted = new List<string>();
        long used = 0;
        foreach (var image in images)
        {
            ArgumentNullException.ThrowIfNull(image, nameof(images));
            var fits = blocks.Count < effective.MaxImages
                && image.Data.Length > 0
                && image.Data.Length <= effective.MaxImageBytes
                && used + image.Data.Length <= effective.MaxTotalBytes
                && AllowedTypes.Contains(image.MimeType, StringComparer.Ordinal);
            if (!fits)
            {
                omitted.Add(image.Name);
                continue;
            }

            used += image.Data.Length;
            blocks.Add(AcpPromptBlock.ImageBlock(image.MimeType, Convert.ToBase64String(image.Data.Span)));
        }

        if (omitted.Count > 0)
        {
            var listed = string.Join(", ", omitted.Take(5));
            blocks.Add(AcpPromptBlock.TextBlock(string.Create(CultureInfo.InvariantCulture, $"[note: {omitted.Count} image(s) were not sent because of the image limits or type: {listed}]")));
        }

        return blocks;
    }
}
