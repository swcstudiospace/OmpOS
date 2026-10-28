namespace Autonogrammer.Native.Settings.Models;

/// <summary>A local engine omp discovers by itself on a loopback address. omp lists its models only while the engine answers.</summary>
/// <param name="Id">The provider id omp uses.</param>
/// <param name="DisplayName">The name shown on the card.</param>
/// <param name="Endpoint">The address omp uses when no environment variable moves it (omp's documented default).</param>
internal sealed record OmpLocalEngine(string Id, string DisplayName, Uri Endpoint);
