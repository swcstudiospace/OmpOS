using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>The answer of <c>POST /providers/{id}/test</c>.</summary>
public sealed record ProviderTestResponse
{
    /// <summary>Gets the provider that was tested.</summary>
    public required string ProviderId { get; init; }

    /// <summary>Gets the outcome.</summary>
    public required ProviderTestStatus Status { get; init; }

    /// <summary>Gets exactly one sentence naming the outcome, for every status.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}
