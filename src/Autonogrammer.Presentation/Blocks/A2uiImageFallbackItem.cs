namespace Autonogrammer.Presentation.Blocks;

/// <summary>
/// The fallback for an image that cannot render inline (<c>image</c> component with a non-renderable source).
/// It is a link when <see cref="Href"/> is set and plain text otherwise.
/// </summary>
public sealed record A2uiImageFallbackItem : A2uiRenderItem
{
    /// <summary>Gets the link target, or null when the source scheme is not link-allowed.</summary>
    public string? Href { get; init; }

    /// <summary>Gets the fallback text: the alt text, or the source text when alt is empty.</summary>
    public string? Text { get; init; }
}
