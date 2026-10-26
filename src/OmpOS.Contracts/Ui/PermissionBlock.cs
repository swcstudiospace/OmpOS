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

    /// <summary>
    /// Gets the decision taken on the request, or null while it is undecided. The known values are in
    /// <see cref="ApprovalDecisions"/>; a value this version does not know is carried as it came.
    /// </summary>
    public string? Decision { get; init; }
}
