using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>Thread-safe in-memory store. Shipped for hosts that have not attached a durable store.</summary>
public sealed class InMemoryArtifactStore : IArtifactStore, IArtifactMutation
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, ArtifactRecord> _artifacts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<ArtifactVersionRecord>> _versions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<string>> _grants = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public ValueTask<T> MutateAsync<T>(Func<IArtifactMutation, T> mutation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mutation);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(mutation(this));
        }
    }

    /// <inheritdoc />
    public ValueTask<ArtifactRecord?> GetArtifactAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(_artifacts.TryGetValue(artifactId, out var artifact) ? artifact : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<ArtifactVersionRecord>> GetVersionsAsync(string artifactId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_versions.TryGetValue(artifactId, out var versions))
            {
                return ValueTask.FromResult<IReadOnlyList<ArtifactVersionRecord>>([]);
            }

            return ValueTask.FromResult<IReadOnlyList<ArtifactVersionRecord>>(versions.ToArray());
        }
    }

    /// <inheritdoc />
    public ArtifactRecord? Find(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        return _artifacts.TryGetValue(artifactId, out var artifact) ? artifact : null;
    }

    /// <inheritdoc />
    public IReadOnlyList<ArtifactVersionRecord> Versions(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        return _versions.TryGetValue(artifactId, out var versions) ? versions : [];
    }

    /// <inheritdoc />
    public void Insert(ArtifactRecord artifact, ArtifactVersionRecord version)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(version);
        _artifacts.Add(artifact.Id, artifact);
        _versions.Add(artifact.Id, [version]);
        _grants.Add(artifact.Id, []);
    }

    /// <inheritdoc />
    public void Append(ArtifactVersionRecord version)
    {
        ArgumentNullException.ThrowIfNull(version);
        _versions[version.ArtifactId].Add(version);
    }

    /// <inheritdoc />
    public void Delete(string artifactId)
    {
        ArgumentNullException.ThrowIfNull(artifactId);
        _ = _artifacts.Remove(artifactId);
        _ = _versions.Remove(artifactId);
        _ = _grants.Remove(artifactId);
    }

    /// <inheritdoc />
    public ArtifactUsage Usage()
    {
        long bytes = 0;
        foreach (var versions in _versions.Values)
        {
            foreach (var version in versions)
            {
                bytes += version.Content.Length;
            }
        }

        return new ArtifactUsage(_artifacts.Count, bytes);
    }

    /// <summary>Replaces stored bytes without updating the hash. Tests use this to prove the read refuses to render.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="content">The tampered bytes.</param>
    internal void TamperContent(string artifactId, int number, byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        lock (_gate)
        {
            Replace(artifactId, number, version => version with { Content = content });
        }
    }

    /// <summary>Breaks the parent hash link. Tests use this to prove the read refuses to render.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="parentContentSha256">A hash that is not the parent's.</param>
    internal void TamperParentHash(string artifactId, int number, string parentContentSha256)
    {
        ArgumentNullException.ThrowIfNull(parentContentSha256);
        lock (_gate)
        {
            Replace(artifactId, number, version => version with { ParentContentSha256 = parentContentSha256 });
        }
    }

    private void Replace(string artifactId, int number, Func<ArtifactVersionRecord, ArtifactVersionRecord> map)
    {
        var versions = _versions[artifactId];
        var index = versions.FindIndex(version => version.Number == number);
        versions[index] = map(versions[index]);
    }
}
