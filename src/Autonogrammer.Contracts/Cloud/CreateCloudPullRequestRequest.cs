using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Cloud;

/// <summary>The body of <c>POST /cloud/agents/{id}/pull-request</c>.</summary>
public sealed record CreateCloudPullRequestRequest
{
    /// <summary>Gets the clone URL of the repository to open the pull request in. When absent the agent's primary repository is used.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Repository { get; init; }

    /// <summary>Gets the pull request title. Required, 1 to 256 characters.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>Gets the pull request description. Optional, up to 65 536 characters.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Body { get; init; }

    /// <summary>Gets the target branch. When absent the repository entry's base branch, its start branch, or the default branch is used, in that order.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaseBranch { get; init; }

    /// <summary>Gets whether to open a draft. When absent the pull request is ready for review. The server setting <c>Cloud:PullRequests:NeverDraft</c> overrides <see langword="true"/>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Draft { get; init; }
}
