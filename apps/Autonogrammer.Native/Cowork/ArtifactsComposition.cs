using Autonogrammer.Accounts;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Abstractions;
using Autonogrammer.Cowork.Artifacts;
using Autonogrammer.Cowork.Artifacts.Stores;
using Autonogrammer.Cowork.Artifacts.Tools;
using Autonogrammer.Native.Views.Cowork.Artifacts;
using Autonogrammer.Presentation.Cowork;
using Autonogrammer.Presentation.Cowork.Artifacts;
using Microsoft.Extensions.Options;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// The desktop artifact composition of one head. The store, the tool provider and the sync service belong to the signed-in account:
/// they exist only while an account is signed in, are keyed by its folder key under <see cref="CoworkAccountFolder.AccountDirectory"/>,
/// and are dropped on sign-out or account switch. The panel is one model for the head; sign-out closes it and clears its grants (D-22).
/// Sync is off by default: <see cref="ArtifactSyncService.Enabled"/> starts false and nothing in this composition turns it on.
/// </summary>
internal sealed class ArtifactsComposition : IDisposable
{
    /// <summary>The reason the tool provider gives while no account is signed in.</summary>
    public const string SignedOutReason = "Artifacts need a signed-in account.";

    private readonly ICoworkAccountGate _accountGate;
    private readonly string _stateRoot;
    private readonly HttpClient? _remoteClient;
    private readonly Func<CancellationToken, ValueTask<string?>> _bearer;
    private readonly IAccountSession? _session;
    private readonly IOptions<ArtifactOptions> _options;
    private readonly TimeProvider _time;
    private readonly ArtifactWriteLimiter _limiter;
    private readonly AccountArtifactTools _tools = new();
    private readonly ArtifactPanelModel _panel;
    private readonly Lock _lock = new();
    private AccountArtifacts? _current;
    private int _disposed;

    /// <summary>Builds the composition and brings it in line with the gate's current account.</summary>
    /// <param name="accountGate">The signed-in account as Cowork storage sees it. Its folder key names the store.</param>
    /// <param name="stateRoot">The state directory the account folders live under.</param>
    /// <param name="remoteClient">The head's API client for sync, or null when the head has no API address. Sync is not built without it. The composition does not dispose it.</param>
    /// <param name="bearer">Supplies the account bearer for sync, or null when there is none.</param>
    /// <param name="session">The account session. Its state changes reconcile the composition, or null when no session signals changes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="accountGate"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stateRoot"/> is null, empty or white space.</exception>
    public ArtifactsComposition(
        ICoworkAccountGate accountGate,
        string stateRoot,
        HttpClient? remoteClient,
        Func<CancellationToken, ValueTask<string?>>? bearer,
        IAccountSession? session)
    {
        ArgumentNullException.ThrowIfNull(accountGate);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _accountGate = accountGate;
        _stateRoot = Path.GetFullPath(stateRoot);
        _remoteClient = remoteClient;
        _bearer = bearer ?? (static _ => ValueTask.FromResult<string?>(null));
        _session = session;
        var options = new ArtifactOptions();
        _options = Options.Create(options);
        _time = TimeProvider.System;
        _limiter = new ArtifactWriteLimiter(_time);
        _panel = new ArtifactPanelModel(
            new NativeWebViewArtifactSurface(static _ => ArtifactDocumentResult.Refused(ArtifactSurfaceReason.DocumentRefused)),
            new ArtifactPanelOptions(options.AutoOpen));
        try
        {
            Reconcile();
        }
        catch
        {
            _panel.Dispose();
            throw;
        }

        if (session is not null)
        {
            session.StateChanged += OnSessionStateChanged;
        }
    }

    /// <summary>Gets the panel. It is registered with the dock host through <see cref="RegisterPanel"/>.</summary>
    public ArtifactPanelModel Panel => _panel;

    /// <summary>
    /// Gets the provider the tool host holds for the whole head. While an account is signed in it offers the artifact tools; while signed out it offers none.
    /// </summary>
    public ICoworkToolProvider ToolProvider => _tools;

    /// <summary>Gets the account's store, or null while no account is signed in.</summary>
    public FileArtifactStore? Store
    {
        get
        {
            lock (_lock)
            {
                return _current?.Store;
            }
        }
    }

    /// <summary>Gets the account's sync service, or null while no account is signed in or the head has no API client.</summary>
    public ArtifactSyncService? Sync
    {
        get
        {
            lock (_lock)
            {
                return _current?.Sync;
            }
        }
    }

    /// <summary>Brings the account-scoped graph in line with the gate: built for the signed-in account, dropped when signed out or switched.</summary>
    /// <exception cref="ObjectDisposedException">The composition is disposed.</exception>
    public void Reconcile()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var key = _accountGate.IsSignedIn ? _accountGate.AccountFolderKey : null;
        lock (_lock)
        {
            if (key is null)
            {
                TearDown();
                return;
            }

            if (_current is not null && string.Equals(_current.Key, key, StringComparison.Ordinal))
            {
                return;
            }

            TearDown();
            _current = Open(key);
            _tools.Attach(_current.Tools);
        }
    }

    /// <summary>Registers the panel with the shell, the same way the browser pane registers. The dock host shows the registered panels.</summary>
    /// <param name="shell">The Cowork shell that owns the dock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="shell"/> is null.</exception>
    public void RegisterPanel(CoworkShellModel shell)
    {
        ArgumentNullException.ThrowIfNull(shell);
        shell.RegisterPanel(_panel);
    }

    /// <summary>Drops the account graph and disposes the panel. Idempotent.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (_session is not null)
        {
            _session.StateChanged -= OnSessionStateChanged;
        }

        lock (_lock)
        {
            TearDown();
        }

        _panel.Dispose();
    }

    private void OnSessionStateChanged(object? sender, AccountStateChangedEventArgs e) => Reconcile();

    private AccountArtifacts Open(string key)
    {
        var store = new FileArtifactStore(CoworkAccountFolder.AccountDirectory(_stateRoot, key), key);
        var service = new ArtifactService(store, _time, _options);
        var tools = new ArtifactToolProvider(service, _limiter, _options);
        var sync = _remoteClient is null
            ? null
            : new ArtifactSyncService(store, new HttpArtifactRemote(_remoteClient, _bearer));
        return new AccountArtifacts(key, store, tools, sync);
    }

    private void TearDown()
    {
        if (_current is null)
        {
            return;
        }

        _tools.Detach();
        _current = null;
        _panel.SignOut();
    }

    private sealed record AccountArtifacts(string Key, FileArtifactStore Store, ArtifactToolProvider Tools, ArtifactSyncService? Sync);

    /// <summary>
    /// The provider the host holds for the head's life. It forwards to the signed-in account's provider and offers nothing while signed out.
    /// </summary>
    private sealed class AccountArtifactTools : ICoworkToolProvider
    {
        private ArtifactToolProvider? _inner;

        /// <inheritdoc />
        public string ProviderId => ArtifactToolProvider.Id;

        /// <summary>Forwards to the given account's provider from now on.</summary>
        /// <param name="provider">The account's provider.</param>
        public void Attach(ArtifactToolProvider provider) => Volatile.Write(ref _inner, provider);

        /// <summary>Stops forwarding. The host then offers no artifact tools.</summary>
        public void Detach() => Volatile.Write(ref _inner, null);

        /// <inheritdoc />
        public IReadOnlyList<CoworkToolDescriptor> Describe() => Volatile.Read(ref _inner)?.Describe() ?? [];

        /// <inheritdoc />
        public ValueTask<CoworkProviderState> GetStateAsync(CoworkSessionContext session, CancellationToken cancellationToken)
        {
            var inner = Volatile.Read(ref _inner);
            return inner is null
                ? ValueTask.FromResult(new CoworkProviderState(CoworkProviderAvailability.Unavailable, SignedOutReason))
                : inner.GetStateAsync(session, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask<CoworkCallPreview> PreviewAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
        {
            var inner = Volatile.Read(ref _inner) ?? throw new InvalidOperationException(SignedOutReason);
            return inner.PreviewAsync(session, invocation, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask<CoworkToolResult> InvokeAsync(CoworkSessionContext session, CoworkToolInvocation invocation, CancellationToken cancellationToken)
        {
            var inner = Volatile.Read(ref _inner);
            return inner is null
                ? ValueTask.FromResult(CoworkToolResult.Failure(SignedOutReason))
                : inner.InvokeAsync(session, invocation, cancellationToken);
        }

        /// <inheritdoc />
        public ValueTask EndSessionAsync(string sessionId, CancellationToken cancellationToken)
        {
            var inner = Volatile.Read(ref _inner);
            return inner is null ? ValueTask.CompletedTask : inner.EndSessionAsync(sessionId, cancellationToken);
        }
    }
}
