namespace Autonogrammer.Presentation.Dispatch;

/// <summary>The one phase a Dispatch row or page shows. Each value has its own text in <see cref="DispatchCopy"/>; colour never carries a phase alone.</summary>
public enum DispatchPhase
{
    /// <summary>The account has no paired device.</summary>
    NoDevices,

    /// <summary>The target device is not connected.</summary>
    DeviceOffline,

    /// <summary>The command is waiting for the device.</summary>
    Queued,

    /// <summary>The device's user must approve the command.</summary>
    PendingApproval,

    /// <summary>The device is running the command.</summary>
    Running,

    /// <summary>The command failed.</summary>
    Failed,

    /// <summary>The command finished.</summary>
    Completed,

    /// <summary>The command was cancelled.</summary>
    Cancelled,

    /// <summary>The command passed its deadline before delivery.</summary>
    Expired,

    /// <summary>The user is not signed in.</summary>
    SignedOut,

    /// <summary>Dispatch is off for this account or deployment.</summary>
    Disabled,

    /// <summary>The service cannot be reached.</summary>
    Unreachable,

    /// <summary>The state is being read.</summary>
    Loading,
}
