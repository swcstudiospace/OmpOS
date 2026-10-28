namespace Autonogrammer.Harness.Abstractions;

/// <summary>The result of an attempt to steer the running turn of a session.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Queue">The queue the message was put into or, when it was not, the queue that was asked for.</param>
/// <param name="Reason">A plain sentence that says why the attempt failed, was unsupported or found no running turn, or <see langword="null"/> otherwise.</param>
public sealed record OmpSteerResult(OmpSteerOutcome Outcome, OmpSteerLane Queue, string? Reason);
