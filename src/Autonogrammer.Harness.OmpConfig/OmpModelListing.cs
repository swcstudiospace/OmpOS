namespace Autonogrammer.Harness.OmpConfig;

/// <summary>
/// The outcome of listing omp's models. The list is gated by credentials: omp shows only the models of providers it has a
/// credential for (a stored key, an environment variable or a <c>models.yml</c> key), so an empty list means "no credentials",
/// not "no models exist".
/// </summary>
/// <param name="Models">The models omp listed, in omp's order. Empty when listing failed.</param>
/// <param name="Warning">The first line of what omp wrote to its standard error, or the reason listing failed; null when omp was quiet.</param>
/// <param name="IsComplete">
/// True when omp answered with exit code 0, the answer parsed, and omp did not report that <c>models.yml</c> failed validation.
/// False when custom providers may be missing from <paramref name="Models"/> or the list could not be read.
/// </param>
public sealed record OmpModelListing(IReadOnlyList<OmpModelInfo> Models, string? Warning, bool IsComplete);
