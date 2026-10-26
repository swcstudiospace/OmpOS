namespace OmpOS.Harness.Acp;

/// <summary>The result of a <see cref="AcpAgentSession.SteerAsync"/> attempt.</summary>
/// <param name="Outcome">How the attempt ended.</param>
/// <param name="Queue">The queue omp reported for the message, or the queue that was asked for when omp reported none.</param>
/// <param name="Reason">A plain sentence that says why the attempt failed, was unsupported or found no running turn, or <see langword="null"/> otherwise.</param>
public sealed record AcpSteerResult(AcpSteerOutcome Outcome, AcpSteerLane Queue, string? Reason);
