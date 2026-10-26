using System.Collections.Concurrent;
using System.Globalization;

namespace OmpOS.Sandbox;

/// <summary>
/// Gives each cloud session one container and one volume, puts idle containers to sleep, and restores the transcript from
/// the volume when a sleeping session is reattached.
/// </summary>
/// <remarks>
/// Idle sleep is evaluated by <see cref="SleepIdleAsync"/> against the injected <see cref="TimeProvider"/>; the host starts no timer,
/// so a caller (a scheduler or reaper) decides how often to sweep.
/// Sleep stops every process in the container. The host persists and restores the transcript only: it never claims
/// that processes, shell state or in-memory state survive sleep.
/// </remarks>
public sealed class CloudSessionHost : IDisposable
{
    /// <summary>The text of the marker stored and shown when a session resumes after sleep.</summary>
    public const string ResumeMarkerText =
        "Session resumed after sleep. The transcript was restored from the session volume; running processes, shell state and environment from before sleep were not preserved.";

    private readonly ICloudEnvironmentClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly CloudSessionHostOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<string, CloudSession> _sessions = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="CloudSessionHost"/> class.
    /// </summary>
    /// <param name="client">The cloud environment client.</param>
    /// <param name="timeProvider">The clock used for activity stamps and idle checks.</param>
    /// <param name="options">The host options; the idle window must be positive.</param>
    public CloudSessionHost(ICloudEnvironmentClient client, TimeProvider timeProvider, CloudSessionHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.IdleWindow, TimeSpan.Zero, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.VolumeMountPath, nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(options.TranscriptPath, nameof(options));
        _client = client;
        _timeProvider = timeProvider;
        _options = options;
    }

    /// <summary>
    /// Opens a session: creates its container and volume on first use, otherwise adopts the running ones.
    /// A session never gets a second container or volume.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The running session.</returns>
    /// <exception cref="InvalidOperationException">The session's container is asleep; use <see cref="ReattachAsync"/>.</exception>
    public async Task<CloudSession> OpenAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var container = await _client.FindContainerAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (container is { Phase: CloudContainerPhase.Sleeping })
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' is asleep; reattach it instead of opening it."));
            }

            var created = container is null;
            container ??= await _client.CreateContainerAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var volume = await _client.FindVolumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
            var attach = created || volume is null;
            volume ??= await _client.CreateVolumeAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (attach)
            {
                await _client.AttachVolumeAsync(container.ContainerId, volume.VolumeId, _options.VolumeMountPath, cancellationToken)
                    .ConfigureAwait(false);
            }

            var session = new CloudSession(
                sessionId,
                container.ContainerId,
                volume.VolumeId,
                CloudSessionState.Running,
                container.BootGeneration,
                _timeProvider.GetUtcNow());
            _sessions[sessionId] = session;
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Returns the host's current snapshot of a session.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The snapshot, or <see langword="null"/> when this host has not opened or reattached the session.</returns>
    public CloudSession? Get(string sessionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        return _sessions.TryGetValue(sessionId, out var session) ? session : null;
    }

    /// <summary>
    /// Stores one conversation message on the session's volume and counts it as activity.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="role">The speaker, for example <c>user</c> or <c>assistant</c>.</param>
    /// <param name="text">The message text.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The stored entry.</returns>
    /// <exception cref="InvalidOperationException">The session is unknown to this host or asleep.</exception>
    public async Task<CloudTranscriptEntry> AppendAsync(string sessionId, string role, string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(text);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' is not open on this host."));
            }

            if (session.State != CloudSessionState.Running)
            {
                throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' is asleep; reattach it before writing."));
            }

            var now = _timeProvider.GetUtcNow();
            var entry = new CloudTranscriptEntry(now, CloudTranscriptEntry.MessageKind, role, text);
            await CreateTranscript(session.VolumeId).AppendAsync(entry, cancellationToken).ConfigureAwait(false);
            _sessions[sessionId] = session with { LastActivityAt = now };
            return entry;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Puts to sleep every running session whose last activity is at least the idle window old.
    /// Sleeping stops the container's processes; the volume, and so the transcript, persists.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The identifiers of the sessions put to sleep, in ordinal order.</returns>
    public async Task<IReadOnlyList<string>> SleepIdleAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var now = _timeProvider.GetUtcNow();
            var idle = _sessions.Values
                .Where(session => session.State == CloudSessionState.Running && now - session.LastActivityAt >= _options.IdleWindow)
                .OrderBy(session => session.SessionId, StringComparer.Ordinal)
                .ToList();
            var slept = new List<string>(idle.Count);
            foreach (var session in idle)
            {
                var container = await _client.SleepContainerAsync(session.ContainerId, cancellationToken).ConfigureAwait(false);
                _sessions[session.SessionId] = session with
                {
                    State = CloudSessionState.Sleeping,
                    BootGeneration = container.BootGeneration,
                };
                slept.Add(session.SessionId);
            }

            return slept;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Reattaches to a session, waking its container when it sleeps, and reads the transcript back from the volume.
    /// Works on a host that never opened the session: the client and the volume are the source of truth.
    /// </summary>
    /// <remarks>
    /// When the container had slept, a resume marker is stored on the volume and returned as the last transcript entry.
    /// The result never claims that processes or shell state were restored.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The restored session view.</returns>
    /// <exception cref="InvalidOperationException">The session has no container or no volume.</exception>
    public async Task<CloudSessionRestore> ReattachAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var container = await _client.FindContainerAsync(sessionId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' has no container."));
            var volume = await _client.FindVolumeAsync(sessionId, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    string.Create(CultureInfo.InvariantCulture, $"Session '{sessionId}' has no volume."));

            var wasAsleep = container.Phase == CloudContainerPhase.Sleeping;
            if (wasAsleep)
            {
                container = await _client.WakeContainerAsync(container.ContainerId, cancellationToken).ConfigureAwait(false);
            }

            var transcript = CreateTranscript(volume.VolumeId);
            var now = _timeProvider.GetUtcNow();
            CloudTranscriptEntry? marker = null;
            if (wasAsleep)
            {
                marker = new CloudTranscriptEntry(now, CloudTranscriptEntry.ResumeMarkerKind, "system", ResumeMarkerText);
                await transcript.AppendAsync(marker, cancellationToken).ConfigureAwait(false);
            }

            var entries = await transcript.ReadAsync(cancellationToken).ConfigureAwait(false);
            var session = new CloudSession(
                sessionId,
                container.ContainerId,
                volume.VolumeId,
                CloudSessionState.Running,
                container.BootGeneration,
                now);
            _sessions[sessionId] = session;
            return new CloudSessionRestore(session, entries, marker);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private CloudTranscriptVolume CreateTranscript(string volumeId) => new(_client, volumeId, _options.TranscriptPath);
}
