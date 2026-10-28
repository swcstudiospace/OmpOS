namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>One steering action as it is recorded: the mode, the length of the operator text and a short hash of it. The text itself is never part of the record.</summary>
public sealed record SteeringRecord
{
    /// <summary>Gets the mode: <c>steer</c> or <c>follow-up</c>.</summary>
    public required string Mode { get; init; }

    /// <summary>Gets the length of the operator text in characters.</summary>
    public required int Length { get; init; }

    /// <summary>Gets the first 12 hexadecimal characters of the SHA-256 of the UTF-8 operator text.</summary>
    public required string TextSha256Prefix { get; init; }

    /// <summary>Gets the time the operator sent the text.</summary>
    public required DateTimeOffset At { get; init; }
}
