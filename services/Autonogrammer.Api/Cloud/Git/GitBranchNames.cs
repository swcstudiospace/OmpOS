using System.Text.RegularExpressions;

namespace Autonogrammer.Api.Cloud;

/// <summary>Branch name rules: the names Autonogrammer creates, and the check applied to a branch a caller names. The check is stricter than git's own rules on purpose, so a name can be placed in a URL, a command and a variable without quoting.</summary>
internal static partial class GitBranchNames
{
    /// <summary>The prefix of every branch a cloud agent pushes.</summary>
    public const string AgentPrefix = "autonogrammer/cloud/";

    /// <summary>Gets the branch a cloud agent pushes to.</summary>
    /// <param name="agentId">The agent identifier.</param>
    /// <returns><c>autonogrammer/cloud/&lt;agent id&gt;</c>.</returns>
    public static string ForAgent(string agentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(agentId);
        return AgentPrefix + agentId;
    }

    /// <summary>Returns whether <paramref name="name"/> is an acceptable branch name.</summary>
    /// <param name="name">The candidate.</param>
    /// <returns>True for 1 to 200 letters, digits and <c>._/-</c>, with no leading dash or slash, no empty segment, no <c>..</c> and no trailing dot, slash or <c>.lock</c>.</returns>
    public static bool IsValid(string? name)
    {
        if (name is null || name.Length is 0 or > 200 || !Pattern().IsMatch(name))
        {
            return false;
        }

        return !name.Contains("..", StringComparison.Ordinal)
            && !name.Contains("//", StringComparison.Ordinal)
            && !name.EndsWith('/')
            && !name.EndsWith('.')
            && !name.EndsWith(".lock", StringComparison.Ordinal)
            && !name.Split('/').Any(static segment => segment.StartsWith('.') || segment.EndsWith(".lock", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^[A-Za-z0-9_][A-Za-z0-9._/-]*\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
