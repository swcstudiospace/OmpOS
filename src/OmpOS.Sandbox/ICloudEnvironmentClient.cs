namespace OmpOS.Sandbox;

/// <summary>
/// Narrow client over a cloud environment provider (Railway in production): one container and one volume per session.
/// </summary>
/// <remarks>
/// Only a volume survives sleep. A sleeping container keeps no running process, shell or in-memory state.
/// Railway sleeps a serverless service implicitly after roughly 5 to 10 minutes without outbound traffic and there is
/// no stop API, so this interface cannot command sleep: <see cref="RequestIdleAsync"/> declares the container logically
/// idle and reports what the provider says, and <see cref="EnsureAwakeAsync"/> wakes it by waiting for its readiness probe.
/// Volume files are only reachable inside the worker container; a provider without a file endpoint throws
/// <see cref="CloudEnvironmentException"/> with <see cref="CloudEnvironmentFailure.Unsupported"/> from the volume file methods.
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
    /// Declares a container logically idle: OmpOS stops activity in it and lets the provider put it to sleep.
    /// </summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The container with <see cref="CloudContainerPhase.Sleeping"/> when the provider reports it asleep (the in-memory fake
    /// sleeps at once), or with <see cref="CloudContainerPhase.Idle"/> when the provider will sleep it later on its own.
    /// </returns>
    Task<CloudContainer> RequestIdleAsync(string containerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes sure a container is serving: waits until the worker answers its readiness probe.
    /// A failing or 502 wake request is retried for up to a configurable window (60 seconds by default) measured with the
    /// injected <see cref="TimeProvider"/>.
    /// </summary>
    /// <param name="containerId">The container identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The <see cref="CloudContainerPhase.Running"/> container. For a container that was sleeping or idle,
    /// <see cref="CloudContainer.BootGeneration"/> is incremented only when the provider reports that it actually slept.
    /// </returns>
    /// <exception cref="CloudEnvironmentException">The worker did not answer in time (<see cref="CloudEnvironmentFailure.WakeTimeout"/>) or the provider failed.</exception>
    Task<CloudContainer> EnsureAwakeAsync(string containerId, CancellationToken cancellationToken = default);

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
