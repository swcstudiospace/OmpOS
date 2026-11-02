using Autonogrammer.Presentation.Cowork.Artifacts;
using Avalonia.Controls;
using Avalonia.Platform;

namespace Autonogrammer.Native.Views.Cowork.Artifacts;

/// <summary>
/// The desktop artifact surface (D-20, D-21). It loads one host-built document with <c>NavigateToString</c>, after the host
/// document's content-security-policy meta element is checked to be first. The surface never subscribes to
/// <c>WebMessageReceived</c>, so the page has no message path into the app. Devtools are off. Where the platform exposes the
/// switch, the store is private and non-persistent. The first navigation is the host document itself; every later
/// navigation, new window, meta refresh or form submit is cancelled and reported through <see cref="NavigationBlocked"/>.
/// </summary>
/// <remarks>
/// The public Avalonia 12.1.1 API has no switch for the default context menu or for the post-message bridge, so the surface
/// cannot set either. The bridge stays unreachable because no handler is attached. Both are verified by the live probe.
/// </remarks>
public sealed class NativeWebViewArtifactSurface : IArtifactSurface
{
    /// <summary>The address the host document is loaded against. The policy sets <c>base-uri 'none'</c>, so no document names a base.</summary>
    public static readonly Uri DocumentBase = new("about:blank");

    /// <summary>The start of the meta element every host document begins with, as <c>ArtifactDocumentPolicy.MetaElement</c> writes it.</summary>
    public const string PolicyMetaPrefix = "<meta http-equiv=\"Content-Security-Policy\" content=\"";

    private readonly Func<ArtifactSurfaceRequest, ArtifactDocumentResult> _documentSource;
    private readonly Border _host = new();
    private NativeWebView? _view;
    private bool _allowFirstLoad;

    /// <summary>Creates the surface.</summary>
    /// <param name="documentSource">Builds the host document for one request. The host reads the stored bytes and applies the policy.</param>
    /// <exception cref="ArgumentNullException"><paramref name="documentSource"/> is null.</exception>
    public NativeWebViewArtifactSurface(Func<ArtifactSurfaceRequest, ArtifactDocumentResult> documentSource)
    {
        ArgumentNullException.ThrowIfNull(documentSource);
        _documentSource = documentSource;
    }

    /// <inheritdoc />
    public ArtifactSurfaceState State { get; private set; } = ArtifactSurfaceState.Idle;

    /// <inheritdoc />
    public ArtifactSurfaceReason Reason { get; private set; } = ArtifactSurfaceReason.None;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <inheritdoc />
    public event EventHandler<ArtifactNavigationBlockedEventArgs>? NavigationBlocked;

    /// <summary>Gets the control the panel places in its frame. It holds the web view while a document is loaded and is empty after destroy.</summary>
    public Border View => _host;

    /// <summary>Gets whether the web view currently exists.</summary>
    public bool HasView => _view is not null;

    /// <inheritdoc />
    public ValueTask LoadAsync(ArtifactSurfaceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        DetachView();

        var result = _documentSource(request);
        if (result.Document is null)
        {
            Fail(result.Reason == ArtifactSurfaceReason.None ? ArtifactSurfaceReason.DocumentRefused : result.Reason);
            return ValueTask.CompletedTask;
        }

        if (!result.Document.StartsWith(PolicyMetaPrefix, StringComparison.Ordinal))
        {
            Fail(ArtifactSurfaceReason.DocumentRefused);
            return ValueTask.CompletedTask;
        }

        var view = CreateView();
        if (view is null)
        {
            Fail(ArtifactSurfaceReason.SurfaceUnavailable);
            return ValueTask.CompletedTask;
        }

        _view = view;
        _host.Child = view;
        _allowFirstLoad = true;
        Enter(ArtifactSurfaceState.Loading, ArtifactSurfaceReason.None);
        try
        {
            view.NavigateToString(result.Document, DocumentBase);
        }
        catch (InvalidOperationException)
        {
            DetachView();
            Fail(ArtifactSurfaceReason.SurfaceUnavailable);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask StopAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _allowFirstLoad = false;
        if (_view is not null)
        {
            _ = _view.Stop();
        }

        Enter(ArtifactSurfaceState.Stopped, ArtifactSurfaceReason.None);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public void Destroy()
    {
        DetachView();
        Enter(ArtifactSurfaceState.Idle, ArtifactSurfaceReason.None);
    }

    /// <summary>Applies the environment switches the platform exposes: devtools off, and a private non-persistent store.</summary>
    /// <param name="args">The environment request of the platform adapter.</param>
    internal static void ConfigureEnvironment(WebViewEnvironmentRequestedEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);
        args.EnableDevTools = false;
        switch (args)
        {
            case WindowsWebView2EnvironmentRequestedEventArgs windows:
                windows.IsInPrivateModeEnabled = true;
                break;
            case AppleWKWebViewEnvironmentRequestedEventArgs apple:
                apple.NonPersistentDataStore = true;
                break;
            case GtkWebViewEnvironmentRequestedEventArgs gtk:
                gtk.EphemeralDataManager = true;
                break;
        }
    }

    /// <summary>Decides one navigation. The first navigation to <see cref="DocumentBase"/> after a load is the host document; all else is cancelled and reported.</summary>
    /// <param name="target">The requested target.</param>
    /// <returns>True when the navigation is cancelled.</returns>
    internal bool CancelNavigation(Uri target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var isHostDocument = _allowFirstLoad && Equals(target, DocumentBase);
        _allowFirstLoad = false;
        if (isHostDocument)
        {
            return false;
        }

        NavigationBlocked?.Invoke(this, new ArtifactNavigationBlockedEventArgs(target));
        return true;
    }

    private NativeWebView? CreateView()
    {
        try
        {
            var view = new NativeWebView();
            view.EnvironmentRequested += OnEnvironmentRequested;
            view.NavigationStarted += OnNavigationStarted;
            view.NavigationCompleted += OnNavigationCompleted;
            view.NewWindowRequested += OnNewWindowRequested;
            return view;
        }
        catch (PlatformNotSupportedException)
        {
            return null;
        }
    }

    private static void OnEnvironmentRequested(object? sender, WebViewEnvironmentRequestedEventArgs e) => ConfigureEnvironment(e);

    private void OnNavigationStarted(object? sender, WebViewNavigationStartingEventArgs e)
    {
        // A navigation with no target cannot be reported, so it is cancelled without a report.
        e.Cancel = e.Request is not { } target || CancelNavigation(target);
    }

    private void OnNavigationCompleted(object? sender, WebViewNavigationCompletedEventArgs e)
    {
        _allowFirstLoad = false;
        if (State != ArtifactSurfaceState.Loading)
        {
            return;
        }

        if (e.IsSuccess)
        {
            Enter(ArtifactSurfaceState.Running, ArtifactSurfaceReason.None);
        }
        else
        {
            DetachView();
            Fail(ArtifactSurfaceReason.SurfaceUnavailable);
        }
    }

    private void OnNewWindowRequested(object? sender, WebViewNewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (e.Request is { } target)
        {
            NavigationBlocked?.Invoke(this, new ArtifactNavigationBlockedEventArgs(target));
        }
    }

    private void DetachView()
    {
        if (_view is null)
        {
            return;
        }

        _view.EnvironmentRequested -= OnEnvironmentRequested;
        _view.NavigationStarted -= OnNavigationStarted;
        _view.NavigationCompleted -= OnNavigationCompleted;
        _view.NewWindowRequested -= OnNewWindowRequested;
        _host.Child = null;
        _view = null;
        _allowFirstLoad = false;
    }

    private void Fail(ArtifactSurfaceReason reason) => Enter(ArtifactSurfaceState.Unavailable, reason);

    private void Enter(ArtifactSurfaceState state, ArtifactSurfaceReason reason)
    {
        State = state;
        Reason = reason;
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
