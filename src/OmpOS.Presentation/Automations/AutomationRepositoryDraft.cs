namespace OmpOS.Presentation.Automations;

/// <summary>A repository a cloud automation works on.</summary>
/// <param name="Url">The https address of the repository.</param>
/// <param name="Branch">The branch the agent works on, or an empty string for the host's choice.</param>
/// <param name="BaseBranch">The branch pull requests target, or an empty string for the repository default.</param>
public sealed record AutomationRepositoryDraft(string Url, string Branch, string BaseBranch);
