namespace OmpOS.Workspace.BuiltinSkills;

/// <summary>A skill that ships with the application.</summary>
/// <param name="Name">The skill name: the folder name below <c>skills</c>, and the token typed after a slash to run it.</param>
/// <param name="SlashTokens">The slash tokens of the command catalogue that this skill makes runnable. The first one is the skill's own name.</param>
/// <param name="Summary">One sentence that says what the skill does.</param>
public sealed record BuiltinSkillEntry(string Name, IReadOnlyList<string> SlashTokens, string Summary);
