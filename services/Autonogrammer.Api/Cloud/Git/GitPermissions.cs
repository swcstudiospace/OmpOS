namespace Autonogrammer.Api.Cloud;

/// <summary>The repository permissions an installation token is minted with. Only what a step needs is asked for.</summary>
/// <param name="Contents">The <c>contents</c> permission: <c>read</c> or <c>write</c>.</param>
/// <param name="PullRequests">The <c>pull_requests</c> permission: <c>read</c>, <c>write</c>, or null for none.</param>
internal sealed record GitPermissions(string Contents, string? PullRequests)
{
    /// <summary>Gets the permissions of a worker: clone, commit and push, and read pull requests.</summary>
    public static GitPermissions Push { get; } = new("write", "write");

    /// <summary>Gets the permissions to open a pull request: read the branch, write the pull request.</summary>
    public static GitPermissions OpenPullRequest { get; } = new("read", "write");

    /// <summary>Gets the permissions to read files and branch names.</summary>
    public static GitPermissions Read { get; } = new("read", null);

    /// <summary>Gets the permissions as the dictionary the GitHub API takes.</summary>
    /// <returns>The permission names and levels.</returns>
    public Dictionary<string, string> ToDictionary()
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal) { ["contents"] = Contents };
        if (PullRequests is not null)
        {
            map["pull_requests"] = PullRequests;
        }

        return map;
    }
}
