namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The devices list: loading, the device rows, or the single phase that replaces them (no devices, feature off, signed out, unreachable).
/// A state that has an answer never shows a spinner.
/// </summary>
/// <param name="Phase">The phase that replaces the rows, or null when the rows are shown.</param>
/// <param name="StatusText">The text for the phase; empty when the rows are shown.</param>
/// <param name="Rows">The device rows; empty unless the devices loaded.</param>
public sealed record DevicesViewModel(DispatchPhase? Phase, string StatusText, IReadOnlyList<DeviceRowModel> Rows)
{
    /// <summary>Gets the state while the devices are being read.</summary>
    /// <returns>The model.</returns>
    public static DevicesViewModel Loading() => Of(DispatchPhase.Loading);

    /// <summary>Gets the state once the devices loaded.</summary>
    /// <param name="devices">The devices.</param>
    /// <returns>The rows, or the no-devices phase when the account has none.</returns>
    public static DevicesViewModel Loaded(IReadOnlyList<DispatchDevice> devices)
    {
        ArgumentNullException.ThrowIfNull(devices);
        return devices.Count == 0
            ? Of(DispatchPhase.NoDevices)
            : new DevicesViewModel(null, string.Empty, devices.Select(DeviceRowModel.From).ToArray());
    }

    /// <summary>Gets the state when the devices could not be read.</summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The phase the failure means.</returns>
    public static DevicesViewModel Failed(DispatchHostException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return Of(DispatchPhaseMap.ForHostFailure(failure.Kind));
    }

    private static DevicesViewModel Of(DispatchPhase phase) => new(phase, DispatchCopy.ForPhase(phase), []);
}
