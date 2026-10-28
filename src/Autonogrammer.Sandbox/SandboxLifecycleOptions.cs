namespace Autonogrammer.Sandbox;

/// <summary>
/// Options for SandboxLifecycle.
/// </summary>
public sealed class SandboxLifecycleOptions
{
    /// <summary>
    /// Gets or sets the default TTL for sandboxes.
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the maximum number of teardown retries.
    /// </summary>
    public int TeardownRetryCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the base backoff for teardown retries.
    /// </summary>
    public TimeSpan BaseBackoff { get; set; } = TimeSpan.FromMilliseconds(500);
}
