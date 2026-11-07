namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Outcome of <see cref="IDispatchStore.CreateDeviceAsync"/>.</summary>
/// <param name="Status">Whether the row was inserted.</param>
/// <param name="Device">The row when <paramref name="Status"/> is <see cref="DispatchDeviceCreateStatus.Created"/>.</param>
public sealed record DispatchDeviceCreateResult(DispatchDeviceCreateStatus Status, DispatchDeviceRecord? Device);
