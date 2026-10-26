namespace OmpOS.Presentation.Blocks;

/// <summary>A body paragraph (<c>text</c> component).</summary>
public sealed record A2uiParagraphItem : A2uiRenderItem
{
    /// <summary>Gets the paragraph text.</summary>
    public string? Text { get; init; }
}
