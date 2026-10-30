namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>One place a permission or sandbox file may be.</summary>
/// <param name="Path">The absolute file path.</param>
/// <param name="Scope">Whether it is the user's or the project's file.</param>
/// <param name="Origin">Whether it is an Autonogrammer or a Cursor file.</param>
public sealed record SafetyFileLocation(string Path, PermissionScope Scope, PermissionFileOrigin Origin);
