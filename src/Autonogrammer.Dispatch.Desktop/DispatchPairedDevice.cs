namespace Autonogrammer.Dispatch.Desktop;

/// <summary>The pairing this desktop holds. The account is fixed at pairing; frames for another user are refused.</summary>
/// <param name="DeviceId">The device identifier.</param>
/// <param name="AccountId">The account the device was paired to.</param>
/// <param name="Keys">The hub signing keys delivered at pairing.</param>
public sealed record DispatchPairedDevice(string DeviceId, string AccountId, IReadOnlyList<DispatchPublicKey> Keys);
