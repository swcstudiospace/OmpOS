namespace Autonogrammer.Contracts.Controls;

/// <summary>One tool approval that waits for a decision.</summary>
public sealed record PendingToolApproval
{
    /// <summary>Gets the request identifier a decision is addressed to.</summary>
    public required string RequestId { get; init; }

    /// <summary>Gets the tool the request concerns.</summary>
    public required string ToolName { get; init; }

    /// <summary>Gets when the request was made.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Gets the tools a decision may allow. Empty means the policy ceiling permits none, so only a deny can be recorded.</summary>
    public required IReadOnlyList<string> GrantableTools { get; init; }
}
