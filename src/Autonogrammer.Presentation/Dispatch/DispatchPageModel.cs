namespace Autonogrammer.Presentation.Dispatch;

/// <summary>
/// The Dispatch page: the devices, the command list, and this desktop's pairing and policy when the head has an <see cref="IThisDeviceHost"/>.
/// The web head passes none, so it has no pairing section; its pair text says to pair in the desktop app.
/// </summary>
/// <param name="Devices">The devices.</param>
/// <param name="Commands">The commands, newest first.</param>
/// <param name="PairThisDevice">This desktop's pairing and policy, or null when the head has no local device.</param>
public sealed record DispatchPageModel(
    DevicesViewModel Devices,
    IReadOnlyList<DispatchCommandRowModel> Commands,
    PairThisDeviceModel? PairThisDevice);
