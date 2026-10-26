using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions;

/// <summary>
/// A tool approval request stored with its session, and its decision once one is made.
/// </summary>
/// <remarks>
/// A request is pending while <see cref="Decision"/> is null, and decided once it is not. A decision is set at most once,
/// and a decided request keeps its identifier, so the identifier cannot be requested again within the session.
/// </remarks>
public sealed record ToolApprovalRecord
{
    /// <summary>Gets the owning session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the request identifier a decision is addressed to. Unique within the session.</summary>
    public required string RequestId { get; init; }

    /// <summary>Gets the tool the request concerns.</summary>
    public required string ToolName { get; init; }

    /// <summary>
    /// Gets the policy ceiling: the tool names a decision may allow, sorted by ordinal comparison.
    /// Empty means DenyAll, the default, which permits no tool.
    /// </summary>
    public required IReadOnlyList<string> GrantableTools { get; init; }

    /// <summary>Gets when the request was made.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Gets the decision, or null while the request is pending.</summary>
    public PermissionDecision? Decision { get; init; }

    /// <summary>Gets when the decision was made, or null while the request is pending.</summary>
    public DateTimeOffset? DecidedAt { get; init; }
}
