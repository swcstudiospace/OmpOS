using System.Text.Json.Serialization;

namespace OmpOS.Workspace;

/// <summary>
/// The JSON shape of one setup step.
/// </summary>
internal sealed class SetupStepFile
{
    /// <summary>Gets the executable to run.</summary>
    [JsonPropertyName("command")]
    public string? Command { get; init; }

    /// <summary>Gets the argument list, passed without a shell.</summary>
    [JsonPropertyName("arguments")]
    public List<string?>? Arguments { get; init; }

    /// <summary>Gets the working directory, relative to the workspace root.</summary>
    [JsonPropertyName("workingDirectory")]
    public string? WorkingDirectory { get; init; }
}
