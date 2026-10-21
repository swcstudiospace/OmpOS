namespace OmpOS.Contracts.Ui;

/// <summary>
/// A markdown content block (kind "markdown").
/// </summary>
public sealed record MarkdownBlock : UiBlock
{
    /// <summary>Gets the markdown source.</summary>
    public required string Markdown { get; init; }
}
