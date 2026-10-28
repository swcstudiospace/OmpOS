namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>One allow or deny rule in force: a token and the file it came from. The audit record names it.</summary>
/// <param name="Token">The token.</param>
/// <param name="Source">The path of the file that holds it.</param>
/// <param name="Scope">Whether that file is the user's or the project's.</param>
public sealed record PermissionRule(PermissionToken Token, string Source, PermissionScope Scope);
