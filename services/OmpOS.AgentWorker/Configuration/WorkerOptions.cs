namespace OmpOS.AgentWorker.Configuration;

/// <summary>
/// Binds the <c>Worker</c> configuration section: where session directories live, how often keep-alive frames are sent, which session
/// the worker serves, and how <c>omp acp</c> is started. The authentication keys live in <see cref="WorkerAuthOptions"/>.
/// </summary>
internal sealed class WorkerOptions
{
    /// <summary>Gets the configuration section name.</summary>
    public const string SectionName = "Worker";

    /// <summary>Gets the longest accepted keep-alive interval.</summary>
    public static readonly TimeSpan MaximumPingInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the absolute directory under which every session gets its own working directory, named after the session subject.
    /// Defaults to <c>/data/work</c>, a directory on the Railway volume.
    /// </summary>
    public string WorkspaceRoot { get; set; } = "/data/work";

    /// <summary>Gets or sets the interval of the keep-alive control frames sent to an attached client. Defaults to 20 seconds.</summary>
    public TimeSpan PingInterval { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>Gets or sets how long the close handshake may take before a connection is aborted. Defaults to 5 seconds.</summary>
    public TimeSpan CloseTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the only session subject this worker serves. Unset means a pooled worker that serves any valid subject, one connection
    /// per subject at a time. A service-per-session worker pins its session here.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>Gets or sets how <c>omp acp</c> is started.</summary>
    public WorkerOmpOptions Omp { get; set; } = new();
}
