using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Sessions;

/// <summary>
/// The persistent list of agent sessions. Each change is written to a versioned JSON state file before the call returns
/// (see <see cref="AgentSessionStateFile"/> for the atomic write and quarantine rules), so a restart finds the sessions and
/// their native resume references. All state sits behind one lock and no method blocks on a task. A write that fails is
/// logged and leaves the in-memory registry correct; the next change writes the whole file again.
/// </summary>
public sealed class AgentSessionRegistry : ISessionCatalog
{
    /// <summary>The largest number of sessions the registry holds.</summary>
    public const int MaxSessions = 200;

    /// <summary>The title of a session created or loaded without one.</summary>
    internal const string DefaultTitle = "New session";

    private const int MaxTitleLength = 200;
    private const int MaxReferenceLength = 4096;
    private static readonly TimeSpan TouchPersistInterval = TimeSpan.FromSeconds(1);

    private static readonly Action<ILogger, string, Exception?> HandlerFailed = LoggerMessage.Define<string>(
        LogLevel.Warning,
        new EventId(10, "AgentSessionChangedHandlerFailed"),
        "A handler of the session registry's Changed event threw {ExceptionType}; the registry is unaffected.");

    private readonly Lock _gate = new();
    private readonly Dictionary<string, AgentSessionRecord> _records = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTimeOffset> _lastTouchWrite = new(StringComparer.Ordinal);
    private readonly AgentSessionStateFile _file;
    private readonly TimeProvider _clock;
    private readonly ILogger _logger;
    private readonly AgentSessionRegistryOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSessionRegistry"/> class and loads the state file synchronously. A
    /// missing file is an empty registry. An invalid file is renamed to <c>&lt;file&gt;.quarantined-&lt;utc&gt;</c> and the registry
    /// starts empty. The constructor never throws for the content of the file.
    /// </summary>
    /// <param name="stateFilePath">The state file. Its directory is created on the first write.</param>
    /// <param name="timeProvider">The clock for activity, heartbeat and quarantine times.</param>
    /// <param name="logger">Receives warnings that carry file names only, or null to discard them.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    public AgentSessionRegistry(
        string stateFilePath,
        TimeProvider timeProvider,
        ILogger? logger = null,
        AgentSessionRegistryOptions? options = null)
        : this(stateFilePath, timeProvider, logger, options, loaded: null)
    {
    }

    private AgentSessionRegistry(
        string stateFilePath,
        TimeProvider timeProvider,
        ILogger? logger,
        AgentSessionRegistryOptions? options,
        List<AgentSessionRecord>? loaded)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var fullPath = Path.GetFullPath(stateFilePath);
        _clock = timeProvider;
        _logger = logger ?? NullLogger.Instance;
        _options = options ?? new AgentSessionRegistryOptions();
        _file = new AgentSessionStateFile(fullPath, ResolveStateRoot(fullPath, _options), _clock, _logger);
        foreach (var record in loaded ?? _file.Load())
        {
            _records[record.Id] = record;
        }
    }

    /// <inheritdoc />
    public event EventHandler? Changed;

    /// <summary>Opens the registry over a state file, reading it asynchronously. The load rules are those of the constructor.</summary>
    /// <param name="stateFilePath">The state file. Its directory is created on the first write.</param>
    /// <param name="timeProvider">The clock for activity, heartbeat and quarantine times.</param>
    /// <param name="logger">Receives warnings that carry file names only, or null to discard them.</param>
    /// <param name="options">The options, or null for the defaults.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The opened registry.</returns>
    public static async Task<AgentSessionRegistry> OpenAsync(
        string stateFilePath,
        TimeProvider timeProvider,
        ILogger? logger,
        AgentSessionRegistryOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateFilePath);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var effective = logger ?? NullLogger.Instance;
        var effectiveOptions = options ?? new AgentSessionRegistryOptions();
        var fullPath = Path.GetFullPath(stateFilePath);
        var file = new AgentSessionStateFile(fullPath, ResolveStateRoot(fullPath, effectiveOptions), timeProvider, effective);
        var loaded = await file.LoadAsync(cancellationToken).ConfigureAwait(false);
        return new AgentSessionRegistry(fullPath, timeProvider, effective, effectiveOptions, loaded);
    }

    /// <summary>Creates a session in the <see cref="SessionLifecycle.Created"/> state and persists it.</summary>
    /// <param name="title">The title. A blank title becomes a default title; a title longer than 200 characters is cut.</param>
    /// <param name="workspaceRoot">The absolute path of the workspace, or null when unknown.</param>
    /// <returns>The new record. Its identifier is <c>s-</c> and 26 lowercase base32 characters.</returns>
    /// <exception cref="AgentSessionLimitException">The registry already holds <see cref="MaxSessions"/> sessions.</exception>
    public AgentSessionRecord Create(string? title, string? workspaceRoot)
    {
        AgentSessionRecord record;
        lock (_gate)
        {
            if (_records.Count >= MaxSessions)
            {
                throw new AgentSessionLimitException(MaxSessions);
            }

            var id = AgentSessionIds.New();
            while (_records.ContainsKey(id))
            {
                id = AgentSessionIds.New();
            }

            var now = _clock.GetUtcNow();
            record = new AgentSessionRecord
            {
                Id = id,
                Title = NormalizeTitle(title) ?? DefaultTitle,
                Lifecycle = SessionLifecycle.Created,
                CreatedAt = now,
                LastActivity = now,
                Heartbeat = now,
                WorkspaceRoot = string.IsNullOrWhiteSpace(workspaceRoot) ? null : workspaceRoot,
                StateDirectory = _file.StateDirectoryFor(id),
            };
            _records[id] = record;
            Persist();
        }

        RaiseChanged();
        return record;
    }

    /// <summary>Renames a session.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="title">The new title; at most 200 characters are kept.</param>
    /// <returns>True when the session exists.</returns>
    /// <exception cref="ArgumentException"><paramref name="title"/> is blank.</exception>
    public bool Rename(string id, string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        var normalized = NormalizeTitle(title)!;
        return Update(id, record => record with { Title = normalized });
    }

    /// <summary>Sets the lifecycle of a session. A transition to <see cref="SessionLifecycle.Running"/> also refreshes the heartbeat, so a resumed session is not stale at once.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="lifecycle">The new lifecycle.</param>
    /// <returns>True when the session exists.</returns>
    public bool SetLifecycle(string id, SessionLifecycle lifecycle)
    {
        if (!Enum.IsDefined(lifecycle))
        {
            throw new ArgumentOutOfRangeException(nameof(lifecycle));
        }

        var now = _clock.GetUtcNow();
        return Update(id, record => record.Lifecycle == lifecycle
            ? record
            : record with
            {
                Lifecycle = lifecycle,
                LastActivity = now,
                Heartbeat = lifecycle == SessionLifecycle.Running ? now : record.Heartbeat,
            });
    }

    /// <summary>Records the omp session behind the ACP session and the native reference that resumes it after a restart.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="acpSessionId">The identifier the engine reported.</param>
    /// <param name="resumeReference">The native omp session reference, or null when the engine gave none. It is never logged.</param>
    /// <returns>True when the session exists.</returns>
    /// <exception cref="ArgumentException">A value is blank or longer than 4096 characters.</exception>
    public bool SetAcpSession(string id, string acpSessionId, string? resumeReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acpSessionId);
        if (acpSessionId.Length > MaxReferenceLength || resumeReference is { Length: > MaxReferenceLength })
        {
            throw new ArgumentException("A session reference is longer than 4096 characters.", nameof(resumeReference));
        }

        var reference = string.IsNullOrWhiteSpace(resumeReference) ? null : resumeReference;
        return Update(id, record => record with { AcpSessionId = acpSessionId, ResumeReference = reference });
    }

    /// <summary>Records the Graph identifier of the session's plan.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="graphId">The Graph identifier, or null to clear it.</param>
    /// <returns>True when the session exists.</returns>
    public bool SetGraphId(string id, string? graphId)
    {
        var value = string.IsNullOrWhiteSpace(graphId) ? null : graphId;
        return Update(id, record => record with { GraphId = value });
    }

    /// <summary>Records the task the session works on.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="taskId">The task identifier, or null to clear it.</param>
    /// <returns>True when the session exists.</returns>
    public bool SetTaskId(string id, string? taskId)
    {
        var value = string.IsNullOrWhiteSpace(taskId) ? null : taskId;
        return Update(id, record => record with { TaskId = value });
    }

    /// <summary>
    /// Records activity and a heartbeat for a session. The in-memory record is always current. The state file is written, and
    /// <see cref="Changed"/> is raised, at most once per second for each session, so a stream of tokens does not write the file
    /// or redraw a list for every token.
    /// </summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>True when the session exists.</returns>
    public bool Touch(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        var persisted = false;
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var current))
            {
                return false;
            }

            var now = _clock.GetUtcNow();
            _records[id] = current with { LastActivity = now, Heartbeat = now };
            if (!_lastTouchWrite.TryGetValue(id, out var last) || now - last >= TouchPersistInterval)
            {
                _lastTouchWrite[id] = now;
                Persist();
                persisted = true;
            }
        }

        if (persisted)
        {
            RaiseChanged();
        }

        return true;
    }

    /// <summary>Removes a session from the registry. Its state directory is not deleted.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>True when the session existed.</returns>
    public bool Close(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            if (!_records.Remove(id))
            {
                return false;
            }

            _lastTouchWrite.Remove(id);
            Persist();
        }

        RaiseChanged();
        return true;
    }

    /// <summary>Returns the record of a session.</summary>
    /// <param name="id">The session identifier.</param>
    /// <param name="record">The record when found.</param>
    /// <returns>True when the session exists.</returns>
    public bool TryGet(string id, [MaybeNullWhen(false)] out AgentSessionRecord record)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            return _records.TryGetValue(id, out record!);
        }
    }

    /// <summary>Returns the full records, most recently active first. They carry resume references, so they are for the host and never for display.</summary>
    /// <returns>A snapshot that later changes do not affect.</returns>
    public IReadOnlyList<AgentSessionRecord> SnapshotRecords() => Ordered();

    /// <inheritdoc />
    public IReadOnlyList<SessionSummary> Snapshot() => [.. Ordered().Select(record => record.ToSummary())];

    /// <summary>
    /// Marks every session that was <see cref="SessionLifecycle.Running"/> as <see cref="SessionLifecycle.Lost"/>. Call it once
    /// after opening the registry at application start: no engine is attached to a session then, whatever its last heartbeat
    /// says. Created, Paused, Completed, Failed and already Lost sessions are unchanged.
    /// </summary>
    /// <returns>The identifiers that became Lost, most recently active first.</returns>
    public IReadOnlyList<string> RecoverAfterRestart() => MarkLost(record => record.Lifecycle == SessionLifecycle.Running);

    /// <summary>
    /// Marks every Running session whose heartbeat is older than <see cref="AgentSessionRegistryOptions.StaleAfter"/> as
    /// <see cref="SessionLifecycle.Lost"/>. Call it periodically while the application runs.
    /// </summary>
    /// <returns>The identifiers that became Lost, most recently active first.</returns>
    public IReadOnlyList<string> DetectLost()
    {
        var now = _clock.GetUtcNow();
        return MarkLost(record => record.Lifecycle == SessionLifecycle.Running && now - record.Heartbeat > _options.StaleAfter);
    }

    private static string ResolveStateRoot(string fullPath, AgentSessionRegistryOptions options) =>
        string.IsNullOrWhiteSpace(options.StateRoot)
            ? Path.GetDirectoryName(fullPath) ?? Path.GetFullPath(".")
            : Path.GetFullPath(options.StateRoot);

    private static string? NormalizeTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var trimmed = title.Trim();
        return trimmed.Length > MaxTitleLength ? trimmed[..MaxTitleLength] : trimmed;
    }

    private List<AgentSessionRecord> Ordered()
    {
        List<AgentSessionRecord> records;
        lock (_gate)
        {
            records = [.. _records.Values];
        }

        records.Sort(static (left, right) =>
        {
            var byActivity = right.LastActivity.CompareTo(left.LastActivity);
            if (byActivity != 0)
            {
                return byActivity;
            }

            var byCreation = right.CreatedAt.CompareTo(left.CreatedAt);
            return byCreation != 0 ? byCreation : string.CompareOrdinal(left.Id, right.Id);
        });
        return records;
    }

    private IReadOnlyList<string> MarkLost(Func<AgentSessionRecord, bool> shouldBeLost)
    {
        List<AgentSessionRecord> lost = [];
        lock (_gate)
        {
            foreach (var record in _records.Values.ToArray())
            {
                if (shouldBeLost(record))
                {
                    _records[record.Id] = record with { Lifecycle = SessionLifecycle.Lost };
                    lost.Add(record);
                }
            }

            if (lost.Count > 0)
            {
                Persist();
            }
        }

        if (lost.Count == 0)
        {
            return [];
        }

        RaiseChanged();
        return
        [
            .. lost
                .OrderByDescending(static record => record.LastActivity)
                .ThenBy(static record => record.Id, StringComparer.Ordinal)
                .Select(static record => record.Id),
        ];
    }

    private bool Update(string id, Func<AgentSessionRecord, AgentSessionRecord> change)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        lock (_gate)
        {
            if (!_records.TryGetValue(id, out var current))
            {
                return false;
            }

            var next = change(current);
            if (next == current)
            {
                return true;
            }

            _records[id] = next;
            Persist();
        }

        RaiseChanged();
        return true;
    }

    // Called under the lock. A failed write is logged by the state file and does not undo the in-memory change.
    private void Persist() => _file.Write(_records.Values);

    private void RaiseChanged()
    {
        var handlers = Changed;
        if (handlers is null)
        {
            return;
        }

        foreach (var handler in handlers.GetInvocationList().Cast<EventHandler>())
        {
            try
            {
                handler(this, EventArgs.Empty);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // A subscriber must never break the registry or the other subscribers.
                HandlerFailed(_logger, ex.GetType().Name, null);
            }
        }
    }
}
