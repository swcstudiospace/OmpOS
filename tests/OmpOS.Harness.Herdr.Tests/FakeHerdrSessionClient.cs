using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace OmpOS.Harness.Herdr.Tests;

/// <summary>
/// An in-memory herdr server. No process is started. A server restart moves every running or paused session to
/// <see cref="HerdrSessionState.Lost"/> and keeps its native reference. Each call is recorded in
/// <see cref="Transcript"/> with its outcome. Argument errors are thrown but not recorded.
/// </summary>
internal sealed class FakeHerdrSessionClient : IHerdrSessionClient
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, FakeSession> _sessions = new(StringComparer.Ordinal);
    private readonly List<HerdrTranscriptEntry> _transcript = [];
    private readonly Channel<HerdrSessionId> _beatRequests = Channel.CreateUnbounded<HerdrSessionId>();
    private Func<Task>? _beforeNextBeat;
    private bool _restartBeforeNextAttach;
    private bool _failNextTerminate;
    private int _nativeCount;

    /// <summary>Gets a copy of every recorded call, oldest first.</summary>
    public IReadOnlyList<HerdrTranscriptEntry> Transcript
    {
        get
        {
            lock (_gate)
            {
                return [.. _transcript];
            }
        }
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> CreateAsync(HerdrCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Create", request.Id.Value, () => Create(request), Describe);
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> AttachAsync(HerdrSessionId sessionId, long afterSequence, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Attach", sessionId.Value, () => Attach(sessionId, afterSequence), Describe);
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> DetachAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Detach", sessionId.Value, () => Detach(sessionId), Describe);
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> PauseAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Pause", sessionId.Value, () => Move(sessionId, HerdrSessionState.Running, HerdrSessionState.Paused), Describe);
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> ResumeAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Resume", sessionId.Value, () => Move(sessionId, HerdrSessionState.Paused, HerdrSessionState.Running), Describe);
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> TerminateAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Terminate", sessionId.Value, () => Terminate(sessionId), Describe);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HerdrSessionSnapshot>> ListAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("List", null, () => ListSnapshots(), list => string.Join(',', list.Select(snapshot => snapshot.Id.Value)));
    }

    /// <inheritdoc />
    public async Task<HerdrSessionSnapshot> StatusAsync(HerdrSessionId sessionId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return Transact("Status", sessionId.Value, () => Find(sessionId).Snapshot(), Describe);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<HerdrHeartbeat> HeartbeatsAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        while (await _beatRequests.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(true))
        {
            while (_beatRequests.Reader.TryRead(out var sessionId))
            {
                // The state is read as the beat is produced, not when it was queued. A session that has since been lost or
                // terminated produces no beat.
                if (ReadBeat(sessionId) is not { } beat)
                {
                    continue;
                }

                var beforeNextBeat = TakeBeforeNextBeat();
                if (beforeNextBeat is not null)
                {
                    await beforeNextBeat().ConfigureAwait(true);
                }

                yield return beat;
            }
        }
    }

    /// <summary>
    /// Simulates a herdr server restart: every running or paused session becomes lost, keeping its native reference.
    /// Lost, terminated and unknown sessions are unchanged.
    /// </summary>
    public void SimulateServerRestart()
    {
        lock (_gate)
        {
            RestartLiveSessions();
            _transcript.Add(new HerdrTranscriptEntry("ServerRestart", null, "ok"));
        }
    }

    /// <summary>Queues one beat request for each session the server holds alive. Beats are produced as a stream reads them.</summary>
    public void EmitHeartbeats()
    {
        lock (_gate)
        {
            foreach (var session in _sessions.Values.Where(candidate => IsAlive(candidate.State)))
            {
                _beatRequests.Writer.TryWrite(session.Id);
            }
        }
    }

    /// <summary>Completes the beat stream, so a reader ends once the queued beat requests are read.</summary>
    public void CompleteHeartbeats() => _beatRequests.Writer.TryComplete();

    /// <summary>
    /// Runs <paramref name="action"/> once, after the next beat has been read and before it is delivered. A test uses it to change
    /// the server between the read of a beat and its delivery.
    /// </summary>
    /// <param name="action">The action to run before the next beat.</param>
    public void RunBeforeNextBeat(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (_gate)
        {
            _beforeNextBeat = action;
        }
    }

    /// <summary>Refuses the next terminate, so a test can observe a failed compensation.</summary>
    public void FailNextTerminate()
    {
        lock (_gate)
        {
            _failNextTerminate = true;
        }
    }

    /// <summary>Restarts the server once, during the next attach: that attach then fails because the session is lost.</summary>
    public void RestartBeforeNextAttach()
    {
        lock (_gate)
        {
            _restartBeforeNextAttach = true;
        }
    }

    private static string Describe(HerdrSessionSnapshot snapshot) => $"{snapshot.State}";

    private static bool IsAlive(HerdrSessionState state) => state is HerdrSessionState.Running or HerdrSessionState.Paused;

    private HerdrHeartbeat? ReadBeat(HerdrSessionId sessionId)
    {
        lock (_gate)
        {
            return _sessions.TryGetValue(sessionId.Value, out var session) && IsAlive(session.State)
                ? new HerdrHeartbeat(session.Id, session.State, session.Sequence)
                : null;
        }
    }

    private Func<Task>? TakeBeforeNextBeat()
    {
        lock (_gate)
        {
            var action = _beforeNextBeat;
            _beforeNextBeat = null;
            return action;
        }
    }

    private T Transact<T>(string operation, string? sessionId, Func<T> action, Func<T, string> describe)
    {
        lock (_gate)
        {
            try
            {
                var result = action();
                _transcript.Add(new HerdrTranscriptEntry(operation, sessionId, describe(result)));
                return result;
            }
            catch (HerdrSessionException failure)
            {
                _transcript.Add(new HerdrTranscriptEntry(operation, sessionId, failure.GetType().Name));
                throw;
            }
        }
    }

    private HerdrSessionSnapshot Create(HerdrCreateRequest request)
    {
        if (_sessions.TryGetValue(request.Id.Value, out var existing))
        {
            var resumes = existing.State == HerdrSessionState.Lost
                && request.NativeReference is { } requested
                && requested.Equals(existing.NativeReference);
            if (!resumes)
            {
                throw new HerdrSessionConflictException($"The herdr session {request.Id} already exists and cannot be started again.");
            }

            existing.State = HerdrSessionState.Running;
            existing.IsAttached = true;
            existing.Advance();
            return existing.Snapshot();
        }

        var native = request.NativeReference
            ?? new NativeSessionReference("native-" + (++_nativeCount).ToString(CultureInfo.InvariantCulture));
        var created = new FakeSession(request.Id, native);
        _sessions.Add(request.Id.Value, created);
        return created.Snapshot();
    }

    private HerdrSessionSnapshot Attach(HerdrSessionId sessionId, long afterSequence)
    {
        if (_restartBeforeNextAttach)
        {
            _restartBeforeNextAttach = false;
            RestartLiveSessions();
        }

        var session = Find(sessionId);
        RequireAlive(session, "attach");
        if (afterSequence < 0 || afterSequence > session.Sequence)
        {
            throw new ArgumentOutOfRangeException(nameof(afterSequence), "The sequence is outside the session's recorded range.");
        }

        if (!session.IsAttached)
        {
            session.IsAttached = true;
            session.Advance();
        }

        return session.Snapshot();
    }

    private HerdrSessionSnapshot Detach(HerdrSessionId sessionId)
    {
        var session = Find(sessionId);
        RequireAlive(session, "detach");
        if (session.IsAttached)
        {
            session.IsAttached = false;
            session.Advance();
        }

        return session.Snapshot();
    }

    private HerdrSessionSnapshot Move(HerdrSessionId sessionId, HerdrSessionState from, HerdrSessionState to)
    {
        var session = Find(sessionId);
        if (session.State != from)
        {
            throw new HerdrInvalidTransitionException($"The herdr session {sessionId} is {session.State}, not {from}, so it cannot become {to}.");
        }

        session.State = to;
        session.Advance();
        return session.Snapshot();
    }

    private HerdrSessionSnapshot Terminate(HerdrSessionId sessionId)
    {
        if (_failNextTerminate)
        {
            _failNextTerminate = false;
            throw new HerdrInvalidTransitionException($"The test server refused to end the herdr session {sessionId}.");
        }

        var session = Find(sessionId);
        if (session.State == HerdrSessionState.Terminated)
        {
            throw new HerdrInvalidTransitionException($"The herdr session {sessionId} is already terminated.");
        }

        session.State = HerdrSessionState.Terminated;
        session.IsAttached = false;
        session.Advance();
        return session.Snapshot();
    }

    private void RestartLiveSessions()
    {
        foreach (var session in _sessions.Values.Where(candidate => IsAlive(candidate.State)))
        {
            session.State = HerdrSessionState.Lost;
            session.IsAttached = false;
            session.Advance();
        }
    }

    private IReadOnlyList<HerdrSessionSnapshot> ListSnapshots() =>
        [.. _sessions.Values.OrderBy(session => session.Id.Value, StringComparer.Ordinal).Select(session => session.Snapshot())];

    private FakeSession Find(HerdrSessionId sessionId) =>
        _sessions.TryGetValue(sessionId.Value, out var session)
            ? session
            : throw new HerdrSessionNotFoundException($"No herdr session {sessionId} exists.");

    private static void RequireAlive(FakeSession session, string operation)
    {
        if (!IsAlive(session.State))
        {
            throw new HerdrInvalidTransitionException($"The herdr session {session.Id} is {session.State} and cannot {operation}.");
        }
    }

    private sealed class FakeSession(HerdrSessionId id, NativeSessionReference native)
    {
        public HerdrSessionId Id { get; } = id;

        public HerdrSessionState State { get; set; } = HerdrSessionState.Running;

        public bool IsAttached { get; set; } = true;

        public NativeSessionReference? NativeReference { get; } = native;

        public long Sequence { get; private set; } = 1;

        public HerdrSessionSnapshot Snapshot() => new(Id, State, IsAttached, NativeReference, Sequence);

        public void Advance() => Sequence++;
    }
}
