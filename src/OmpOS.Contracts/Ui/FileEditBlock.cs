namespace OmpOS.Contracts.Ui;

/// <summary>
/// A file change a tool call made (kind "file_edit"): the path with the text before and after. Unlike
/// <see cref="DiffPlaceholderBlock"/>, which only names a path, this block carries the edit itself.
/// </summary>
public sealed record FileEditBlock : UiBlock
{
    /// <summary>Gets the identifier of the tool call that made the edit.</summary>
    public required string CallId { get; init; }

    /// <summary>Gets the path of the edited file, as the agent reported it.</summary>
    public required string Path { get; init; }

    /// <summary>Gets the file text before the edit, or null when the edit created the file.</summary>
    public string? OldText { get; init; }

    /// <summary>Gets the file text after the edit.</summary>
    public required string NewText { get; init; }
}
