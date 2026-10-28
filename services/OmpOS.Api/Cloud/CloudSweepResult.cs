namespace OmpOS.Api.Cloud;

/// <summary>What one sweep did.</summary>
/// <param name="Idled">How many running agents were put to idle for being inactive.</param>
/// <param name="LimitStopped">How many running agents were stopped for using their compute budget.</param>
/// <param name="TimedOut">How many agents were marked failed for staying in provisioning or starting too long.</param>
/// <param name="DeletionsCompleted">How many agents stuck in deleting were finally removed.</param>
/// <param name="Orphans">The orphan scan's report, or null when this sweep did not run one.</param>
internal sealed record CloudSweepResult(int Idled, int LimitStopped, int TimedOut, int DeletionsCompleted, CloudOrphanReport? Orphans);
