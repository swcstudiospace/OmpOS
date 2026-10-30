namespace Autonogrammer.Presentation.Mentions;

/// <summary>The rules and skills a mention can attach. The wiring adapts the convention catalog of <c>Autonogrammer.Harness.Workspace</c>.</summary>
public interface IMentionConventionProvider
{
    /// <summary>Lists the rules a user may mention: those applied only when mentioned (Cursor's "Apply Manually" type) and any other the implementation chooses to offer.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The rules.</returns>
    Task<IReadOnlyList<MentionConventionItem>> ListRulesAsync(CancellationToken cancellationToken);

    /// <summary>Lists the skills.</summary>
    /// <param name="cancellationToken">Cancels the listing.</param>
    /// <returns>The skills.</returns>
    Task<IReadOnlyList<MentionConventionItem>> ListSkillsAsync(CancellationToken cancellationToken);

    /// <summary>Reads the body of a rule or skill.</summary>
    /// <param name="kind"><see cref="MentionKind.Rule"/> or <see cref="MentionKind.Skill"/>.</param>
    /// <param name="name">The rule or skill name.</param>
    /// <param name="maxBytes">The byte cap.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The text.</returns>
    /// <exception cref="MentionUnavailableException">The rule or skill no longer exists.</exception>
    Task<MentionContent> ReadAsync(MentionKind kind, string name, int maxBytes, CancellationToken cancellationToken);
}
