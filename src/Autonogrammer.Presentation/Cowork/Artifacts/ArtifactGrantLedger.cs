namespace Autonogrammer.Presentation.Cowork.Artifacts;

/// <summary>
/// In-memory "Allow for this version" grants. A grant binds to one <see cref="ArtifactVersionKey"/>, so a new version
/// asks again. Nothing is written to disk; sign-out and Access revoke clear it.
/// </summary>
public sealed class ArtifactGrantLedger
{
    private readonly Dictionary<ArtifactVersionKey, IReadOnlyList<string>> _grants = [];

    /// <summary>Returns the origins approved for one version, or an empty list.</summary>
    /// <param name="key">The version.</param>
    /// <returns>The approved origins.</returns>
    public IReadOnlyList<string> ApprovedFor(ArtifactVersionKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return _grants.TryGetValue(key, out var origins) ? origins : [];
    }

    /// <summary>Adds origins to one version. Duplicates are ignored.</summary>
    /// <param name="key">The version.</param>
    /// <param name="origins">The origins to approve.</param>
    public void Add(ArtifactVersionKey key, IReadOnlyList<string> origins)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(origins);
        var merged = ApprovedFor(key).ToList();
        foreach (var origin in origins)
        {
            if (!merged.Contains(origin, StringComparer.Ordinal))
            {
                merged.Add(origin);
            }
        }

        _grants[key] = merged;
    }

    /// <summary>Removes every grant for one version.</summary>
    /// <param name="key">The version.</param>
    public void Revoke(ArtifactVersionKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        _grants.Remove(key);
    }

    /// <summary>Removes every grant. Sign-out calls this.</summary>
    public void Clear() => _grants.Clear();
}
