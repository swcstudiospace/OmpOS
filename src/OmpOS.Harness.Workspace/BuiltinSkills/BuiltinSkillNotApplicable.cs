namespace OmpOS.Workspace.BuiltinSkills;

/// <summary>A Cursor built-in skill that OmpOS does not ship, and the reason.</summary>
/// <param name="Token">The Cursor slash token, without the slash.</param>
/// <param name="Reason">One sentence that says why OmpOS has no equivalent.</param>
public sealed record BuiltinSkillNotApplicable(string Token, string Reason);
