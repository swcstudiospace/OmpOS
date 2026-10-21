namespace OmpOS.Presentation.Blocks;

/// <summary>A component the catalog does not support, shown with its raw JSON.</summary>
public sealed record A2uiUnsupportedItem : A2uiRenderItem
{
    /// <summary>Gets the component type name as carried by the payload.</summary>
    public required string Type { get; init; }

    /// <summary>Gets the component's raw JSON text.</summary>
    public required string Raw { get; init; }
}
