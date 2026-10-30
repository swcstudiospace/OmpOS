namespace Autonogrammer.Sandbox;

/// <summary>
/// Options for <see cref="CloudSessionHost"/>.
/// </summary>
public sealed class CloudSessionHostOptions
{
    /// <summary>The default idle window: 24 hours (owner decision 2026-10-22).</summary>
    public static readonly TimeSpan DefaultIdleWindow = TimeSpan.FromHours(24);

    /// <summary>
    /// Gets or sets how long a session may be idle before its container is put to sleep.
    /// Injected so the host never waits on settings; callers that read the U40-D idle-stop setting pass its value here.
    /// </summary>
    public TimeSpan IdleWindow { get; set; } = DefaultIdleWindow;

    /// <summary>Gets or sets the absolute path where the session volume is mounted inside the container.</summary>
    public string VolumeMountPath { get; set; } = "/var/omp/session";

    /// <summary>Gets or sets the transcript file path relative to the volume root.</summary>
    public string TranscriptPath { get; set; } = CloudTranscriptVolume.DefaultPath;
}
