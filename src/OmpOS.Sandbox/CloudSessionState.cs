namespace OmpOS.Sandbox;

/// <summary>
/// Whether a cloud session's container is running or asleep.
/// </summary>
public enum CloudSessionState
{
    /// <summary>The container runs and accepts transcript writes.</summary>
    Running,

    /// <summary>The container sleeps; only the volume persists. Call <see cref="CloudSessionHost.ReattachAsync"/> to wake it.</summary>
    Sleeping,
}
