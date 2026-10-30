using Autonogrammer.Contracts.Providers;

namespace Autonogrammer.Api.Services.Providers;

/// <summary>A custom endpoint one caller added.</summary>
/// <param name="Owner">The caller that added it.</param>
/// <param name="Id">The provider id.</param>
/// <param name="DisplayName">The name shown.</param>
/// <param name="Protocol">The wire protocol.</param>
/// <param name="BaseUrl">The canonical base URL as written to omp.</param>
/// <param name="ModelIds">The model ids.</param>
/// <param name="KeyEnvName">The key variable, or null for a keyless endpoint.</param>
internal sealed record CustomProviderRecord(
    string Owner,
    string Id,
    string DisplayName,
    CustomProviderProtocol Protocol,
    string BaseUrl,
    IReadOnlyList<string> ModelIds,
    string? KeyEnvName);
