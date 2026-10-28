namespace OmpOS.Harness.AgentLoop.Safety;

/// <summary>The read boundary the Settings window holds. A <c>sandbox.json</c> value replaces it, so it is the lowest-precedence source.</summary>
/// <param name="ReadBoundary">The boundary.</param>
/// <param name="AdditionalReadPaths">The read allowlist: folders, globs and <c>~</c> paths.</param>
public sealed record SandboxSettings(SandboxReadBoundary ReadBoundary, IReadOnlyList<string> AdditionalReadPaths)
{
    /// <summary>Gets the defaults: read anywhere, no allowlist.</summary>
    public static SandboxSettings Default { get; } = new(SandboxReadBoundary.System, []);
}
