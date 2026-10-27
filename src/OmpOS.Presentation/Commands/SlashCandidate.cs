namespace OmpOS.Presentation.Commands;

/// <summary>One source that can run a typed slash name. When several sources share the name, the resolver lists each as a candidate with its source label.</summary>
/// <param name="Name">The name typed after the slash.</param>
/// <param name="Source">The source: the engine, the OmpOS catalogue, a discovered command, skill or subagent.</param>
/// <param name="Kind">What runs it.</param>
/// <param name="Scope">Where the source file lives; <see cref="SlashScope.BuiltIn"/> for the engine and the catalogue.</param>
/// <param name="Origin">The file it comes from, or <see langword="null"/>.</param>
public sealed record SlashCandidate(string Name, SlashPaletteSource Source, SlashHandlerKind Kind, SlashScope Scope, string? Origin)
{
    /// <summary>Gets the text that tells the user which source this is, for example <c>Skill, project</c>.</summary>
    public string Label => Scope == SlashScope.BuiltIn
        ? SlashPaletteCopy.SourceBadge(Source)
        : SlashPaletteCopy.SourceBadge(Source) + ", " + SlashPaletteCopy.ScopeTag(Scope);
}
