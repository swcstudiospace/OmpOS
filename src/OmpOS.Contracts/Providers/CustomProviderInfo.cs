using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Providers;

/// <summary>A custom endpoint the caller added. It names the environment variable of its key and never holds one.</summary>
public sealed record CustomProviderInfo
{
    /// <summary>Gets the provider id as omp uses it in a model selector.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the name shown for the endpoint.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Gets the wire protocol.</summary>
    public required CustomProviderProtocol Protocol { get; init; }

    /// <summary>Gets the base URL as the service wrote it to omp's configuration.</summary>
    [JsonPropertyName("baseUrl")]
    public required string BaseAddress { get; init; }

    /// <summary>Gets the model ids.</summary>
    public required IReadOnlyList<string> ModelIds { get; init; }

    /// <summary>Gets the name of the environment variable the operator sets to the key, or null when the endpoint needs no key.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyEnvName { get; init; }

    /// <summary>Gets whether the service sees that variable set.</summary>
    public required ProviderConfiguredState Configured { get; init; }
}
