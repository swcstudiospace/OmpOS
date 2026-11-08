namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>What differs on a remote machine, and the installs that would fix the items that can be fixed.</summary>
/// <param name="Drift">Every item that differs, actionable or blocked.</param>
/// <param name="Actions">The installs, in the order they run: Bun, then Omp, then the plugins.</param>
internal sealed record ProvisionPlan(IReadOnlyList<ProvisionDriftItem> Drift, IReadOnlyList<ProvisionAction> Actions);
