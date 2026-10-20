namespace OmpOS.Contracts.Ui;

/// <summary>
/// A tool-approval prompt block (kind "permission").
/// </summary>
public sealed record PermissionBlock : UiBlock
{
    /// <summary>Gets the approval prompt text.</summary>
    public required string Prompt { get; init; }

    /// <summary>Gets the verbatim options offered by the agent.</summary>
    public required IReadOnlyList<string> Options { get; init; }
}
