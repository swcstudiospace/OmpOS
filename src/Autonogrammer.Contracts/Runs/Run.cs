using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Runs;

/// <summary>
/// One hosted run: a single prompt executed by the hosted harness inside a session. The run carries its status and no output; the output of a settled run is read as a RunOutput.
/// </summary>
public sealed record Run
{
    /// <summary>Gets the run identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets the identifier of the session the run executes in.</summary>
    public required string SessionId { get; init; }

    /// <summary>Gets the run lifecycle state.</summary>
    public required RunStatus Status { get; init; }

    /// <summary>Gets the time the run was accepted.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets the time the run settled, or null while the run is queued or running.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DateTimeOffset? CompletedAt { get; init; }
}
