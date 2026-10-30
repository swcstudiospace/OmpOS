namespace Autonogrammer.Harness.AgentLoop.Steering;

/// <summary>Proof that a directive is waiting in the session's steering inbox. It does not say that omp took it: wait for the acknowledgement for that.</summary>
/// <param name="Id">The identifier of the directive; the acknowledgement carries the same one.</param>
/// <param name="Mode">The delivery mode that was written.</param>
/// <param name="QueuedAt">The time the directive was written to the inbox.</param>
public sealed record SteeringReceipt(string Id, SteeringDirectiveMode Mode, DateTimeOffset QueuedAt);
