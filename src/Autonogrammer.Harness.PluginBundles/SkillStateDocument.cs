namespace Autonogrammer.Harness.PluginBundles;

/// <summary>The <c>skills-state.json</c> document. Keys are <c>bundleId/skillId</c>.</summary>
internal sealed class SkillStateDocument
{
    /// <summary>Gets or sets the skill flags.</summary>
    public Dictionary<string, SkillStateEntry> Skills { get; set; } = new(StringComparer.Ordinal);
}
