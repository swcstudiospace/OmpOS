namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The machine has drift that an install would fix, and no confirmation for exactly these actions is recorded for the environment. No write was
/// made. The caller shows <see cref="Actions"/> to the user and records a <see cref="ProvisionConfirmation"/> with them before it provisions again.
/// </summary>
/// <param name="State">The state the probe read.</param>
/// <param name="Drift">Every item that differs from the desired state.</param>
/// <param name="Actions">The installs that would run, in order.</param>
public sealed record ProvisionNeedsConfirmation(RemoteState State, IReadOnlyList<ProvisionDriftItem> Drift, IReadOnlyList<string> Actions) : RemoteProvisionResult;
