using Autonogrammer.Presentation.Modes;

namespace Autonogrammer.Presentation.Tests.Modes;

/// <summary>A skill source that serves scripted skills and bodies.</summary>
internal sealed class ScriptedModeSkillSource : IModeSkillSource
{
    /// <summary>Gets or sets the skills listed.</summary>
    public List<ModeSkill> Skills { get; set; } = [];

    /// <summary>Gets the bodies by skill name; a missing name reads as null.</summary>
    public Dictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets or sets an exception the next read throws.</summary>
    public Exception? Failure { get; set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModeSkill>> ListAsync(CancellationToken cancellationToken) =>
        Failure is not null ? Task.FromException<IReadOnlyList<ModeSkill>>(Failure) : Task.FromResult<IReadOnlyList<ModeSkill>>([.. Skills]);

    /// <inheritdoc/>
    public Task<string?> ReadBodyAsync(ModeSkill skill, CancellationToken cancellationToken) =>
        Failure is not null ? Task.FromException<string?>(Failure) : Task.FromResult(Bodies.TryGetValue(skill.Name, out var body) ? body : null);
}
