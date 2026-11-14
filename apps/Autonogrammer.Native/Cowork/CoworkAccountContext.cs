using Autonogrammer.Accounts;
using Autonogrammer.Cowork;
using Autonogrammer.Cowork.Memory;
using Autonogrammer.Cowork.Projects;
using Autonogrammer.Cowork.Sessions;

namespace Autonogrammer.Native.Cowork;

/// <summary>
/// The head's account data for interactive Cowork sessions. It keeps one account graph for the signed-in account (its project store, its project sync,
/// its session record and its memory client), keyed by the account folder key. Sign-out or an account switch retires the graph: calls made through it are
/// cancelled, and a call that completes after the switch returns an unavailable snapshot rather than another account's data.
/// Each request reads the active account's bearer when it is sent; a signed-out head sends none.
/// </summary>
internal sealed class CoworkAccountContext : IDisposable
{
    /// <summary>How many entries a session recalls for its brief. The brief is cut to 4 KiB after the recall.</summary>
    public const int RecallCount = 50;

    /// <summary>
    /// The active account's projects after a sync. <see cref="AccountKey"/> names the account the rows belong to; an unavailable read carries no rows.
    /// </summary>
    /// <param name="AccountKey">The folder key of the account, or null when the read is unavailable.</param>
    /// <param name="SyncState">The <see cref="ProjectSyncState"/> of the sync that preceded the read.</param>
    /// <param name="IsReadOnly">Whether the rows are the last synced copy and are read-only.</param>
    /// <param name="Projects">The local projects, archived ones included.</param>
    internal sealed record ProjectsRead(string? AccountKey, string SyncState, bool IsReadOnly, IReadOnlyList<global::Autonogrammer.Cowork.Projects.CoworkProject> Projects)
    {
        /// <summary>Returns the read for a request that has no account: signed out, switched during the call, or refused by the sync.</summary>
        /// <param name="state">The sync state that explains why.</param>
        /// <returns>A read with no account and no rows.</returns>
        public static ProjectsRead Unavailable(string state) => new(null, state, true, []);
    }

    private readonly Lock _lock = new();
    private readonly ICoworkAccountGate _gate;
    private readonly IAccountSession _session;
    private readonly string _stateRoot;
    private readonly HttpClient? _apiClient;
    private AccountScope? _current;
    private bool _disposed;

    /// <summary>Builds the context and brings it in line with the gate's current account. It listens for account changes until disposed.</summary>
    /// <param name="gate">The signed-in account as Cowork storage sees it. Its folder key names the graph.</param>
    /// <param name="session">The head's account session. It supplies the bearer for each request.</param>
    /// <param name="stateRoot">The state directory the account folders live under.</param>
    /// <param name="apiClient">The head's API client, or null when the head has no API address. The context does not dispose it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="gate"/> or <paramref name="session"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="stateRoot"/> is null, empty or white space.</exception>
    public CoworkAccountContext(ICoworkAccountGate gate, IAccountSession session, string stateRoot, HttpClient? apiClient)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        _gate = gate;
        _session = session;
        _stateRoot = Path.GetFullPath(stateRoot);
        _apiClient = apiClient;
        Reconcile();
        _session.StateChanged += OnSessionStateChanged;
    }

    /// <summary>Gets the folder key of the account the context serves, or null while no account is signed in.</summary>
    public string? AccountKey
    {
        get
        {
            lock (_lock)
            {
                return _current?.Key;
            }
        }
    }

    /// <summary>Brings the account graph in line with the gate. Signing in builds it; signing out or switching retires the old one. A disposed context does nothing.</summary>
    public void Reconcile()
    {
        lock (_lock)
        {
            if (!_disposed)
            {
                _ = EnsureScopeLocked();
            }
        }
    }

    /// <summary>Returns whether a snapshot still belongs to the active account.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>True when the snapshot's account is the active account.</returns>
    public bool IsCurrent(CoworkAccountSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (_lock)
        {
            return snapshot.AccountKey is not null
                && _current is not null
                && string.Equals(snapshot.AccountKey, _current.Key, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Syncs the active account, then reads what an interactive session of that account takes at its start. The session record names the project and
    /// whether memory is off for the session; the project, the global instructions and the memory settings are read now, not when the session was created.
    /// </summary>
    /// <param name="sessionId">The session whose record names the project.</param>
    /// <param name="cancellationToken">Cancels the sync and the recall.</param>
    /// <returns>The snapshot. It is unavailable when no account is signed in, when the account changed during the call, or when the sync refused the account.</returns>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ObjectDisposedException">The context is disposed.</exception>
    public async Task<CoworkAccountSnapshot> SnapshotAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        var scope = CurrentScope();
        if (scope is null)
        {
            return CoworkAccountSnapshot.Unavailable(ProjectSyncState.SignedOut);
        }

        using var linked = scope.Link(cancellationToken);
        if (linked is null)
        {
            return CoworkAccountSnapshot.Unavailable(ProjectSyncState.SignedOut);
        }

        try
        {
            var snapshot = await scope.BuildAsync(sessionId, linked.Token).ConfigureAwait(false);
            return IsCurrentScope(scope) ? snapshot : CoworkAccountSnapshot.Unavailable(ProjectSyncState.SignedOut);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The account was retired during the call. The caller gets nothing from the account that was active before.
            return CoworkAccountSnapshot.Unavailable(ProjectSyncState.SignedOut);
        }
    }

    /// <summary>
    /// Syncs the active account and returns its projects. The read is refused when no account is signed in, and a read that finishes after the
    /// account changed is replaced by an unavailable one, so the caller never gets another account's projects.
    /// </summary>
    /// <param name="cancellationToken">Cancels the sync.</param>
    /// <returns>The read.</returns>
    /// <exception cref="ObjectDisposedException">The context is disposed.</exception>
    public async Task<ProjectsRead> ListProjectsAsync(CancellationToken cancellationToken)
    {
        var scope = CurrentScope();
        if (scope is null)
        {
            return ProjectsRead.Unavailable(ProjectSyncState.SignedOut);
        }

        using var linked = scope.Link(cancellationToken);
        if (linked is null)
        {
            return ProjectsRead.Unavailable(ProjectSyncState.SignedOut);
        }

        try
        {
            var read = await scope.ListProjectsAsync(linked.Token).ConfigureAwait(false);
            return IsCurrentScope(scope) ? read : ProjectsRead.Unavailable(ProjectSyncState.SignedOut);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The account was retired during the call.
            return ProjectsRead.Unavailable(ProjectSyncState.SignedOut);
        }
    }

    /// <summary>Retires the account graph and stops listening for account changes. Idempotent.</summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _session.StateChanged -= OnSessionStateChanged;
            RetireCurrentLocked();
        }
    }

    private void OnSessionStateChanged(object? sender, AccountStateChangedEventArgs e) => Reconcile();

    private AccountScope? CurrentScope()
    {
        lock (_lock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return EnsureScopeLocked();
        }
    }

    private bool IsCurrentScope(AccountScope scope)
    {
        lock (_lock)
        {
            return ReferenceEquals(_current, scope);
        }
    }

    private AccountScope? EnsureScopeLocked()
    {
        var key = _gate.IsSignedIn ? _gate.AccountFolderKey : null;
        if (key is null)
        {
            RetireCurrentLocked();
            return null;
        }

        if (_current is not null && string.Equals(_current.Key, key, StringComparison.Ordinal))
        {
            return _current;
        }

        RetireCurrentLocked();
        _current = new AccountScope(key, _stateRoot, _session, _apiClient);
        return _current;
    }

    private void RetireCurrentLocked()
    {
        _current?.Dispose();
        _current = null;
    }

    /// <summary>
    /// The graph of one account. It is retired once: after that it refuses every link, and every call made through it has been cancelled.
    /// </summary>
    private sealed class AccountScope : IDisposable
    {
        private readonly Lock _scopeLock = new();
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ProjectAccountSync _sync;
        private readonly Autonogrammer.Cowork.Projects.CoworkProjectStore _projects;
        private readonly CoworkSessionMetaStore _sessions;
        private readonly HttpMemoryStore? _memory;
        private bool _retired;

        public AccountScope(string key, string stateRoot, IAccountSession session, HttpClient? apiClient)
        {
            Key = key;
            _projects = new Autonogrammer.Cowork.Projects.CoworkProjectStore(stateRoot, key);
            _sessions = new CoworkSessionMetaStore(stateRoot, key);
            _sync = new ProjectAccountSync(new AccountProjectApi(apiClient, session), _projects, new GlobalInstructionsCache(stateRoot, key));
            _memory = apiClient is null
                ? null
                : new HttpMemoryStore(apiClient, token => AccountBearer.TokenOrNullAsync(session, token));
        }

        /// <summary>Gets the folder key of the account.</summary>
        public string Key { get; }

        /// <summary>Links a caller's token to the scope's lifetime. Returns null once the scope is retired. The caller disposes the result.</summary>
        /// <param name="cancellationToken">The caller's token.</param>
        /// <returns>The linked source, or null.</returns>
        public CancellationTokenSource? Link(CancellationToken cancellationToken)
        {
            lock (_scopeLock)
            {
                return _retired ? null : CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            }
        }

        /// <summary>Cancels every call made through this scope and refuses later ones. Idempotent.</summary>
        public void Dispose()
        {
            lock (_scopeLock)
            {
                if (_retired)
                {
                    return;
                }

                _retired = true;
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
        }

        /// <summary>Syncs the account, then reads the session's project and memory.</summary>
        /// <param name="sessionId">The session whose record names the project.</param>
        /// <param name="cancellationToken">Cancels the calls.</param>
        /// <returns>The snapshot of this account.</returns>
        public async Task<CoworkAccountSnapshot> BuildAsync(string sessionId, CancellationToken cancellationToken)
        {
            var state = await _sync.SyncAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(state, ProjectSyncState.SignedOut, StringComparison.Ordinal))
            {
                return CoworkAccountSnapshot.Unavailable(state);
            }

            var (meta, unreadable) = ReadSession(sessionId);
            var (project, projectState) = ResolveProject(meta?.ProjectId);
            var (brief, memoryState) = await RecallAsync(unreadable, meta, project, sessionId, cancellationToken).ConfigureAwait(false);
            return new CoworkAccountSnapshot(
                Key,
                state,
                _sync.IsReadOnly,
                _sync.GlobalInstructions,
                project?.Id,
                projectState,
                project?.Instructions,
                memoryState,
                brief);
        }

        /// <summary>Syncs the account, then returns its local projects, archived ones included.</summary>
        /// <param name="cancellationToken">Cancels the sync.</param>
        /// <returns>The read, with the account it belongs to.</returns>
        public async Task<ProjectsRead> ListProjectsAsync(CancellationToken cancellationToken)
        {
            var state = await _sync.SyncAsync(cancellationToken).ConfigureAwait(false);
            if (string.Equals(state, ProjectSyncState.SignedOut, StringComparison.Ordinal))
            {
                return ProjectsRead.Unavailable(state);
            }

            return new ProjectsRead(Key, state, _sync.IsReadOnly, _projects.List(includeArchived: true));
        }

        private (CoworkSessionMeta? Meta, bool Unreadable) ReadSession(string sessionId)
        {
            try
            {
                return (_sessions.Get(sessionId), false);
            }
            catch (InvalidDataException)
            {
                return (null, true);
            }
        }

        private (Autonogrammer.Cowork.Projects.CoworkProject? Project, string State) ResolveProject(string? projectId)
        {
            if (string.IsNullOrWhiteSpace(projectId))
            {
                return (null, CoworkAccountSnapshot.ProjectNone);
            }

            var found = _projects.List(includeArchived: true)
                .FirstOrDefault(project => string.Equals(project.Id, projectId, StringComparison.Ordinal));
            if (found is null)
            {
                return (null, CoworkAccountSnapshot.ProjectMissing);
            }

            return found.ArchivedAt is null ? (found, CoworkAccountSnapshot.ProjectActive) : (null, CoworkAccountSnapshot.ProjectArchived);
        }

        private async Task<(string Brief, string State)> RecallAsync(
            bool unreadable,
            CoworkSessionMeta? meta,
            Autonogrammer.Cowork.Projects.CoworkProject? project,
            string sessionId,
            CancellationToken cancellationToken)
        {
            if (unreadable)
            {
                return (string.Empty, CoworkAccountSnapshot.MemoryOffUnreadableSession);
            }

            if (meta?.MemoryOff == true)
            {
                return (string.Empty, MemoryCodes.MemoryOffForTask);
            }

            if (_memory is null)
            {
                return (string.Empty, MemoryCodes.Offline);
            }

            // The account's settings are read on every snapshot, so a change made since the session started applies here.
            var settings = await _memory.GetSettingsAsync(Key, cancellationToken).ConfigureAwait(false);
            if (!settings.Enabled)
            {
                return (string.Empty, CoworkAccountSnapshot.MemoryDisabled);
            }

            if (settings.Paused)
            {
                return (string.Empty, MemoryCodes.MemoryPaused);
            }

            // A project session reads account entries only when the project uses them; a session without a project reads account entries.
            var scope = new MemoryScope(Key, project?.Id, project is null || project.UseAccountMemory, sessionId);
            var recall = await _memory.RecallAsync(scope, null, CoworkAccountContext.RecallCount, cancellationToken).ConfigureAwait(false);
            return recall.Succeeded
                ? (MemoryBriefBuilder.Build(recall.Entries), MemoryCodes.Ok)
                : (string.Empty, recall.Code);
        }
    }
}
