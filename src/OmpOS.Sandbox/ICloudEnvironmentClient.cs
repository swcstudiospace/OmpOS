namespace OmpOS.Sandbox;

/// <summary>
/// Narrow client over a cloud environment provider (Railway in production): one container and one volume per session.
/// </summary>
/// <remarks>
/// Only a volume survives sleep. A sleeping container keeps no running process, shell or in-memory state,
/// and <see cref="WakeContainerAsync"/> starts the container's processes afresh.
/// The live Railway implementation is blocked externally (Railway deploys paused); only fakes implement this interface today.
/// </remarks>
public interface ICloudEnvironmentClient
{
    /// <summary>
    /// Finds the container created for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The container, or <see langword="null"/> when none exists.</returns>
    Task<CloudContainer?> FindContainerAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the container for a session, running.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created container.</returns>
    Task<CloudContainer> CreateContainerAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the volume created for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The volume, or <see langword="null"/> when none exists.</returns>
    Task<CloudVolume?> FindVolumeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates the volume for a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created volume.</returns>
    Task<CloudVolume> CreateVolumeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mounts a volume into a container.
    /// </summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="volumeId">The volume identifier.</param>
    /// <param name="mountPath">The absolute path inside the container.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the volume is mounted.</returns>
    Task AttachVolumeAsync(string containerId, string volumeId, string mountPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a container so it costs nothing while idle. Every process in it is lost; its volume persists.
    /// </summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sleeping container.</returns>
    Task<CloudContainer> SleepContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a sleeping container again. The processes are new; nothing that ran before sleep is restored.
    /// </summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The running container with an incremented <see cref="CloudContainer.BootGeneration"/>.</returns>
    Task<CloudContainer> WakeContainerAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends text to a file on a volume, creating the file when it does not exist.
    /// </summary>
    /// <param name="volumeId">The volume identifier.</param>
    /// <param name="path">The file path relative to the volume root.</param>
    /// <param name="text">The text to append.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the text is durable on the volume.</returns>
    Task AppendVolumeFileAsync(string volumeId, string path, string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads a whole file from a volume.
    /// </summary>
    /// <param name="volumeId">The volume identifier.</param>
    /// <param name="path">The file path relative to the volume root.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The file text, or <see langword="null"/> when the file does not exist.</returns>
    Task<string?> ReadVolumeFileAsync(string volumeId, string path, CancellationToken cancellationToken = default);
}
