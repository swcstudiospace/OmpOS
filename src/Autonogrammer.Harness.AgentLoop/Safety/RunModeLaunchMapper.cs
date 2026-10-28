namespace Autonogrammer.Harness.AgentLoop.Safety;

/// <summary>
/// Maps a run mode to omp's approval mode. omp's approval policy belongs to the process (<c>omp acp --approval-mode X</c>; ACP has no per-session field) and its default is
/// <c>yolo</c>, so a launch that passes no flag runs everything. The accepted values are <c>always-ask</c>, <c>always-ask-cached</c>, <c>write</c> and <c>yolo</c>
/// (verified with <c>omp acp --help</c> and omp's approval-mode reference).
/// </summary>
public static class RunModeLaunchMapper
{
    /// <summary>The tool kinds of ACP v1 (<c>ToolKind</c>). <see cref="RunMode.RunEverything"/> needs an approval policy that admits all of them, because the session consults the policy before any resolver.</summary>
    public static IReadOnlyList<string> AcpToolKinds { get; } = ["read", "edit", "delete", "move", "search", "execute", "think", "fetch", "switch_mode", "other"];

    /// <summary>Maps a run mode.</summary>
    /// <param name="mode">The run mode.</param>
    /// <param name="readsNeedGate">
    /// True when Autonogrammer must see file reads (the read boundary is <c>workspace</c> or a read deny rule exists). omp's <c>always-ask</c> auto-approves reads, so Autonogrammer never sees them;
    /// <c>always-ask-cached</c> asks before each file read too. See <see cref="ReadsNeedGate"/>.
    /// </param>
    /// <returns>The launch description.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
    public static RunModeLaunch Map(RunMode mode, bool readsNeedGate = true) => mode switch
    {
        RunMode.AutoReview or RunMode.Allowlist => new RunModeLaunch(mode, readsNeedGate ? "always-ask-cached" : "always-ask", OmpAsksAutonogrammer: true),
        RunMode.RunEverything => new RunModeLaunch(mode, "yolo", OmpAsksAutonogrammer: false),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown run mode."),
    };

    /// <summary>Returns whether Autonogrammer must see file reads to enforce the files in force.</summary>
    /// <param name="permissions">The permissions in force.</param>
    /// <param name="sandbox">The sandbox in force.</param>
    /// <returns>True for a <c>workspace</c> read boundary or any <c>Read</c> deny rule.</returns>
    public static bool ReadsNeedGate(EffectivePermissions permissions, EffectiveSandbox sandbox)
    {
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(sandbox);
        return sandbox.ReadBoundary == SandboxReadBoundary.Workspace || permissions.DenyRules.Any(static rule => rule.Token.Kind == PermissionTokenKind.Read);
    }
}
