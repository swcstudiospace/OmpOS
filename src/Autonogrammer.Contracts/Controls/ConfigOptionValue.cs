using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Controls;

/// <summary>One value a <see cref="ConfigOption"/> can take.</summary>
public sealed record ConfigOptionValue
{
    /// <summary>Gets the engine's identifier of the value. It is sent back verbatim to select it.</summary>
    public required string Value { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the description, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }
}
