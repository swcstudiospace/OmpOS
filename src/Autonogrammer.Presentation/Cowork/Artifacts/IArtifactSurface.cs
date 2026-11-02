namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The isolated surface that shows one artifact version. The desktop implementation is a bridge-off web view; the web head
/// and tests use <see cref="FakeArtifactSurface"/>. The surface carries identity only: the host builds the document from
/// stored bytes and the document policy, so this contract never sees content.
/// </summary>
public interface IArtifactSurface
{
    /// <summary>Gets the current state.</summary>
    ArtifactSurfaceState State { get; }

    /// <summary>Gets the reason the surface is <see cref="ArtifactSurfaceState.Unavailable"/>, or <see cref="ArtifactSurfaceReason.None"/>.</summary>
    ArtifactSurfaceReason Reason { get; }

    /// <summary>Occurs after <see cref="State"/> or <see cref="Reason"/> changes.</summary>
    event EventHandler? StateChanged;

    /// <summary>Occurs when a navigation after the first document load was cancelled. Every such navigation is reported.</summary>
    event EventHandler<ArtifactNavigationBlockedEventArgs>? NavigationBlocked;

    /// <summary>Loads one version into the surface.</summary>
    /// <param name="request">The version identity and the origins approved for it.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the surface has accepted the document.</returns>
    ValueTask LoadAsync(ArtifactSurfaceRequest request, CancellationToken cancellationToken);

    /// <summary>Stops the running document. The caller destroys the surface afterwards.</summary>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>A task that completes when the document has stopped.</returns>
    ValueTask StopAsync(CancellationToken cancellationToken);

    /// <summary>Destroys the surface. Its document and any state are discarded; nothing is preserved.</summary>
    void Destroy();
}
