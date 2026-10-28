namespace OmpOS.Harness.Acp;

/// <summary>The wire form of one prompt content block: a <c>text</c> block or an embedded <c>resource</c> block.</summary>
public sealed class AcpPromptBlock
{
    /// <summary>Gets the block kind: <c>text</c> or <c>resource</c>.</summary>
    public string Type { get; init; } = "text";

    /// <summary>Gets the text, for text blocks.</summary>
    public string? Text { get; init; }

    /// <summary>Gets the embedded resource, for resource blocks.</summary>
    public AcpEmbeddedResource? Resource { get; init; }

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
