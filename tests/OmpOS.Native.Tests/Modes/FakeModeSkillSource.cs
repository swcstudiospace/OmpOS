using OmpOS.Presentation.Modes;

namespace OmpOS.Native.Tests.Modes;

/// <summary>A skill source that serves scripted skills and bodies and counts how often the skills were listed.</summary>
internal sealed class FakeModeSkillSource : IModeSkillSource
{
    /// <summary>Gets or sets the skills listed.</summary>
    public List<ModeSkill> Skills { get; set; } = [];

    /// <summary>Gets the bodies by skill name; a missing name reads as null.</summary>
    public Dictionary<string, string> Bodies { get; } = new(StringComparer.Ordinal);

    /// <summary>Gets how many times the skills were listed.</summary>
    public int ListCalls { get; private set; }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ModeSkill>> ListAsync(CancellationToken cancellationToken)
    {
        ListCalls++;
        return Task.FromResult<IReadOnlyList<ModeSkill>>([.. Skills]);
    }

    /// <inheritdoc/>
    public Task<string?> ReadBodyAsync(ModeSkill skill, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        return Task.FromResult(Bodies.TryGetValue(skill.Name, out var body) ? body : null);
    }
}
