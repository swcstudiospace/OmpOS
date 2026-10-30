namespace Autonogrammer.Harness.Automations.Hosted;

/// <summary>
/// A repository a cloud target works in.
/// </summary>
/// <param name="Address">The HTTPS clone URL, with no credentials, query or fragment.</param>
/// <param name="Branch">The branch the agent starts from, or null for the repository's default branch.</param>
/// <param name="BaseBranch">The branch a pull request targets, or null for the branch the agent started from.</param>
public sealed record AutomationRepository(string Address, string? Branch = null, string? BaseBranch = null);
