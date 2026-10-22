using System.Text.Json.Serialization;

namespace OmpOS.Workspace;

/// <summary>
/// The JSON shape of a mode file. Every member is nullable so that a missing member is reported with a
/// specific reason rather than a generic deserialization error.
/// </summary>
internal sealed class ModeFile
{
    /// <summary>Gets the mode id.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>Gets the display name shown in the mode picker.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    /// <summary>Gets the ids of the rules the mode uses.</summary>
    [JsonPropertyName("rules")]
    public List<string?>? Rules { get; init; }

    /// <summary>Gets the ids of the skills the mode uses.</summary>
    [JsonPropertyName("skills")]
    public List<string?>? Skills { get; init; }
}
