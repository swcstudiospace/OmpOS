namespace OmpOS.Presentation.Cloud;

/// <summary>A pull request an agent opened for one of its repositories.</summary>
/// <param name="Repository">The repository address the pull request belongs to, equal to a <see cref="CloudRepository.Address"/> of the agent.</param>
/// <param name="Url">The https address of the pull request page.</param>
/// <param name="Title">The pull request title, or null when the host did not report one.</param>
public sealed record CloudPullRequestLink(string Repository, Uri Url, string? Title = null);
