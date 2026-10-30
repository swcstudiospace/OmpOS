namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// The parts of a GitHub <c>push</c> or <c>pull_request</c> payload that trigger filters and the run prompt use. Every text comes from
/// outside Autonogrammer and is untrusted; it is already bounded and stripped of control characters.
/// </summary>
/// <param name="Event">The event name: <c>push</c> or <c>pull_request</c>.</param>
/// <param name="Action">The pull request action, with <c>merged</c> for a pull request closed by a merge; empty for a push.</param>
/// <param name="Branch">The pushed branch, or the base branch of a pull request, without <c>refs/heads/</c>.</param>
/// <param name="IsForkPullRequest">True when the head repository differs from the base repository.</param>
/// <param name="Summary">One line for the run history.</param>
/// <param name="Payload">A bounded text block of the event's key facts, framed as data in the prompt.</param>
public sealed record GitHubEventInfo(string Event, string Action, string Branch, bool IsForkPullRequest, string Summary, string Payload);
