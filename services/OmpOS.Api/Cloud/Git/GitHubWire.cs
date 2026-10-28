using System.Text.Json.Serialization;

namespace OmpOS.Api.Cloud;

/// <summary>The JSON shapes of the GitHub REST calls the control plane makes. Only the members it reads or writes are declared.</summary>
internal static class GitHubWire
{
    /// <summary>The body of <c>POST /app/installations/{id}/access_tokens</c>.</summary>
    /// <param name="Repositories">The repository names (without owner) the token may reach.</param>
    /// <param name="Permissions">The permission names and levels.</param>
    internal sealed record InstallationTokenRequest(
        [property: JsonPropertyName("repositories")] string[] Repositories,
        [property: JsonPropertyName("permissions")] Dictionary<string, string> Permissions);

    /// <summary>The answer to the installation token request.</summary>
    /// <param name="Token">The token. Never printed.</param>
    /// <param name="ExpiresAt">When it expires.</param>
    internal sealed record InstallationTokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt)
    {
        /// <inheritdoc />
        public override string ToString() => "InstallationTokenResponse (token not shown)";
    }

    /// <summary>The answer to <c>GET /repos/{owner}/{repo}/installation</c>.</summary>
    /// <param name="Id">The installation identifier.</param>
    internal sealed record InstallationResponse([property: JsonPropertyName("id")] long Id);

    /// <summary>The answer to <c>GET /repos/{owner}/{repo}</c>.</summary>
    /// <param name="DefaultBranch">The default branch name.</param>
    internal sealed record RepositoryResponse([property: JsonPropertyName("default_branch")] string DefaultBranch);

    /// <summary>The answer to <c>GET /repos/{owner}/{repo}/contents/{path}</c> for a file.</summary>
    /// <param name="Type">The entry type; <c>file</c> for a file.</param>
    /// <param name="Encoding">The content encoding; <c>base64</c>.</param>
    /// <param name="Content">The base64 content with line breaks.</param>
    internal sealed record ContentResponse(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("encoding")] string? Encoding,
        [property: JsonPropertyName("content")] string? Content);

    /// <summary>The body of <c>POST /repos/{owner}/{repo}/pulls</c>.</summary>
    /// <param name="Title">The title.</param>
    /// <param name="Head">The branch with the changes.</param>
    /// <param name="BaseBranch">The branch to merge into.</param>
    /// <param name="Body">The description.</param>
    /// <param name="Draft">Whether to open a draft.</param>
    internal sealed record CreatePullRequestBody(
        [property: JsonPropertyName("title")] string Title,
        [property: JsonPropertyName("head")] string Head,
        [property: JsonPropertyName("base")] string BaseBranch,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("draft")] bool Draft);

    /// <summary>The answer to the pull request creation.</summary>
    /// <param name="Number">The pull request number.</param>
    /// <param name="HtmlUrl">The web URL.</param>
    /// <param name="Draft">Whether it is a draft.</param>
    internal sealed record PullRequestResponse(
        [property: JsonPropertyName("number")] int Number,
        [property: JsonPropertyName("html_url")] string HtmlUrl,
        [property: JsonPropertyName("draft")] bool Draft);
}
