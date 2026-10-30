using Autonogrammer.Cowork.Artifacts.Model;

namespace Autonogrammer.Cowork.Artifacts;

/// <summary>Pure persistence port. File and Postgres stores arrive later; this library performs no I/O.</summary>
public interface IArtifactStore
{
    /// <summary>
    /// Runs <paramref name="mutation"/> while the store's lock is held so compare-and-swap sees a consistent view.
    /// </summary>
    /// <typeparam name="T">The mutation result.</typeparam>
    /// <param name="mutation">The decision and write.</param>
    /// <param name="cancellationToken">Cancels before the lock is taken.</param>
    /// <returns>The mutation result.</returns>
    ValueTask<T> MutateAsync<T>(Func<IArtifactMutation, T> mutation, CancellationToken cancellationToken);

    /// <summary>Copies the artifact header, or null when it is absent.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The header, or null.</returns>
    ValueTask<ArtifactRecord?> GetArtifactAsync(string artifactId, CancellationToken cancellationToken);

    /// <summary>Copies every version, ordered by number.</summary>
    /// <param name="artifactId">The artifact identifier.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The versions.</returns>
    ValueTask<IReadOnlyList<ArtifactVersionRecord>> GetVersionsAsync(string artifactId, CancellationToken cancellationToken);
}
