namespace Autonogrammer.Dispatch.Desktop;

/// <summary>
/// The local approval granted to one dispatched Computer Use task. It lasts 30 minutes and is withdrawn when the device is revoked.
/// It carries no typing auto-approval: every typed action still goes through the per-action gate.
/// </summary>
/// <param name="ExpiresAt">When the grant lapses.</param>
public sealed record DispatchGrant(DateTimeOffset ExpiresAt);
