namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>An entry of a list that was merged from several files, with the file it came from.</summary>
/// <param name="Text">The entry.</param>
/// <param name="Source">The path of the file that holds it.</param>
/// <param name="Scope">Whether that file is the user's or the project's.</param>
/// <param name="Origin">Whether that file is an OmpOS or a Cursor file.</param>
public sealed record SafetyEntry(string Text, string Source, PermissionScope Scope, PermissionFileOrigin Origin);
