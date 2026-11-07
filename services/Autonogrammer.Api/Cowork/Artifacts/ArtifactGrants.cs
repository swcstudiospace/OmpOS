using System.Collections.Concurrent;
using Autonogrammer.Cowork.Artifacts.Stores;

namespace Autonogrammer.Api.Cowork.Artifacts;

/// <summary>Version-bound grants. A new version is not covered by an older grant.</summary>
internal sealed class ArtifactGrants
{
    private readonly ConcurrentDictionary<string, List<ArtifactGrantDocument>> _grants = new(StringComparer.Ordinal);

    /// <summary>Lists grants for one owner's artifact.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact.</param>
    /// <returns>The grants.</returns>
    public IReadOnlyList<ArtifactGrantDocument> List(string accountId, string artifactId)
    {
        return _grants.TryGetValue(Key(accountId, artifactId), out var grants) ? grants.ToArray() : [];
    }

    /// <summary>Binds a grant to a version hash.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="grant">The grant.</param>
    public void Save(string accountId, ArtifactGrantDocument grant)
    {
        ArgumentNullException.ThrowIfNull(grant);
        var list = _grants.GetOrAdd(Key(accountId, grant.ArtifactId), static _ => []);
        lock (list)
        {
            list.RemoveAll(existing => existing.Number == grant.Number && string.Equals(existing.Scope, grant.Scope, StringComparison.Ordinal) && string.Equals(existing.Device, grant.Device, StringComparison.Ordinal));
            list.Add(grant);
        }
    }

    /// <summary>Removes grants matching the document. An empty body revokes every grant of that version.</summary>
    /// <param name="accountId">The owner.</param>
    /// <param name="artifactId">The artifact.</param>
    /// <param name="number">The version.</param>
    /// <param name="scope">An optional scope.</param>
    /// <param name="device">An optional device.</param>
    /// <returns>The origins that were revoked.</returns>
    public IReadOnlyList<string> Revoke(string accountId, string artifactId, int number, string? scope, string? device)
    {
        if (!_grants.TryGetValue(Key(accountId, artifactId), out var list))
        {
            return [];
        }

        lock (list)
        {
            var removed = list.Where(grant => grant.Number == number && (scope is null || string.Equals(grant.Scope, scope, StringComparison.Ordinal)) && (device is null || string.Equals(grant.Device, device, StringComparison.Ordinal))).ToArray();
            foreach (var grant in removed)
            {
                _ = list.Remove(grant);
            }

            return removed.SelectMany(static grant => grant.Origins).Distinct(StringComparer.Ordinal).ToArray();
        }
    }

    /// <summary>Copies a wire grant into the API document.</summary>
    /// <param name="grant">The wire grant.</param>
    /// <returns>The document.</returns>
    public static ArtifactGrantDocument FromWire(ArtifactWireContract.Grant grant) =>
        new(grant.ArtifactId, grant.Number, grant.ContentSha256, grant.Origins, grant.Scope, grant.Device, grant.GrantedAt);

    private static string Key(string accountId, string artifactId) => accountId + "\n" + artifactId;
}
