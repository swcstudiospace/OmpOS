namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The names of the tools an automation run may be allowed to use. A name is a request to the target, which enforces it; the
/// library carries and validates the names but runs no tool itself.
/// </summary>
public static class AutomationTools
{
    /// <summary>Open a pull request from the work the run did. On by default.</summary>
    public const string PullRequestCreate = "pull_request_create";

    /// <summary>Comment on a pull request, top-level or inline.</summary>
    public const string PullRequestComment = "pull_request_comment";

    /// <summary>Request reviewers on a pull request.</summary>
    public const string PullRequestRequestReviewers = "pull_request_request_reviewers";

    /// <summary>Gets every known tool name.</summary>
    public static IReadOnlyList<string> Known { get; } = [PullRequestCreate, PullRequestComment, PullRequestRequestReviewers];

    /// <summary>Gets the tools a new automation allows when none are chosen.</summary>
    public static IReadOnlyList<string> Default { get; } = [PullRequestCreate];
}
