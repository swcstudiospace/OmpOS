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

    /// <summary>Gets or sets the Temporal server address.</summary>
    /// <remarks>Required when <see cref="Enabled"/> is <see langword="true"/>. Defaults to <c>localhost:7233</c>.</remarks>
    internal string Address { get; set; } = DefaultAddress;

    /// <summary>Gets or sets the Temporal namespace.</summary>
    /// <remarks>Defaults to <c>default</c>.</remarks>
    internal string Namespace { get; set; } = DefaultNamespace;

    /// <summary>Gets or sets the task queue name.</summary>
    /// <remarks>Defaults to <c>ompos-session-runs</c>.</remarks>
    internal string TaskQueue { get; set; } = DefaultTaskQueue;

    /// <summary>Gets or sets the Temporal API key, sent as a Bearer token on every call.</summary>
    /// <remarks>Optional; <see langword="null"/> when unset or blank. Temporal Cloud requires it. The SDK enables TLS automatically when it is set. The value is never logged and never echoed in exception text.</remarks>
    internal string? ApiKey { get; set; }

    /// <summary>Gets or sets whether the Temporal client connects over TLS.</summary>
    /// <remarks>Defaults to <see langword="false"/>. When <see langword="true"/>, the client uses default TLS options. When <see langword="false"/> and <see cref="ApiKey"/> is set, the SDK still enables TLS.</remarks>
    internal bool Tls { get; set; }
}
