namespace Autonogrammer.Harness.AgentLoop.Substrate;

/// <summary>The result of recording a steering action.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="Reason">A plain-text reason for <see cref="SteeringRecordOutcome.Skipped"/> and <see cref="SteeringRecordOutcome.Failed"/>, otherwise null. It never contains a token or response text.</param>
public sealed record SteeringRecordResult(SteeringRecordOutcome Outcome, string? Reason);
