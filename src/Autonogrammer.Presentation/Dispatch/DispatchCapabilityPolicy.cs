namespace Autonogrammer.Presentation.Dispatch;

/// <summary>One capability and whether this desktop allows it.</summary>
/// <param name="Capability">The capability name.</param>
/// <param name="Enabled">Whether it is allowed.</param>
public sealed record DispatchCapabilityPolicy(string Capability, bool Enabled);
