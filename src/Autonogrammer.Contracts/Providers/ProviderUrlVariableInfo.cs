using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Providers;

/// <summary>A named placeholder in the base URL of a route.</summary>
public sealed record ProviderUrlVariableInfo
{
    /// <summary>Gets the placeholder name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the label of the input.</summary>
    public required string Label { get; init; }

    /// <summary>Gets an example value, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    /// <summary>Gets a short sentence that says where the user finds the value, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Help { get; init; }
}
