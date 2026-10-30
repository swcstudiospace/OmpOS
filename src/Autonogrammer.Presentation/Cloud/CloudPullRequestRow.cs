namespace Autonogrammer.Presentation.Cloud;

/// <summary>The pull request line of one repository of an agent: the link when a pull request exists, otherwise the action that opens one.</summary>
/// <param name="Repository">The repository address; the key the details model acts on.</param>
/// <param name="RepositoryName">The repository as <c>owner/repository</c>.</param>
/// <param name="HasPullRequest">Whether a pull request exists, so the action opens it instead of creating one.</param>
/// <param name="Title">The pull request title, or an empty string.</param>
/// <param name="ActionLabel">The words of the button: Open pull request, or Create pull request.</param>
/// <param name="ActionName">The accessible name of the button, which names the repository.</param>
/// <param name="CanAct">Whether the button is enabled.</param>
public sealed record CloudPullRequestRow(
    string Repository,
    string RepositoryName,
    bool HasPullRequest,
    string Title,
    string ActionLabel,
    string ActionName,
    bool CanAct);
