namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The remote machine was read and compared with the desired state. No write was made. An empty <see cref="Drift"/> means the machine is up to
/// date, and a second provision of an up-to-date machine returns this case with no write.
/// </summary>
/// <param name="State">The state the probe read.</param>
/// <param name="Drift">Every item that differs from the desired state, including the items that cannot be installed (see <see cref="ProvisionDriftItem.BlockedBecause"/>).</param>
/// <param name="Actions">The installs that would run, in order: one line each, exactly as the confirmation records them.</param>
public sealed record ProvisionCheck(RemoteState State, IReadOnlyList<ProvisionDriftItem> Drift, IReadOnlyList<string> Actions) : RemoteProvisionResult;
