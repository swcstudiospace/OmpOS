using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Cloud;

/// <summary>One repository a cloud agent works in.</summary>
public sealed record CloudRepository
{
    /// <summary>Gets the HTTPS clone URL, for example <c>https://github.com/owner/repo</c>. No credentials, query or fragment.</summary>
    [JsonPropertyName("url")]
    public required string Address { get; init; }

    /// <summary>Gets the branch the agent starts from. When absent the repository's default branch is used.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Branch { get; init; }

    /// <summary>Gets the branch a pull request targets. When absent it is the branch the agent started from.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BaseBranch { get; init; }
}
