namespace OmpOS.Workspace;

/// <summary>
/// Holds the current workspace convention snapshot and reloads it when convention files change.
/// Changes are watched on the convention folder only, not on the whole tree. <see cref="Reload"/> performs a
/// reload synchronously and is the deterministic entry point for callers and tests.
/// </summary>
public sealed class WorkspaceCatalog : IDisposable
{
    private static readonly TimeSpan DebounceInterval = TimeSpan.FromMilliseconds(250);

    private static readonly StringComparison PathComparison =
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly WorkspaceRoot _root;
    private readonly Lock _gate = new();
    private readonly Timer _debounce;
    private readonly FileSystemWatcher _rootWatcher;
    private FileSystemWatcher? _conventionWatcher;
    private WorkspaceSnapshot _current = WorkspaceSnapshot.Empty;
    private bool _disposed;

    /// <summary>
    /// Creates a catalog for a workspace and loads it once. File watching starts immediately.
    /// </summary>
    /// <param name="root">The workspace root.</param>
    public WorkspaceCatalog(WorkspaceRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _root = root;

        _debounce = new Timer(OnDebounceElapsed, state: null, Timeout.Infinite, Timeout.Infinite);
        _rootWatcher = new FileSystemWatcher(root.FullPath)
        {
            Filter = WorkspaceConventions.ConventionFolder,
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.CreationTime | NotifyFilters.LastWrite,
        };
        _rootWatcher.Created += (_, _) => ScheduleReload();
        _rootWatcher.Changed += (_, _) => ScheduleReload();
        _rootWatcher.Deleted += (_, _) => ScheduleReload();
        _rootWatcher.Renamed += (_, _) => ScheduleReload();
        _rootWatcher.Error += (_, _) => ScheduleReload();
        _rootWatcher.EnableRaisingEvents = true;

        Reload();
    }

    /// <summary>
    /// Raised after every successful reload, on the thread that performed it. Read <see cref="Current"/> for the state.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// Gets the snapshot produced by the most recent reload.
    /// </summary>
    public WorkspaceSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    /// <summary>
    /// Reloads every convention file now and publishes the result.
    /// </summary>
    /// <returns>The new snapshot.</returns>
    /// <exception cref="ObjectDisposedException">The catalog has been disposed.</exception>
    public WorkspaceSnapshot Reload() =>
        TryReload() ?? throw new ObjectDisposedException(nameof(WorkspaceCatalog));

    /// <summary>
    /// Stops watching and releases the watchers. The last snapshot remains readable.
    /// </summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _rootWatcher.Dispose();
            _conventionWatcher?.Dispose();
            _conventionWatcher = null;
        }

        _debounce.Dispose();
    }

    private WorkspaceSnapshot? TryReload()
    {
        WorkspaceSnapshot snapshot;
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            // Watch before reading, so that a change made during the read schedules another reload.
            ConfigureConventionWatcher();
            snapshot = WorkspaceLoader.Load(_root);
            _current = snapshot;
        }

        Changed?.Invoke(this, EventArgs.Empty);
        return snapshot;
    }

    private void ConfigureConventionWatcher()
    {
        string? watchPath = null;
        try
        {
            var candidate = _root.Resolve(WorkspaceConventions.ConventionFolder);
            if (Directory.Exists(candidate))
            {
                watchPath = candidate;
            }
        }
        catch (WorkspacePathException)
        {
            // The convention folder escapes the root. The loader reports it; nothing is watched.
        }

        if (_conventionWatcher is not null
            && watchPath is not null
            && string.Equals(_conventionWatcher.Path, watchPath, PathComparison))
        {
            return;
        }

        _conventionWatcher?.Dispose();
        _conventionWatcher = null;
        if (watchPath is null)
        {
            return;
        }

        var watcher = new FileSystemWatcher(watchPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
                | NotifyFilters.Size | NotifyFilters.CreationTime,
        };
        watcher.Created += (_, _) => ScheduleReload();
        watcher.Changed += (_, _) => ScheduleReload();
        watcher.Deleted += (_, _) => ScheduleReload();
        watcher.Renamed += (_, _) => ScheduleReload();
        watcher.Error += (_, _) => ScheduleReload();
        watcher.EnableRaisingEvents = true;
        _conventionWatcher = watcher;
    }

    private void ScheduleReload()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _debounce.Change(DebounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnDebounceElapsed(object? state) => TryReload();
}
