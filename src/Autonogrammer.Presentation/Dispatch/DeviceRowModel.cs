namespace Autonogrammer.Presentation.Dispatch;

/// <summary>One device row: its name and the text that says whether it can take commands.</summary>
/// <param name="DeviceId">The device.</param>
/// <param name="Name">The device name.</param>
/// <param name="Phase">The phase when the device is offline; null when it is connected or its presence is unknown.</param>
/// <param name="StatusText">The text beside the device.</param>
public sealed record DeviceRowModel(string DeviceId, string Name, DispatchPhase? Phase, string StatusText)
{
    /// <summary>Builds the row for a device. Unknown presence is said as unknown, never as offline.</summary>
    /// <param name="device">The device.</param>
    /// <returns>The row.</returns>
    public static DeviceRowModel From(DispatchDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (!device.PresenceKnown)
        {
            return new DeviceRowModel(device.Id, device.Name, null, DispatchCopy.ForDegradation(DispatchDegradation.PresenceUnknown));
        }

        return device.Online
            ? new DeviceRowModel(device.Id, device.Name, null, DispatchCopy.Connected)
            : new DeviceRowModel(device.Id, device.Name, DispatchPhase.DeviceOffline, DispatchCopy.ForPhase(DispatchPhase.DeviceOffline));
    }
}
