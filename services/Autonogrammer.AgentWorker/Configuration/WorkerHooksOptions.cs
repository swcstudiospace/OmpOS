namespace Autonogrammer.AgentWorker.Configuration;

/// <summary>
/// Binds the <c>Worker:Hooks</c> section: whether the worker runs the project hooks of a session's checkout next to its <c>omp acp</c> child,
/// and where the hooks extension for omp and the per-session hook state live. Hooks are off by default. When they are on, a session still runs
/// no project hook unless the connection carries a consent snapshot (see <c>HooksConsentSnapshotReader</c>).
/// </summary>
internal sealed class WorkerHooksOptions
{
    /// <summary>Gets the name of the directory under the workspace root that holds the hook state when <see cref="StateRoot"/> is not set.</summary>
    public const string DefaultStateDirectoryName = ".autonogrammer-hooks";

    /// <summary>Gets or sets a value indicating whether the worker runs project hooks. Defaults to <see langword="false"/>.</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the absolute path of the directory that holds the <c>autonogrammer-hooks-omp</c> extension (the image's <c>plugins</c> directory).
    /// Required when hooks are enabled: without the extension a hook cannot stop a tool call, so a session that has hooks the worker cannot
    /// enforce is refused when one of them is fail-closed and reported as unavailable otherwise.
    /// </summary>
    public string? PluginsRoot { get; set; }

    /// <summary>
    /// Gets or sets the absolute directory of the per-session hook state: the consent store built from the snapshot, the bridge startup file and
    /// the run log. Defaults to <c>.autonogrammer-hooks</c> under the workspace root, a name no session subject can take.
    /// </summary>
    public string? StateRoot { get; set; }

    /// <summary>Gets or sets the most hashes a consent snapshot may name. A snapshot with more is refused. Defaults to 256, which keeps the signed token (about 90 bytes per hash once encoded) well inside Kestrel's 32 KiB header limit.</summary>
    public int MaxSnapshotHashes { get; set; } = 256;
}
