namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Device to hub heartbeat.</summary>
public sealed record DispatchHbFrame
{
    /// <summary>Protocol version. Always 1.</summary>
    public required int V { get; init; }

    /// <summary>Frame type <c>hb</c>.</summary>
    public required string T { get; init; }

    /// <summary>Last sequence the device applied.</summary>
    public required long LastAppliedSeq { get; init; }

    /// <summary>Commands currently running.</summary>
    public required int RunningCount { get; init; }

    /// <summary>Status <c>idle</c>, <c>busy</c>, <c>approval_pending</c> or <c>paused</c>.</summary>
    public required string Status { get; init; }
}
