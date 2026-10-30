namespace Autonogrammer.Api.Cloud;

/// <summary>One repository as the worker is told to check it out.</summary>
/// <param name="Directory">The directory name under the session directory.</param>
/// <param name="Url">The HTTPS clone URL, without a credential.</param>
/// <param name="Branch">The branch to start from, or null for the default branch.</param>
/// <param name="BaseBranch">The branch a pull request targets, or null.</param>
internal sealed record CloudWorkerRepository(string Directory, string Url, string? Branch, string? BaseBranch);
