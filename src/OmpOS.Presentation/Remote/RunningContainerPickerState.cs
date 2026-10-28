namespace OmpOS.Presentation.Remote;

/// <summary>What the running-container picker shows as a whole.</summary>
public enum RunningContainerPickerState
{
    /// <summary>The picker was not opened yet.</summary>
    Idle,

    /// <summary>podman is being asked for the running containers.</summary>
    Loading,

    /// <summary>At least one running container is listed.</summary>
    Ready,

    /// <summary>podman answered and no container is running.</summary>
    Empty,

    /// <summary>podman could not list the containers: the diagnosis says why and how to fix it.</summary>
    Error,
}
