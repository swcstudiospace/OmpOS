using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Runs;

/// <summary>
/// The output of a run. A queued or running run reports its status with no transcript and no changes. A settled run (completed, failed or cancelled) returns its transcript, and the file changes the harness reported while it ran. A harness that reports no file edit yields an empty change list, so the output is the transcript only.
/// </summary>
public sealed record RunOutput
{
    /// <summary>Gets the run identifier.</summary>
    public required string RunId { get; init; }

    /// <summary>Gets the run lifecycle state at the time of the read.</summary>
    public required RunStatus Status { get; init; }

    /// <summary>Gets the run's transcript, or null while the run is queued or running.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RunTranscript? Transcript { get; init; }

    /// <summary>Gets the file changes the harness reported, in the order it reported them, or null while the run is queued or running. Empty when the harness reported none.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<RunFileChange>? Changes { get; init; }
}
