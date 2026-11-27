using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>Owner-scoped durable store for version grants. The owner is fixed by store construction: every operation is filtered by it, so one account can never list, replace or delete another account's grants. Session grants are memory-only and must never enter this contract or any persistent storage behind it.</summary>
public interface IArtifactGrantStore
{
    /// <summary>Lists every durable grant for one artifact in this account.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The grants, ordered by version.</returns>
    ValueTask<IReadOnlyList<ArtifactGrantBinding>> ListAsync(string artifactId, CancellationToken cancellationToken);

    /// <summary>Atomically inserts a grant or replaces the existing grant with the same artifact, version, scope and device.</summary>
    /// <param name="grant">The grant binding. Its artifact must belong to this account.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the grant is stored.</returns>
    ValueTask UpsertAsync(ArtifactGrantBinding grant, CancellationToken cancellationToken);

    /// <summary>Atomically deletes the one grant matching the selected artifact, version, scope and device. Other devices' grants are untouched.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="number">The version number.</param>
    /// <param name="scope">The grant scope.</param>
    /// <param name="device">The device the grant names.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes when the grant is deleted. Deleting an absent grant deletes nothing.</returns>
    ValueTask DeleteAsync(string artifactId, int number, string scope, string device, CancellationToken cancellationToken);
}
