using Autonogrammer.Cowork.Artifacts.Model;
using ArtifactGrantBinding = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.Grant;
using ArtifactMetadataDocument = Autonogrammer.Cowork.Artifacts.Stores.ArtifactWireContract.SyncMetadata;
using ArtifactRemoteResult = Autonogrammer.Cowork.Artifacts.Stores.HttpArtifactRemote.RemoteResult;
using ArtifactRemoteStatus = Autonogrammer.Cowork.Artifacts.Stores.HttpArtifactRemote.RemoteStatus;
using ArtifactSyncOptions = Autonogrammer.Cowork.Artifacts.Stores.ArtifactSyncService.Options;
using ArtifactSyncOutcome = Autonogrammer.Cowork.Artifacts.Stores.ArtifactSyncService.Outcome;

namespace Autonogrammer.Cowork.Artifacts.Stores;

/// <summary>
/// Pushes desktop artifacts to the account store. Sync is off until the user enables it, and a signed-out account sends nothing.
/// </summary>
public sealed class ArtifactSyncService
{
    /// <summary>The one sync setting. It defaults off.</summary>
    internal sealed class Options
    {
        /// <summary>Gets or sets a value indicating whether version bodies may leave the device. The default is false.</summary>
        public bool Enabled { get; set; }
    }

    /// <summary>The outcome of one sync attempt.</summary>
    /// <param name="Status">The remote or queue status.</param>
    /// <param name="ForkedArtifactId">The new artifact created on divergence, if any.</param>
    internal sealed record Outcome(HttpArtifactRemote.RemoteStatus Status, string? ForkedArtifactId);

    /// <summary>The status text the web head shows while sync is off.</summary>
    public const string SyncOffText = "sync is off";

    private readonly IArtifactStore _store;
    private readonly HttpArtifactRemote _remote;
    private readonly ArtifactSyncOptions _options;
    private readonly Lock _gate = new();
    private readonly Queue<Pending> _queue = new();
    private readonly List<ArtifactGrantBinding> _grants = [];
    private bool _signedOut;

    /// <summary>Initializes a new instance of the <see cref="ArtifactSyncService"/> class.</summary>
    /// <param name="store">The authoritative desktop store.</param>
    /// <param name="remote">The only network path.</param>

    public ArtifactSyncService(IArtifactStore store, HttpArtifactRemote remote)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(remote);
        _store = store;
        _remote = remote;
        _options = new ArtifactSyncOptions();
    }

    /// <summary>Gets or sets a value indicating whether version bodies may leave the device.</summary>
    public bool Enabled { get => _options.Enabled; set => _options.Enabled = value; }

    /// <summary>Gets the status text. While sync is off this is <see cref="SyncOffText"/>.</summary>
    public string StatusText => _options.Enabled ? "sync is on" : SyncOffText;

    /// <summary>Gets a value indicating whether the last remote answer was signed out.</summary>
    public bool IsSignedOut => _signedOut;

    /// <summary>Gets how many pushes are waiting, in order.</summary>
    public int QueuedCount
    {
        get
        {
            lock (_gate)
            {
                return _queue.Count;
            }
        }
    }

    /// <summary>Remembers a grant for the next payload. The payload has no account id and no title.</summary>
    /// <param name="grant">The grant binding.</param>
    internal void RememberGrant(ArtifactGrantBinding grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        lock (_gate)
        {
            _grants.Add(grant);
        }
    }

    /// <summary>
    /// Pushes one artifact. While sync is off only metadata leaves. A failed push is queued and later retries keep hash idempotence.
    /// </summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome. Divergence forks the desktop copy to a new artifact and does not merge.</returns>
    internal async Task<ArtifactSyncOutcome> PushAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        if (_signedOut)
        {
            return new ArtifactSyncOutcome(ArtifactRemoteStatus.SignedOut, null);
        }

        var artifact = await _store.GetArtifactAsync(artifactId, cancellationToken).ConfigureAwait(false);
        var versions = await _store.GetVersionsAsync(artifactId, cancellationToken).ConfigureAwait(false);
        if (artifact is null || versions.Count == 0)
        {
            return new ArtifactSyncOutcome(ArtifactRemoteStatus.SignedOut, null);
        }

        var pending = new Pending(artifact, versions[^1], versions.Count, versions[0].CreatedAt, versions.Select(version => version.ContentSha256).ToArray());
        if (QueuedCount > 0)
        {
            Enqueue(pending);
            return new ArtifactSyncOutcome(ArtifactRemoteStatus.Offline, null);
        }

        return await SendOrQueueAsync(pending, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Retries queued pushes in order. A failure leaves the remainder queued.</summary>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The outcome of the last attempt, or signed out when the queue is empty and the account is signed out.</returns>
    internal async Task<ArtifactSyncOutcome> RetryAsync(CancellationToken cancellationToken)
    {
        ArtifactSyncOutcome last = new(ArtifactRemoteStatus.Accepted, null);
        while (TryPeek(out var pending))
        {
            last = await SendOrQueueAsync(pending, cancellationToken, alreadyQueued: true).ConfigureAwait(false);
            if (last.Status is ArtifactRemoteStatus.Offline or ArtifactRemoteStatus.RateLimited or ArtifactRemoteStatus.SignedOut)
            {
                return last;
            }
        }

        return last;
    }

    private async Task<ArtifactSyncOutcome> SendOrQueueAsync(Pending pending, CancellationToken cancellationToken, bool alreadyQueued = false)
    {
        ArtifactRemoteResult result;
        if (!_options.Enabled)
        {
            result = await _remote.PushMetadataAsync(Metadata(pending), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var version = pending.Version;
            result = await _remote.PushVersionAsync(version.ArtifactId, version.Number, version.Content, version.ContentSha256, pending.Artifact.Kind.ToString(), cancellationToken).ConfigureAwait(false);
        }

        if (result.Status == ArtifactRemoteStatus.SignedOut)
        {
            _signedOut = true;
            return new ArtifactSyncOutcome(ArtifactRemoteStatus.SignedOut, null);
        }

        if (result.Status is ArtifactRemoteStatus.Offline or ArtifactRemoteStatus.RateLimited)
        {
            if (result.RetryAfter is TimeSpan wait && wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
            }

            if (!alreadyQueued)
            {
                Enqueue(pending);
            }

            return new ArtifactSyncOutcome(result.Status, null);
        }

        if (alreadyQueued)
        {
            Dequeue();
        }

        if (result.Status != ArtifactRemoteStatus.Divergence)
        {
            return new ArtifactSyncOutcome(result.Status, null);
        }

        var forked = await ForkAsync(pending, cancellationToken).ConfigureAwait(false);
        return new ArtifactSyncOutcome(ArtifactRemoteStatus.Divergence, forked);
    }

    private async Task<string> ForkAsync(Pending pending, CancellationToken cancellationToken)
    {
        var source = pending.Version;
        var id = Guid.CreateVersion7().ToString("D");
        var copy = new ArtifactRecord(
            id,
            pending.Artifact.Kind,
            pending.Artifact.Title,
            pending.Artifact.MediaType,
            pending.Artifact.Language,
            pending.Artifact.SessionId,
            new ArtifactDerivation(source.ArtifactId, source.Number, source.ContentSha256));
        var version = new ArtifactVersionRecord(id, 1, source.Content.ToArray(), source.ContentSha256, null, null, ArtifactAuthor.Agent, source.CreatedAt, null);
        _ = await _store.MutateAsync(
            mutation =>
            {
                mutation.Insert(copy, version);
                return id;
            },
            cancellationToken).ConfigureAwait(false);
        return id;
    }

    private ArtifactMetadataDocument Metadata(Pending pending)
    {
        ArtifactGrantBinding[] grants;
        lock (_gate)
        {
            grants = _grants.Where(grant => string.Equals(grant.ArtifactId, pending.Artifact.Id, StringComparison.Ordinal)).ToArray();
        }

        return new ArtifactMetadataDocument
        {
            Id = pending.Artifact.Id,
            Title = pending.Artifact.Title,
            Kind = pending.Artifact.Kind.ToString(),
            VersionCount = pending.VersionCount,
            CreatedAt = pending.CreatedAt,
            UpdatedAt = pending.Version.CreatedAt,
            Hashes = pending.Hashes,
            Grants = grants,
        };
    }

    private void Enqueue(Pending pending)
    {
        lock (_gate)
        {
            _queue.Enqueue(pending);
        }
    }

    private bool TryPeek(out Pending pending)
    {
        lock (_gate)
        {
            return _queue.TryPeek(out pending!);
        }
    }

    private void Dequeue()
    {
        lock (_gate)
        {
            if (_queue.Count > 0)
            {
                _ = _queue.Dequeue();
            }
        }
    }

    private sealed record Pending(ArtifactRecord Artifact, ArtifactVersionRecord Version, int VersionCount, DateTimeOffset CreatedAt, string[] Hashes);
}
