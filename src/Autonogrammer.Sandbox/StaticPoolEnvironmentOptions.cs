namespace Autonogrammer.Sandbox;

/// <summary>
/// Settings for <see cref="StaticPoolEnvironmentClient"/>.
/// </summary>
public sealed class StaticPoolEnvironmentOptions
{
    /// <summary>Gets or sets the fixed list of running workers; it must not be empty and worker ids must be unique.</summary>
    public IReadOnlyList<StaticPoolWorker> Workers { get; set; } = [];

    /// <summary>Gets or sets how long <see cref="StaticPoolEnvironmentClient.EnsureAwakeAsync"/> retries the readiness probe.</summary>
    public TimeSpan WakeTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Gets or sets the pause between readiness probes.</summary>
    public TimeSpan WakeRetryInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>Gets or sets the workers' anonymous readiness path.</summary>
    public string HealthPath { get; set; } = "/healthz";
}
