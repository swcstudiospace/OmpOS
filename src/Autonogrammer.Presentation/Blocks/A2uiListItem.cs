namespace Autonogrammer.Presentation.Blocks;

/// <summary>A list (<c>list</c> component).</summary>
public sealed record A2uiListItem : A2uiRenderItem
{
    /// <summary>Gets the list items, in payload order.</summary>
    public required IReadOnlyList<string> Items { get; init; }
}
