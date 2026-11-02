namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// An in-memory surface for hosts and tests. It records every transition, every load and every blocked navigation
/// so headless tests assert against it without a web view.
/// </summary>
public sealed class FakeArtifactSurface : IArtifactSurface
{
    private readonly List<ArtifactSurfaceState> _transitions = [ArtifactSurfaceState.Idle];
    private readonly List<ArtifactSurfaceRequest> _loads = [];
    private readonly List<Uri> _blockedUrls = [];

    /// <inheritdoc />
    public ArtifactSurfaceState State { get; private set; } = ArtifactSurfaceState.Idle;

    /// <inheritdoc />
    public ArtifactSurfaceReason Reason { get; private set; } = ArtifactSurfaceReason.None;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ArtifactNavigationBlockedEventArgs>? NavigationBlocked;

    /// <summary>Gets every state the surface has entered, in order, starting with <see cref="ArtifactSurfaceState.Idle"/>.</summary>
    public IReadOnlyList<ArtifactSurfaceState> Transitions => _transitions;

    /// <summary>Gets every load request, in order.</summary>
    public IReadOnlyList<ArtifactSurfaceRequest> Loads => _loads;

    /// <summary>Gets every blocked navigation target, in order.</summary>
    public IReadOnlyList<Uri> BlockedUrls => _blockedUrls;

    /// <summary>Gets how many times the surface was destroyed.</summary>
    public int DestroyCount { get; private set; }

    /// <inheritdoc />
    public ValueTask LoadAsync(ArtifactSurfaceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        _loads.Add(request);
        Reason = ArtifactSurfaceReason.None;
        Enter(ArtifactSurfaceState.Loading);
        Enter(ArtifactSurfaceState.Running);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Enter(ArtifactSurfaceState.Stopped);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Destroy()
    {
        DestroyCount++;
        Reason = ArtifactSurfaceReason.None;
        Enter(ArtifactSurfaceState.Idle);
    }

    /// <summary>Simulates a cancelled navigation. The state becomes <see cref="ArtifactSurfaceState.NavigationBlocked"/>.</summary>
    /// <param name="url">The requested target.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is null.</exception>
    public void SimulateBlockedNavigation(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        _blockedUrls.Add(url);
        Enter(ArtifactSurfaceState.NavigationBlocked);
        NavigationBlocked?.Invoke(this, new ArtifactNavigationBlockedEventArgs(url));
    }

    /// <summary>Simulates a surface that cannot show the document.</summary>
    /// <param name="reason">The reason. <see cref="ArtifactSurfaceReason.None"/> is refused.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is <see cref="ArtifactSurfaceReason.None"/>.</exception>
    public void SimulateUnavailable(ArtifactSurfaceReason reason)
    {
        if (reason == ArtifactSurfaceReason.None)
        {
            throw new ArgumentException("An unavailable surface needs a reason.", nameof(reason));
        }

        Reason = reason;
        Enter(ArtifactSurfaceState.Unavailable);
    }

    private void Enter(ArtifactSurfaceState next)
    {
        State = next;
        _transitions.Add(next);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
