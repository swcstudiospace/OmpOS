namespace Autonogrammer.Harness.Acp;

/// <summary>The wire form of one prompt content block: a <c>text</c> block, an embedded <c>resource</c> block or an <c>image</c> block.</summary>
public sealed class AcpPromptBlock
{
    /// <summary>Gets the block kind: <c>text</c>, <c>resource</c> or <c>image</c>.</summary>
    public string Type { get; init; } = "text";

    /// <summary>Gets the text, for text blocks.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the embedded resource, for resource blocks.</summary>
    public AcpEmbeddedResource? Resource { get; init; }

    /// <summary>Gets the base64 image bytes, for image blocks.</summary>
    public string? Data { get; init; }

    /// <summary>Gets the image MIME type, for image blocks.</summary>
    public string? MimeType { get; init; }

    /// <summary>Creates an image block.</summary>
    /// <param name="mimeType">The image MIME type.</param>
    /// <param name="base64Data">The base64 encoded image.</param>
    /// <returns>The image block.</returns>
    public static AcpPromptBlock ImageBlock(string mimeType, string base64Data)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mimeType);
        ArgumentException.ThrowIfNullOrEmpty(base64Data);
        return new AcpPromptBlock { Type = "image", Data = base64Data, MimeType = mimeType };
    }

    /// <summary>Creates a text block.</summary>
    /// <param name="text">The block text.</param>
    /// <returns>The text block.</returns>
    public static AcpPromptBlock TextBlock(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new AcpPromptBlock { Type = "text", Text = text };
    }

    /// <summary>Creates an embedded resource block.</summary>
    /// <param name="resource">The embedded resource.</param>
    /// <returns>The resource block.</returns>
    public static AcpPromptBlock ResourceBlock(AcpEmbeddedResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new AcpPromptBlock { Type = "resource", Resource = resource };
    }
}
