namespace OmpOS.Harness.Herdr;

/// <summary>
/// The lifecycle of herdr sessions over <see cref="IHerdrSessionClient"/>. Every change the server confirms is written to the
/// <see cref="HerdrSessionStore"/> before the call returns. A restart reattaches by stored id. A session the server reports as
/// lost is recorded as lost, and it resumes only through <see cref="ResumeFromNativeReferenceAsync"/>. The store alone never
/// makes a session running.
/// </summary>
public sealed class HerdrSessionService : IDisposable
{
    private readonly IHerdrSessionClient _client;
    private readonly HerdrSessionStore _store;
    private readonly HerdrHeartbeatMonitor _monitor;
    private readonly SemaphoreSlim _createGate = new(1, 1);
    private static readonly TimeSpan CompensationTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Creates a service over a server client and a local store.</summary>
    /// <param name="client">The herdr server client.</param>
    /// <param name="store">The local store that persists the session records.</param>
    /// <param name="timeProvider">The clock that measures heartbeat silence. The system clock when null.</param>
    public HerdrSessionService(IHerdrSessionClient client, HerdrSessionStore store, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(store);
        _client = client;
        _store = store;
        _monitor = new HerdrHeartbeatMonitor(timeProvider);
    }

    /// <summary>Releases the lock that serializes creates. The client and the store belong to the caller.</summary>
    public void Dispose() => _createGate.Dispose();

    /// <summary>
    /// Starts a new session and records it. The identifier must not already be in the local store. Creates are serialized per
    /// service, so two creates of one identifier cannot both reach the server. When the server accepts the create but the
    /// record cannot be stored, the server session is terminated and the persist failure is rethrown.
    /// </summary>
    /// <param name="id">The identifier for the new session.</param>
    /// <param name="providerId">The provider selection, as a plain provider identifier.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view of the started session.</returns>
    /// <exception cref="HerdrSessionConflictException">The local store already records this identifier.</exception>
    public async Task<HerdrSessionView> CreateAsync(HerdrSessionId id, string providerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerId);
        await _createGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_store.Find(id) is not null)
            {
                throw new HerdrSessionConflictException($"The herdr session {id} is already recorded in the local store.");
            }

            var snapshot = await _client.CreateAsync(new HerdrCreateRequest(id, providerId, null), cancellationToken).ConfigureAwait(false);
            return await RecordStartedAsync(snapshot, async () =>
            {
                var stored = new HerdrStoredSession(id, providerId, snapshot.NativeReference, snapshot.State, snapshot.LastSequence);
                await _store.UpsertAsync(stored, cancellationToken).ConfigureAwait(false);
                return new HerdrSessionView(snapshot, providerId, _monitor.IsStale(id));
            }).ConfigureAwait(false);
        }
        finally
        {
            _createGate.Release();
        }
    }

    /// <summary>Reads a recorded session from the server and updates the local record.</summary>
    /// <param name="id">The session to read.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view of the session.</returns>
    /// <exception cref="HerdrSessionNotFoundException">The local store has no record of this session.</exception>
    public Task<HerdrSessionView> StatusAsync(HerdrSessionId id, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.StatusAsync(id, cancellationToken), cancellationToken);

    /// <summary>Attaches a client to a live recorded session.</summary>
    /// <param name="id">The session to attach to.</param>
    /// <param name="afterSequence">The last sequence number the caller has seen.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view after the attach.</returns>
    public Task<HerdrSessionView> AttachAsync(HerdrSessionId id, long afterSequence, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.AttachAsync(id, afterSequence, cancellationToken), cancellationToken);

    /// <summary>Detaches the client from a recorded session. The agent keeps running.</summary>
    /// <param name="id">The session to detach from.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view after the detach.</returns>
    public Task<HerdrSessionView> DetachAsync(HerdrSessionId id, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.DetachAsync(id, cancellationToken), cancellationToken);

    /// <summary>Pauses a running recorded session.</summary>
    /// <param name="id">The session to pause.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view after the pause.</returns>
    public Task<HerdrSessionView> PauseAsync(HerdrSessionId id, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.PauseAsync(id, cancellationToken), cancellationToken);

    /// <summary>Resumes a paused recorded session.</summary>
    /// <param name="id">The session to resume.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view after the resume.</returns>
    public Task<HerdrSessionView> ResumeAsync(HerdrSessionId id, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.ResumeAsync(id, cancellationToken), cancellationToken);

    /// <summary>Ends a recorded session.</summary>
    /// <param name="id">The session to end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view after the termination.</returns>
    public Task<HerdrSessionView> TerminateAsync(HerdrSessionId id, CancellationToken cancellationToken) =>
        ApplyAsync(id, client => client.TerminateAsync(id, cancellationToken), cancellationToken);

    /// <summary>
    /// Returns true when a recorded session has been silent for <see cref="HerdrHeartbeatMonitor.StaleAfter"/> or longer. It reads
    /// only the clock and the beats already seen, so a silent server still reports staleness.
    /// </summary>
    /// <param name="id">The session to check.</param>
    public bool IsStale(HerdrSessionId id) => _monitor.IsStale(id);

    /// <summary>
    /// Resumes a lost session from its stored native reference. The session becomes running only when the server confirms it.
    /// </summary>
    /// <param name="id">The lost session to resume.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view of the resumed session.</returns>
    /// <exception cref="HerdrInvalidTransitionException">
    /// The recorded state is not lost, or the lost session has no native reference to resume from.
    /// </exception>
    public async Task<HerdrSessionView> ResumeFromNativeReferenceAsync(HerdrSessionId id, CancellationToken cancellationToken)
    {
        await _createGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var stored = RequireStored(id);
            if (stored.LastKnownState != HerdrSessionState.Lost)
            {
                throw new HerdrInvalidTransitionException(
                    $"The herdr session {id} is {stored.LastKnownState}; only a lost session resumes from its native reference.");
            }

            if (stored.NativeReference is not { } native)
            {
                throw new HerdrInvalidTransitionException($"The lost herdr session {id} has no native reference to resume from.");
            }

            var snapshot = await _client.CreateAsync(new HerdrCreateRequest(id, stored.ProviderId, native), cancellationToken).ConfigureAwait(false);
            return await RecordStartedAsync(snapshot, () => PersistAsync(snapshot, cancellationToken)).ConfigureAwait(false);
        }
        finally
        {
            _createGate.Release();
        }
    }

    /// <summary>
    /// Reconciles every recorded session with the server after a start. A session that is still alive is reattached by its
    /// stored id. A session the server reports as lost, or does not know, is recorded as lost and keeps its native reference.
    /// A terminated session is not contacted.
    /// </summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The view of every recorded session, ordered by identifier.</returns>
    public async Task<IReadOnlyList<HerdrSessionView>> RestoreAsync(CancellationToken cancellationToken)
    {
        var views = new List<HerdrSessionView>();
        foreach (var stored in _store.Sessions)
        {
            views.Add(await RestoreOneAsync(stored, cancellationToken).ConfigureAwait(false));
        }

        return views;
    }

    /// <summary>
    /// Consumes the server's heartbeat stream until it ends or is cancelled. Each beat for a recorded session refreshes its
    /// silence count. A beat that carries a different state or a later sequence updates the recorded record, but only while the
    /// record and the beat are both running or paused. Beats for unrecorded sessions are ignored.
    /// </summary>
    /// <param name="cancellationToken">Ends the stream. Cancellation surfaces as <see cref="OperationCanceledException"/>.</param>
    public async Task RecordHeartbeatsAsync(CancellationToken cancellationToken)
    {
        await foreach (var beat in _client.HeartbeatsAsync(cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (_store.Find(beat.Id) is null)
            {
                continue;
            }

            _monitor.Record(beat.Id);
            await _store.UpdateAsync(beat.Id, current => ApplyBeat(current, beat), cancellationToken).ConfigureAwait(false);
        }
    }

    private static HerdrStoredSession ApplyBeat(HerdrStoredSession current, HerdrHeartbeat beat)
    {
        // A beat is evidence that the server holds the session alive. It never revives or ends a session: lost and terminated
        // records change only through the explicit operations. A beat older than the stored sequence is stale, so it is ignored.
        if (!IsLive(current.LastKnownState) || !IsLive(beat.State) || beat.LastSequence < current.LastSequence)
        {
            return current;
        }

        return current with { LastKnownState = beat.State, LastSequence = beat.LastSequence };
    }

    private static bool IsLive(HerdrSessionState state) => state is HerdrSessionState.Running or HerdrSessionState.Paused;

    private HerdrStoredSession RequireStored(HerdrSessionId id) =>
        _store.Find(id) ?? throw new HerdrSessionNotFoundException($"The local store has no herdr session {id}.");

    private async Task<HerdrSessionView> ApplyAsync(
        HerdrSessionId id,
        Func<IHerdrSessionClient, Task<HerdrSessionSnapshot>> operation,
        CancellationToken cancellationToken)
    {
        RequireStored(id);
        var snapshot = await operation(_client).ConfigureAwait(false);
        return await PersistAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> RecordStartedAsync<T>(HerdrSessionSnapshot snapshot, Func<Task<T>> persist)
    {
        _monitor.Track(snapshot.Id);
        try
        {
            return await persist().ConfigureAwait(false);
        }
        catch
        {
            // The server accepted the start, but its record is not stored, so nothing would reattach to it. End it, then
            // rethrow the persist failure exactly as it was raised.
            await EndUnrecordedSessionAsync(snapshot.Id).ConfigureAwait(false);
            throw;
        }
    }

    private async Task EndUnrecordedSessionAsync(HerdrSessionId id)
    {
        // The cleanup has its own bound, so a stalled server cannot hold the create gate indefinitely.
        using var cleanup = new CancellationTokenSource(CompensationTimeout);
        try
        {
            await _client.TerminateAsync(id, cleanup.Token).ConfigureAwait(false);
        }
        catch (HerdrSessionException)
        {
            // The server refused the termination. The persist failure is still the one reported.
        }
        catch (OperationCanceledException)
        {
            // The cleanup did not finish within CompensationTimeout. The persist failure is still the one reported.
        }
    }

    private async Task<HerdrSessionView> PersistAsync(HerdrSessionSnapshot snapshot, CancellationToken cancellationToken)
    {
        // The record is read again under the store's write lock, so the provider and the native reference written are the
        // stored ones at write time, not copies taken before the server call.
        var stored = await _store.UpdateAsync(snapshot.Id, current => current with
        {
            NativeReference = snapshot.NativeReference ?? current.NativeReference,
            LastKnownState = snapshot.State,
            LastSequence = snapshot.LastSequence,
        }, cancellationToken).ConfigureAwait(false);
        return new HerdrSessionView(snapshot with { NativeReference = stored.NativeReference }, stored.ProviderId, _monitor.IsStale(snapshot.Id));
    }

    private async Task<HerdrSessionView> RestoreOneAsync(HerdrStoredSession stored, CancellationToken cancellationToken)
    {
        if (stored.LastKnownState == HerdrSessionState.Terminated)
        {
            var ended = new HerdrSessionSnapshot(stored.Id, HerdrSessionState.Terminated, false, stored.NativeReference, stored.LastSequence);
            return new HerdrSessionView(ended, stored.ProviderId, _monitor.IsStale(stored.Id));
        }

        _monitor.Track(stored.Id);
        var snapshot = await ReattachAsync(stored, cancellationToken).ConfigureAwait(false);
        return await PersistAsync(snapshot, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HerdrSessionSnapshot> ReattachAsync(HerdrStoredSession stored, CancellationToken cancellationToken)
    {
        var current = await ReadServerSnapshotAsync(stored, cancellationToken).ConfigureAwait(false);
        if (!IsLive(current.State))
        {
            return current;
        }

        try
        {
            return await _client.AttachAsync(stored.Id, Math.Min(stored.LastSequence, current.LastSequence), cancellationToken).ConfigureAwait(false);
        }
        catch (HerdrInvalidTransitionException)
        {
            // The server moved the session to lost between the status read and the attach. Report what it holds now, so the
            // other restored sessions are unaffected.
            return await ReadServerSnapshotAsync(stored, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<HerdrSessionSnapshot> ReadServerSnapshotAsync(HerdrStoredSession stored, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.StatusAsync(stored.Id, cancellationToken).ConfigureAwait(false);
        }
        catch (HerdrSessionNotFoundException)
        {
            // The server no longer knows this session. It is recorded as lost, and its native reference is kept.
            return new HerdrSessionSnapshot(stored.Id, HerdrSessionState.Lost, false, stored.NativeReference, stored.LastSequence);
        }
    }
}
