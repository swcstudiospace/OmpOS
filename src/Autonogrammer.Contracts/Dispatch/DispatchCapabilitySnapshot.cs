namespace Autonogrammer.Contracts.Dispatch;

/// <summary>One capability row the desktop reports at pairing and in every hello.</summary>
/// <param name="Id">The capability.</param>
/// <param name="Version">The capability version the desktop implements.</param>
/// <param name="Enabled">Whether the desktop currently allows the capability.</param>
public sealed record DispatchCapabilitySnapshot(DispatchCapability Id, int Version, bool Enabled);
