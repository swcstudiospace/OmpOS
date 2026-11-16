namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The remote machine was read, but the desired state could not be determined: the registry could not be reached or did not answer with a usable
/// release, or the release requires a Bun this build does not pin. Nothing is written, because an install needs a known target.
/// </summary>
/// <param name="State">The state the probe read.</param>
/// <param name="Reason">Why the desired state is unknown, in one plain sentence.</param>
public sealed record ProvisionDesiredStateUnavailable(RemoteState State, string Reason) : RemoteProvisionResult;
