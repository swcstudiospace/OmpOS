namespace OmpOS.Presentation.Modes;

/// <summary>
/// Where custom modes come from. The desktop head implements it over <c>ConventionCatalog</c> of <c>OmpOS.Harness.Workspace</c>: <see cref="ListAsync"/> maps every skill whose <c>UserInvocable</c> is true,
/// and <see cref="ReadBodyAsync"/> reads its <c>SKILL.md</c> through the same file source and returns the text after the frontmatter.
/// </summary>
public interface IModeSkillSource
{
    /// <summary>Lists the skills that can be pinned. Each call reads again, so calling it is the manual refresh.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The skills, in the order the palette lists them.</returns>
    Task<IReadOnlyList<ModeSkill>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Reads a skill's body: its text without the frontmatter.</summary>
    /// <param name="skill">The skill.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The body, or <see langword="null"/> when the skill cannot be read any more.</returns>
    Task<string?> ReadBodyAsync(ModeSkill skill, CancellationToken cancellationToken);
}
