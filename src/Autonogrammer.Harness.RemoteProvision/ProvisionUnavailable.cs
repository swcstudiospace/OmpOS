namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// The remote machine could not be reached: ssh failed, did not answer in time, or could not start. Nothing was read and nothing was written.
/// The last snapshot the caller passed in is returned with its own timestamp, so the caller can show what was known and when it was read.
/// </summary>
/// <param name="LastKnown">The last state read from the machine, or null when there is none. Its <see cref="RemoteState.ObservedAt"/> is the timestamp of that snapshot.</param>
/// <param name="Reason">Why the machine could not be reached, in one plain sentence.</param>
public sealed record ProvisionUnavailable(RemoteState? LastKnown, string Reason) : RemoteProvisionResult;
