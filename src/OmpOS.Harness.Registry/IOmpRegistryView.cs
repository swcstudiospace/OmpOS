namespace OmpOS.Harness.Registry;

/// <summary>
/// The registry as the omp side sees it. How omp consumes the registry is not yet established (docs/open-questions.md), so
/// the consistency check depends on this interface instead of on one reading mechanism. <see cref="FileOmpRegistryView"/>
/// is the implementation that reads the export file directly.
/// </summary>
public interface IOmpRegistryView
{
    /// <summary>
    /// Reads the registry as omp sees it.
    /// </summary>
    /// <param name="expectedGeneration">The generation the caller last observed.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>
    /// The view. A snapshot is returned only for an accepted export; a missing or refused export carries no snapshot.
    /// </returns>
    Task<RegistryReadResult> ReadAsync(long expectedGeneration, CancellationToken cancellationToken);
}
