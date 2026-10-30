using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The answer of <c>GET /providers</c>: the catalogue rows that match the filters, the caller's custom endpoints and what the service allows.</summary>
public sealed record ProvidersResponse
{
    /// <summary>Gets the catalogue providers that match the filters, in catalogue order.</summary>
    public required IReadOnlyList<ProviderSummary> Providers { get; init; }

    /// <summary>Gets how many providers the catalogue holds before the filters.</summary>
    public required int Total { get; init; }

    /// <summary>Gets the custom endpoints the caller added.</summary>
    public required IReadOnlyList<CustomProviderInfo> Custom { get; init; }

    /// <summary>Gets a value indicating whether omp is installed on the service.</summary>
    public required bool OmpInstalled { get; init; }

    /// <summary>Gets a value indicating whether the operator allows keys to be written through the API.</summary>
    public required bool CredentialWritesEnabled { get; init; }

    /// <summary>Gets a value indicating whether the operator allows custom endpoints and the default model to be changed through the API.</summary>
    public required bool ConfigWritesEnabled { get; init; }

    /// <summary>Gets a value indicating whether the operator allows live tests.</summary>
    public required bool LiveTestsEnabled { get; init; }

    /// <summary>Gets the default model as <c>provider/model</c>, or null when omp reports none or cannot be read.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DefaultModel { get; init; }
}
