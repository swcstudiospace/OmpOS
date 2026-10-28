namespace Autonogrammer.Contracts.Ui;

/// <summary>
/// A file-diff placeholder block (kind "diff"). The actual diff is rendered
/// by a later phase; this block carries only the location and reason.
/// </summary>
public sealed record DiffPlaceholderBlock : UiBlock
{
    /// <summary>Gets the file path the diff applies to.</summary>
    public required string Path { get; init; }

    /// <summary>Gets why only a placeholder is carried.</summary>
    public required string Reason { get; init; }
}
