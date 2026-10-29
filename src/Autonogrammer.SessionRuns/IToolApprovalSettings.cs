namespace Autonogrammer.SessionRuns;

/// <summary>
/// The operator policy a run applies when it registers a permission request as a tool approval: the ceiling of the tools a user decision
/// may allow, and how long the request waits for that decision. Without a policy a run registers every request with an empty ceiling
/// (an <c>allow</c> decision is refused) and the shared <see cref="Autonogrammer.Harness.Abstractions.PermissionDecisionWindow"/>.
/// </summary>
public interface IToolApprovalSettings
{
    /// <summary>Gets how long a registered request waits for its decision before it is denied. Must be positive.</summary>
    TimeSpan DecisionTimeout { get; }

    /// <summary>
    /// Gets the policy ceiling stored with every request: the tool names a decision may allow, or <c>*</c> to permit any tool. Empty permits none.
    /// </summary>
    IReadOnlySet<string> GrantableTools { get; }
}
