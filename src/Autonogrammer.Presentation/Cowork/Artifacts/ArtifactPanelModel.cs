using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// The artifact panel. The surface exists only while the panel shows it: hide, stop, close and sign-out destroy it and
/// nothing is preserved (D-22). Session grants live only while the surface does; version grants live in the ledger.
/// </summary>
public sealed class ArtifactPanelModel : ObservableModel, IDockPanel, IDisposable
{
    /// <summary>The region label the chrome shows (ADR-0033 D7.5).</summary>
    public const string ChromeLabel = "Generated content, not part of Autonogrammer";

    /// <summary>The status line when no origin is approved.</summary>
    public const string SandboxStatus = "Running in a sandbox. No network.";

    /// <summary>The status line when at least one origin is approved for the shown version.</summary>
    public const string ApprovedStatus = "Running in a sandbox. Requests go only to the approved hosts.";

    private readonly IArtifactSurface _surface;
    private readonly ArtifactPanelOptions _options;
    private readonly List<Uri> _blockedNavigations = [];
    private ArtifactPanelItem? _current;
    private ArtifactVersionKey? _sessionKey;
    private IReadOnlyList<string> _sessionOrigins = [];
    private bool _open;
    private bool _autoOpened;
    private string _announcement = string.Empty;

    /// <summary>Creates the panel over a surface.</summary>
    /// <param name="surface">The isolated surface.</param>
    /// <param name="options">The panel settings.</param>
    /// <exception cref="ArgumentNullException">A parameter is null.</exception>
    public ArtifactPanelModel(IArtifactSurface surface, ArtifactPanelOptions options)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(options);
        _surface = surface;
        _options = options;
        _surface.StateChanged += OnSurfaceStateChanged;
        _surface.NavigationBlocked += OnNavigationBlocked;
    }

    /// <inheritdoc />
    public string Id => "artifacts";

    /// <inheritdoc />
    public string Title => "Artifacts";

    /// <inheritdoc />
    public object Content => this;

    /// <summary>Gets the network and storage grants for the panel. The desktop composition reads it for Access.</summary>
    public ArtifactGrantLedger Grants { get; } = new();

    /// <summary>Gets whether the panel is open.</summary>
    public bool IsOpen => _open;

    /// <summary>Gets the shown version, or null.</summary>
    public ArtifactPanelItem? Current => _current;

    /// <summary>Gets the labelled heading, for example "Artifact: Plan, version 3 of 5".</summary>
    public string Heading => _current is null
        ? "Artifact"
        : FormattableString.Invariant($"Artifact: {_current.Title}, version {_current.Number} of {_current.VersionCount}");

    /// <summary>Gets the surface state.</summary>
    public ArtifactSurfaceState SurfaceState => _surface.State;

    /// <summary>Gets why the surface is unavailable, or <see cref="ArtifactSurfaceReason.None"/>.</summary>
    public ArtifactSurfaceReason SurfaceReason => _surface.Reason;

    /// <summary>Gets every navigation the surface cancelled since the panel was last closed.</summary>
    public IReadOnlyList<Uri> BlockedNavigations => _blockedNavigations;

    /// <summary>Gets the one polite live-region sentence.</summary>
    public string Announcement => _announcement;

    /// <summary>Gets the origins approved for the shown version.</summary>
    public IReadOnlyList<string> ApprovedOrigins
    {
        get
        {
            if (_current is null)
            {
                return [];
            }

            List<string> origins = [];
            if (_sessionKey == _current.Key)
            {
                origins.AddRange(_sessionOrigins);
            }
            foreach (var origin in Grants.ApprovedFor(_current.Key))
            {
                if (!origins.Contains(origin, StringComparer.Ordinal))
                {
                    origins.Add(origin);
                }
            }

            return origins;
        }
    }

    /// <summary>Gets the status line: the sandbox sentence, or the approved-hosts sentence when a host is approved.</summary>
    public string StatusLine => ApprovedOrigins.Count == 0 ? SandboxStatus : ApprovedStatus;

    /// <summary>Gets whether the banner and the Access action show: the shown version declares an origin no grant covers.</summary>
    public bool ReviewAccessAvailable =>
        _current is not null && _current.RequestedOrigins.Except(ApprovedOrigins, StringComparer.Ordinal).Any();

    /// <summary>Shows one version, loading it into the surface with the origins approved for it.</summary>
    /// <param name="item">The version.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A task that completes when the surface accepted the version.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public async ValueTask ShowAsync(ArtifactPanelItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        _current = item;
        _open = true;
        NotifyPanel();
        await LoadCurrentAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Records that the agent created or updated an artifact. The announcement never moves focus. The first artifact of a
    /// session opens the panel only when <see cref="ArtifactPanelOptions.AutoOpen"/> is set.
    /// </summary>
    /// <param name="item">The new version.</param>
    /// <param name="cancellationToken">Cancels the load if the panel opens.</param>
    /// <returns>A task that completes when the announcement is set and any auto-open has loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="item"/> is null.</exception>
    public ValueTask NotifyVersionCreatedAsync(ArtifactPanelItem item, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(item);
        Announce(FormattableString.Invariant($"Version {item.Number} created by the agent"));
        if (!_options.AutoOpen || _autoOpened)
        {
            return ValueTask.CompletedTask;
        }

        _autoOpened = true;
        return ShowAsync(item, cancellationToken);
    }

    /// <summary>Stops the running document and destroys the surface. The panel stays open on the same version.</summary>
    /// <param name="cancellationToken">Cancels the stop.</param>
    /// <returns>A task that completes when the surface stopped and was destroyed.</returns>
    public async ValueTask StopAsync(CancellationToken cancellationToken)
    {
        await _surface.StopAsync(cancellationToken).ConfigureAwait(false);
        DestroySurface();
        NotifyPanel();
    }

    /// <summary>Hides the panel and destroys the surface. Nothing is preserved; Show loads again.</summary>
    public void Hide()
    {
        _open = false;
        _current = null;
        DestroySurface();
        NotifyPanel();
    }

    /// <summary>Closes the panel: hides it and clears the blocked-navigation report and the announcement.</summary>
    public void Close()
    {
        Hide();
        _blockedNavigations.Clear();
        _announcement = string.Empty;
        NotifyPropertyChanged(nameof(BlockedNavigations));
        NotifyPropertyChanged(nameof(Announcement));
    }

    /// <summary>Signs out: closes the panel, clears every grant and re-arms the first-artifact auto-open.</summary>
    public void SignOut()
    {
        Close();
        Grants.Clear();
        _autoOpened = false;
    }

    /// <summary>
    /// Applies the user's access choice. A denial, or an empty origin list, approves nothing. An approval for the shown version
    /// rebuilds the surface with the new origins.
    /// </summary>
    /// <param name="decision">The decision from <see cref="ArtifactAccessReviewModel"/>.</param>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns>A task that completes when the grant is applied and any rebuild loaded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="decision"/> is null.</exception>
    public async ValueTask ApplyAsync(ArtifactAccessDecision decision, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);
        if (decision.Origins.Count == 0)
        {
            return;
        }

        switch (decision.Choice)
        {
            case ArtifactAccessChoice.AllowSession:
                _sessionKey = decision.Version;
                _sessionOrigins = decision.Origins;
                break;
            case ArtifactAccessChoice.AllowVersion:
                Grants.Add(decision.Version, decision.Origins);
                break;
            case ArtifactAccessChoice.Deny:
            default:
                return;
        }

        if (_current is not null && _current.Key == decision.Version)
        {
            await RebuildAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Revokes every grant for the shown version and rebuilds the surface with no network.</summary>
    /// <param name="cancellationToken">Cancels the rebuild.</param>
    /// <returns>A task that completes when the surface is rebuilt.</returns>
    public async ValueTask RevokeAccessAsync(CancellationToken cancellationToken)
    {
        if (_current is null)
        {
            return;
        }

        Grants.Revoke(_current.Key);
        if (_sessionKey == _current.Key)
        {
            _sessionKey = null;
            _sessionOrigins = [];
        }

        await RebuildAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Builds the Access review for the shown version, or null when it declares no origin.</summary>
    /// <param name="clock">The clock the review uses for its timeout.</param>
    /// <param name="timeout">How long the review waits before it denies.</param>
    /// <returns>The review, or null.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="clock"/> is null.</exception>
    public ArtifactAccessReviewModel? CreateAccessReview(TimeProvider clock, TimeSpan timeout)
    {
        ArgumentNullException.ThrowIfNull(clock);
        if (_current is null || _current.RequestedOrigins.Count == 0)
        {
            return null;
        }

        return new ArtifactAccessReviewModel(_current.Key, _current.RequestedOrigins, clock, timeout);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _surface.StateChanged -= OnSurfaceStateChanged;
        _surface.NavigationBlocked -= OnNavigationBlocked;
        DestroySurface();
    }

    private async ValueTask RebuildAsync(CancellationToken cancellationToken)
    {
        _surface.Destroy();
        await LoadCurrentAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask LoadCurrentAsync(CancellationToken cancellationToken)
    {
        if (_current is null)
        {
            return;
        }

        var request = new ArtifactSurfaceRequest(
            _current.ArtifactId,
            _current.Number,
            _current.ContentSha256,
            _current.Kind,
            ApprovedOrigins);
        await _surface.LoadAsync(request, cancellationToken).ConfigureAwait(false);
        NotifyPanel();
    }

    private void DestroySurface()
    {
        _surface.Destroy();
        _sessionKey = null;
        _sessionOrigins = [];
    }

    private void OnSurfaceStateChanged(object? sender, EventArgs e)
    {
        NotifyPropertyChanged(nameof(SurfaceState));
        NotifyPropertyChanged(nameof(SurfaceReason));
    }

    private void OnNavigationBlocked(object? sender, ArtifactNavigationBlockedEventArgs e)
    {
        _blockedNavigations.Add(e.Url);
        Announce("Navigation blocked: " + e.Url.OriginalString);
        NotifyPropertyChanged(nameof(BlockedNavigations));
    }

    private void Announce(string text)
    {
        _announcement = text;
        NotifyPropertyChanged(nameof(Announcement));
    }

    private void NotifyPanel()
    {
        NotifyPropertyChanged(nameof(IsOpen));
        NotifyPropertyChanged(nameof(Current));
        NotifyPropertyChanged(nameof(Heading));
        NotifyPropertyChanged(nameof(StatusLine));
        NotifyPropertyChanged(nameof(ReviewAccessAvailable));
    }
}
