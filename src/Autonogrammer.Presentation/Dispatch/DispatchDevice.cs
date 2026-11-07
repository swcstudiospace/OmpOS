namespace Autonogrammer.Presentation.Dispatch;

/// <summary>A device on the account as the presentation shows it. It holds no token, key or address.</summary>
/// <param name="Id">The device.</param>
/// <param name="Name">The name the owner gave the device.</param>
/// <param name="Online">Whether the device is connected.</param>
/// <param name="PresenceKnown">Whether the connection is known; false while the presence service is down.</param>
public sealed record DispatchDevice(string Id, string Name, bool Online, bool PresenceKnown);
