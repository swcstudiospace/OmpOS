namespace OmpOS.Workflows.Worker;

/// <summary>
/// Configuration options for the Temporal workflow worker.
/// </summary>
/// <remarks>
/// Bound from the <c>Workflows</c> configuration section by
/// <see cref="WorkerHostConfiguration"/> (primitive getters only, so trimming stays safe).
/// Validated at startup when <see cref="Enabled"/> is <see langword="true"/>.
/// </remarks>
internal sealed class WorkerOptions
{
    /// <summary>The default Temporal server gRPC endpoint.</summary>
    internal const string DefaultAddress = "localhost:7233";

    /// <summary>The default Temporal namespace.</summary>
    internal const string DefaultNamespace = "default";

    /// <summary>The default task queue for session-run workflow and activity tasks.</summary>
    internal const string DefaultTaskQueue = "ompos-session-runs";

    /// <summary>Gets or sets whether the Temporal worker is enabled.</summary>
    /// <remarks>Defaults to <see langword="false"/>. When disabled, the host exits immediately without registering Temporal services.</remarks>
    internal bool Enabled { get; set; }

    /// <summary>Gets or sets whether the worker registers fake session-run activities.</summary>
    /// <remarks>Defaults to <see langword="false"/>. Fake activities silently fabricate successful runs; they must be opted into explicitly for replay and smoke runs. Otherwise a fail-loud guard is registered.</remarks>
    internal bool EnableFakeActivities { get; set; }

    /// <summary>Gets or sets the Temporal server address.</summary>
    /// <remarks>Required when <see cref="Enabled"/> is <see langword="true"/>. Defaults to <c>localhost:7233</c>.</remarks>
    internal string Address { get; set; } = DefaultAddress;

    /// <summary>Gets or sets the Temporal namespace.</summary>
    /// <remarks>Defaults to <c>default</c>.</remarks>
    internal string Namespace { get; set; } = DefaultNamespace;

    /// <summary>Gets or sets the task queue name.</summary>
    /// <remarks>Defaults to <c>ompos-session-runs</c>.</remarks>
    internal string TaskQueue { get; set; } = DefaultTaskQueue;
}
