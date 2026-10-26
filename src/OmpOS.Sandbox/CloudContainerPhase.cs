namespace OmpOS.Sandbox;

/// <summary>
/// Lifecycle phase of a cloud session container.
/// </summary>
public enum CloudContainerPhase
{
    /// <summary>The container is running and its volume is mounted.</summary>
    Running,

    /// <summary>The container is stopped to save cost; only its volume persists.</summary>
    Sleeping,
}
