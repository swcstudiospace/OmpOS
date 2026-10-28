namespace OmpOS.Contracts.Cloud;

/// <summary>A pull request a cloud agent opened.</summary>
public sealed record CloudPullRequest
{
    /// <summary>Gets the clone URL of the repository the pull request is in.</summary>
    public required string Repository { get; init; }

    /// <summary>Gets the pull request number.</summary>
    public required int Number { get; init; }

    /// <summary>Gets the web URL of the pull request.</summary>
    public required Uri Url { get; init; }

    /// <summary>Gets the branch the agent pushed (always <c>ompos/cloud/&lt;agent id&gt;</c>).</summary>
    public required string Branch { get; init; }

    /// <summary>Gets the branch the pull request targets.</summary>
    public required string BaseBranch { get; init; }

    /// <summary>Gets whether the pull request was opened as a draft.</summary>
    public required bool Draft { get; init; }

    /// <summary>Gets when the control plane opened it.</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
