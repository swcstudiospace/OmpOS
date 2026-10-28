using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>One select-style option of a session that the engine owns: its model, its mode or its thinking level.</summary>
public sealed record ConfigOption
{
    /// <summary>Gets the engine's identifier of the option, for example <c>model</c>, <c>mode</c> or <c>thinking</c>. It names the option in <c>PUT /sessions/{id}/config-options/{optionId}</c>.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the display name, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; init; }

    /// <summary>Gets the category the engine gave the option (<c>model</c>, <c>mode</c> or <c>thought_level</c>), or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Category { get; init; }

    /// <summary>Gets the value the option has now.</summary>
    public required string CurrentValue { get; init; }

    /// <summary>Gets the values the option can take, in the engine's order.</summary>
    public required IReadOnlyList<ConfigOptionValue> Values { get; init; }
}
