namespace Autonogrammer.Presentation.Blocks;

/// <summary>A heading (<c>heading</c> component).</summary>
public sealed record A2uiHeadingItem : A2uiRenderItem
{
    /// <summary>Gets the heading text.</summary>
    public string? Text { get; init; }
}
