namespace OmpOS.Presentation.Blocks;

/// <summary>A button (<c>button</c> component). It has no action on the client.</summary>
public sealed record A2uiButtonItem : A2uiRenderItem
{
    /// <summary>Gets the button label.</summary>
    public string? Label { get; init; }
}
