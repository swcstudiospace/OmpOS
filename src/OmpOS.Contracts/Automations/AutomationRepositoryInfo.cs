namespace OmpOS.Contracts.Automations;

/// <summary>A repository a cloud automation works in.</summary>
public sealed record AutomationRepositoryInfo
{
    /// <summary>Gets the HTTPS clone URL, for example <c>https://github.com/owner/repo</c>. No credentials, query or fragment.</summary>
    public required string Url { get; init; }

    /// <summary>Gets the branch the agent starts from. When absent the repository's default branch is used.</summary>
    public string? Branch { get; init; }

    /// <summary>Gets the branch a pull request targets. When absent it is the branch the agent started from.</summary>
    public string? BaseBranch { get; init; }
}
