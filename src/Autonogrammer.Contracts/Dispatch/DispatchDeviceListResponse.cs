namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Body of <c>GET /dispatch/devices</c>.</summary>
public sealed record DispatchDeviceListResponse
{
    /// <summary>Devices owned by the caller.</summary>
    public required IReadOnlyList<DispatchDeviceDto> Devices { get; init; }
}
