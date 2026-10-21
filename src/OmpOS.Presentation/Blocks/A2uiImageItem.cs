namespace OmpOS.Presentation.Blocks;

/// <summary>A renderable inline image (<c>image</c> component with an allowed data URI).</summary>
public sealed record A2uiImageItem : A2uiRenderItem
{
    /// <summary>Gets the image URI. Only an allowed inline data image reaches this item, so the browser never fetches a remote source.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the alt text, or null when the payload carries none.</summary>
    public string? Alt { get; init; }
}
