namespace Autonogrammer.Harness.RemoteProvision;

/// <summary>
/// One item of the remote machine that differs from the desired state, with the reason. An item with <see cref="BlockedBecause"/> is reported
/// but never installed: the remote side cannot provide it in this build, or a tool it needs is missing.
/// </summary>
/// <param name="Component">What differs.</param>
/// <param name="Subject">The path or plugin directory the item is about, as it appears on the remote machine.</param>
/// <param name="Reason">Why it differs, for example the version it has and the version the pin names.</param>
/// <param name="BlockedBecause">Why the item cannot be installed, or null when it can.</param>
public sealed record ProvisionDriftItem(ProvisionComponent Component, string Subject, string Reason, string? BlockedBecause)
{
    /// <summary>Gets whether an install would fix this item.</summary>
    public bool IsActionable => BlockedBecause is null;
}
