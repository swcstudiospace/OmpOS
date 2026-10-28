namespace Autonogrammer.Presentation.Cloud;

/// <summary>What the user asks for when an agent's branch becomes a pull request.</summary>
public sealed record CloudPullRequestRequest
{
    /// <summary>Gets the repository address the pull request is for.</summary>
    public required string Repository { get; init; }

    /// <summary>Gets the pull request title.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the pull request description. Empty when none.</summary>
    public string Body { get; init; } = string.Empty;

    /// <summary>Gets the branch the pull request targets, or null for the repository's default branch.</summary>
    public string? BaseBranch { get; init; }

    /// <summary>Gets whether the pull request opens as a draft. It is true by default, because the agent's work has not been reviewed.</summary>
    public bool Draft { get; init; } = true;
}
