namespace Autonogrammer.Api.Cloud;

/// <summary>A pull request to open.</summary>
/// <param name="Title">The title.</param>
/// <param name="Body">The description, or null.</param>
/// <param name="Head">The branch that holds the changes.</param>
/// <param name="Base">The branch to merge into.</param>
/// <param name="Draft">Whether to open a draft.</param>
internal sealed record GitPullRequestRequest(string Title, string? Body, string Head, string Base, bool Draft);
