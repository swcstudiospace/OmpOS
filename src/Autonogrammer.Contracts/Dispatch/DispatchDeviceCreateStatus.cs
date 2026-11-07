namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Result of creating a device row.</summary>
public enum DispatchDeviceCreateStatus
{
    /// <summary>The row was inserted.</summary>
    Created,

    /// <summary>The account already has the device cap of non-revoked devices.</summary>
    DeviceLimitReached,

    /// <summary>That device identifier already exists for the account.</summary>
    DeviceIdTaken,
}
