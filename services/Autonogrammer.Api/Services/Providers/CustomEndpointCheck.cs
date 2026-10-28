namespace Autonogrammer.Api.Services.Providers;

/// <summary>The result of checking the base URL of a custom endpoint.</summary>
/// <param name="Url">The canonical URL to write to omp, or null when the address is refused.</param>
/// <param name="Problem">The sentence that says why the address is refused, or null.</param>
internal sealed record CustomEndpointCheck(Uri? Url, string? Problem);
