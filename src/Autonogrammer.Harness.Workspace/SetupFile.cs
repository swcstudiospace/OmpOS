using System.Text.Json.Serialization;

namespace Autonogrammer.Workspace;

/// <summary>
/// The JSON shape of the setup file. Every member is nullable so that a missing member is reported with a
/// specific reason rather than a generic deserialization error.
/// </summary>
internal sealed class SetupFile
{
    /// <summary>Gets the ordered setup steps.</summary>
    [JsonPropertyName("steps")]
    public List<SetupStepFile?>? Steps { get; init; }

    /// <summary>Gets the environment variables visible to every step and to the agent run.</summary>
    [JsonPropertyName("environment")]
    public Dictionary<string, string?>? Environment { get; init; }
}
