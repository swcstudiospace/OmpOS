using Autonogrammer.Api.Options;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Api.Harness;

/// <summary>
/// The isolation the API applies to the engine it spawns, resolved from the selected adapter's settings
/// (<see cref="HarnessOptions.AcpSettings"/> or <see cref="HarnessOptions.RpcSettings"/>) and the host environment:
/// which environment variables the engine inherits and under which directory sessions without a run worktree get their working directory.
/// </summary>
/// <param name="Mode">How the engine obtains its environment.</param>
/// <param name="Allow">The extra variable names or prefixes kept in <see cref="EngineEnvironmentMode.Allowlist"/> mode.</param>
/// <param name="WorkspaceRoot">The absolute directory that holds one working directory per session, or <see langword="null"/> when unset.</param>
internal sealed record EngineIsolationSettings(EngineEnvironmentMode Mode, IReadOnlyList<string> Allow, string? WorkspaceRoot)
{
    /// <summary>
    /// Resolves the settings. Outside Development the mode defaults to <see cref="EngineEnvironmentMode.Allowlist"/>; in Development it
    /// defaults to <see cref="EngineEnvironmentMode.Inherit"/>, the desktop behaviour. This method does not validate; see
    /// <see cref="EngineIsolationOptionsValidator"/>.
    /// </summary>
    /// <param name="acp">The bound <c>Harness:Acp</c> section.</param>
    /// <param name="development">Whether the host runs in the Development environment.</param>
    /// <returns>The resolved settings.</returns>
    public static EngineIsolationSettings Resolve(HarnessOptions.AcpSettings acp, bool development)
    {
        ArgumentNullException.ThrowIfNull(acp);
        return Resolve(acp.Environment, acp.WorkspaceRoot, development);
    }

    /// <summary>Resolves the settings from the bound <c>Harness:Rpc</c> section; same rules as the ACP overload.</summary>
    /// <param name="rpc">The bound <c>Harness:Rpc</c> section.</param>
    /// <param name="development">Whether the host runs in the Development environment.</param>
    /// <returns>The resolved settings.</returns>
    public static EngineIsolationSettings Resolve(HarnessOptions.RpcSettings rpc, bool development)
    {
        ArgumentNullException.ThrowIfNull(rpc);
        return Resolve(rpc.Environment, rpc.WorkspaceRoot, development);
    }

    private static EngineIsolationSettings Resolve(HarnessOptions.AcpEnvironmentSettings environment, string? workspaceRoot, bool development)
    {
        var mode = environment.Mode ?? (development ? EngineEnvironmentMode.Inherit : EngineEnvironmentMode.Allowlist);
        var root = string.IsNullOrWhiteSpace(workspaceRoot) ? null : workspaceRoot.Trim();
        return new EngineIsolationSettings(mode, [.. environment.Allow], root);
    }
}
