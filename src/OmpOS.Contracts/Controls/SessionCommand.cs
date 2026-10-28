using System.Text.Json.Serialization;

namespace OmpOS.Contracts.Controls;

/// <summary>One slash command the engine advertises for a session. The engine runs it when a prompt that starts with its name is sent.</summary>
public sealed record SessionCommand
{
    /// <summary>Gets the command name without the leading slash.</summary>
    public required string Name { get; init; }

    /// <summary>Gets what the command does, as the engine words it, or null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; init; }

    /// <summary>Gets the hint for the argument text that follows the command, or null when it takes no input.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InputHint { get; init; }
}
