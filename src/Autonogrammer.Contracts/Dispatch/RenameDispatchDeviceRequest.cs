namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Body of <c>PATCH /dispatch/devices/{id}</c>.</summary>
public sealed record RenameDispatchDeviceRequest
{
    /// <summary>New display name.</summary>
    public required string Name { get; init; }
}
