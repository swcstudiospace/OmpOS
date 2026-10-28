using System.Globalization;
using System.Text;

namespace Autonogrammer.Harness.Acp;

/// <summary>
/// Builds the content blocks of a <c>session/prompt</c> request. omp inlines embedded <c>resource</c> blocks but reduces
/// <c>resource_link</c> blocks to a title, so @-mentions are always sent embedded. Resource blocks come first and the user
/// text block last, except when the text starts with <c>/</c> (after leading whitespace): omp runs a leading slash command
/// from the first text block, so that text block is placed first and the resources follow it.
/// </summary>
public static class AcpPromptContent
{
    private const int MaxNamedOmissions = 5;

    /// <summary>Builds the prompt blocks.</summary>
    /// <param name="text">The user text.</param>
    /// <param name="resources">The resolved mention resources, in attachment order.</param>
    /// <param name="limits">The size limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The ordered content blocks.</returns>
    /// <exception cref="ArgumentException">A resource URI is blank, or the text is blank and there are no resources.</exception>
    public static IReadOnlyList<AcpPromptBlock> Build(string text, IReadOnlyList<AcpPromptResource> resources, AcpPromptLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resources);
        ValidateResources(resources);

        var hasText = !string.IsNullOrWhiteSpace(text);
        if (!hasText && resources.Count == 0)
        {
            throw new ArgumentException("A prompt needs text or at least one resource.", nameof(text));
        }

        var omitted = new List<string>();
        var resourceBlocks = BuildResourceBlocks(resources, limits ?? new AcpPromptLimits(), omitted);

        var blocks = new List<AcpPromptBlock>();
        var slashFirst = hasText && text.TrimStart().StartsWith('/');
        if (slashFirst)
        {
            blocks.Add(AcpPromptBlock.TextBlock(text));
        }

        blocks.AddRange(resourceBlocks);
        if (hasText && !slashFirst)
        {
            blocks.Add(AcpPromptBlock.TextBlock(text));
        }

        if (omitted.Count > 0)
        {
            blocks.Add(AcpPromptBlock.TextBlock(OmissionNote(omitted)));
        }

        return blocks;
    }

    private static void ValidateResources(IReadOnlyList<AcpPromptResource> resources)
    {
        foreach (var resource in resources)
        {
            ArgumentNullException.ThrowIfNull(resource, nameof(resources));
            if (string.IsNullOrWhiteSpace(resource.Location))
            {
                throw new ArgumentException("A prompt resource needs a non-blank location.", nameof(resources));
            }
        }
    }

    private static List<AcpPromptBlock> BuildResourceBlocks(IReadOnlyList<AcpPromptResource> resources, AcpPromptLimits effective, List<string> omitted)
    {
        var maxResources = Math.Max(0, effective.MaxResources);
        var maxResourceBytes = Math.Max(0, effective.MaxResourceBytes);
        var remaining = (long)Math.Max(0, effective.MaxTotalBytes);

        var resourceBlocks = new List<AcpPromptBlock>();
        for (var i = 0; i < resources.Count; i++)
        {
            var resource = resources[i];
            var body = resource.Text ?? string.Empty;
            var fullBytes = Encoding.UTF8.GetByteCount(body);
            if (i >= maxResources || (remaining <= 0 && fullBytes > 0))
            {
                omitted.Add(string.IsNullOrWhiteSpace(resource.DisplayName) ? resource.Location : resource.DisplayName);
                continue;
            }

            var allowed = (int)Math.Min(maxResourceBytes, remaining);
            var shown = body;
            var shownBytes = fullBytes;
            if (fullBytes > allowed)
            {
                shown = CutUtf8(body, allowed, out shownBytes);
            }

            if (shownBytes < fullBytes || resource.Truncated)
            {
                var total = Math.Max(resource.OriginalLength, fullBytes);
                shown = string.Create(CultureInfo.InvariantCulture, $"{shown}\n[truncated: showing {shownBytes} of {total} bytes]");
            }

            remaining -= shownBytes;
            resourceBlocks.Add(AcpPromptBlock.ResourceBlock(new AcpEmbeddedResource
            {
                Location = resource.Location,
                MimeType = string.IsNullOrWhiteSpace(resource.MimeType) ? "text/plain" : resource.MimeType,
                Text = shown,
            }));
        }

        return resourceBlocks;
    }

    /// <summary>Builds the <c>session/prompt</c> parameters.</summary>
    /// <param name="sessionId">The session the prompt belongs to.</param>
    /// <param name="text">The user text.</param>
    /// <param name="resources">The resolved mention resources.</param>
    /// <param name="limits">The size limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The prompt parameters.</returns>
    public static AcpPromptContentParams BuildParams(string sessionId, string text, IReadOnlyList<AcpPromptResource> resources, AcpPromptLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return new AcpPromptContentParams(sessionId, Build(text, resources, limits));
    }

    /// <summary>Builds the prompt blocks of a message that carries images. The text may be blank when an image is attached; the image blocks follow the text and resource blocks.</summary>
    /// <param name="text">The user text.</param>
    /// <param name="resources">The resolved mention resources, in attachment order.</param>
    /// <param name="images">The images in attachment order.</param>
    /// <param name="limits">The resource limits, or <see langword="null"/> for the defaults.</param>
    /// <param name="imageLimits">The image limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The ordered content blocks.</returns>
    /// <exception cref="ArgumentException">A resource URI is blank, or there is no text, no resource and no image.</exception>
    public static IReadOnlyList<AcpPromptBlock> BuildWithImages(string text, IReadOnlyList<AcpPromptResource> resources, IReadOnlyList<AcpPromptImage> images, AcpPromptLimits? limits = null, AcpImageLimits? imageLimits = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(resources);
        ArgumentNullException.ThrowIfNull(images);

        var imageBlocks = AcpImageContent.Build(images, imageLimits);
        if (imageBlocks.Count == 0)
        {
            return Build(text, resources, limits);
        }

        if (string.IsNullOrWhiteSpace(text) && resources.Count == 0)
        {
            return imageBlocks;
        }

        return [.. Build(text, resources, limits), .. imageBlocks];
    }

    /// <summary>Builds the <c>session/prompt</c> parameters of a message that carries images.</summary>
    /// <param name="sessionId">The session the prompt belongs to.</param>
    /// <param name="text">The user text.</param>
    /// <param name="resources">The resolved mention resources.</param>
    /// <param name="images">The images in attachment order.</param>
    /// <param name="limits">The resource limits, or <see langword="null"/> for the defaults.</param>
    /// <param name="imageLimits">The image limits, or <see langword="null"/> for the defaults.</param>
    /// <returns>The prompt parameters.</returns>
    public static AcpPromptContentParams BuildParamsWithImages(string sessionId, string text, IReadOnlyList<AcpPromptResource> resources, IReadOnlyList<AcpPromptImage> images, AcpPromptLimits? limits = null, AcpImageLimits? imageLimits = null)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        return new AcpPromptContentParams(sessionId, BuildWithImages(text, resources, images, limits, imageLimits));
    }

    /// <summary>Formats the note that lists omitted attachments.</summary>
    /// <param name="names">The omitted names.</param>
    /// <returns>The note text.</returns>
    private static string OmissionNote(List<string> names)
    {
        var listed = string.Join(", ", names.Take(MaxNamedOmissions));
        if (names.Count > MaxNamedOmissions)
        {
            listed = string.Create(CultureInfo.InvariantCulture, $"{listed}, and {names.Count - MaxNamedOmissions} more");
        }

        return string.Create(CultureInfo.InvariantCulture, $"[note: {names.Count} attached item(s) were omitted because the prompt context limit was reached: {listed}]");
    }

    /// <summary>Cuts text to at most a number of UTF-8 bytes without splitting a code point.</summary>
    /// <param name="value">The text.</param>
    /// <param name="maxBytes">The byte budget.</param>
    /// <param name="bytes">The UTF-8 byte count of the returned text.</param>
    /// <returns>The longest whole-code-point prefix that fits.</returns>
    private static string CutUtf8(string value, int maxBytes, out int bytes)
    {
        var used = 0;
        var chars = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (used + size > maxBytes)
            {
                break;
            }

            used += size;
            chars += rune.Utf16SequenceLength;
        }

        bytes = used;
        return value[..chars];
    }
}
