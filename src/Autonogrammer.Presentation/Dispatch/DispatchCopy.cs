using Autonogrammer.Contracts.Dispatch;

namespace Autonogrammer.Presentation.Dispatch;

/// <summary>The words a Dispatch surface shows. Every state has its own text; none is conveyed by colour alone.</summary>
public static class DispatchCopy
{
    /// <summary>Shown beside a device that is connected.</summary>
    public const string Connected = "Connected.";

    /// <summary>Shown when the head has no Dispatch service configured.</summary>
    public const string NotConfigured = "Dispatch is not set up in this app.";

    /// <summary>Shown beside this desktop when it is paired to the account.</summary>
    public const string ThisDevicePaired = "This desktop is paired to your account.";

    /// <summary>Shown beside this desktop when it is not paired.</summary>
    public const string ThisDeviceNotPaired = "This desktop is not paired. Pair it to send and receive commands.";

    /// <summary>Gets the text for a phase.</summary>
    /// <param name="phase">The phase.</param>
    /// <returns>The text.</returns>
    public static string ForPhase(DispatchPhase phase) => phase switch
    {
        DispatchPhase.NoDevices => "No devices are paired. Pair this desktop or another device to send commands.",
        DispatchPhase.DeviceOffline => "The device is offline. The command waits until it connects.",
        DispatchPhase.Queued => "Queued for the device.",
        DispatchPhase.PendingApproval => "Waiting for approval on the device.",
        DispatchPhase.Running => "Running on the device.",
        DispatchPhase.Failed => "Failed on the device.",
        DispatchPhase.Completed => "Completed.",
        DispatchPhase.Cancelled => "Cancelled.",
        DispatchPhase.Expired => "Expired before the device received it.",
        DispatchPhase.SignedOut => "Sign in to use Dispatch.",
        DispatchPhase.Disabled => "Dispatch is off for this account.",
        DispatchPhase.Unreachable => "Dispatch cannot be reached right now. Try again later.",
        DispatchPhase.Loading => "Loading devices.",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    /// <summary>Gets the message for a degraded state.</summary>
    /// <param name="degradation">The degraded state.</param>
    /// <returns>The message.</returns>
    public static string ForDegradation(DispatchDegradation degradation) => degradation switch
    {
        DispatchDegradation.PresenceUnknown => "Presence is unknown. The device may be online.",
        DispatchDegradation.CredentialExpired => "Your sign-in for this device expired. Pair the device again.",
        DispatchDegradation.DeviceRevoked => "This device was revoked and no longer receives commands.",
        DispatchDegradation.ReplacedByOtherInstance => "Dispatch is open in another copy of the app, which now holds the connection.",
        DispatchDegradation.UpdateRequired => "Update the app to use Dispatch with this device.",
        DispatchDegradation.AccountUnavailable => "This account cannot use Dispatch right now.",
        DispatchDegradation.RateLimited => "Too many commands were sent. Wait a moment and try again.",
        DispatchDegradation.PayloadTooLarge => "The command is too large to send.",
        DispatchDegradation.CapabilityUnavailable => "This capability is not available on the device.",
        DispatchDegradation.PausedByUser => "Dispatch is paused on this device.",
        DispatchDegradation.CoordinationPaused => "Dispatch is paused for maintenance.",
        DispatchDegradation.KeyStoreUnavailable => "This device has no key store, so Dispatch cannot protect its key.",
        DispatchDegradation.PairedToAnotherAccount => "This device is paired to another account.",
        _ => throw new ArgumentOutOfRangeException(nameof(degradation), degradation, null),
    };

    /// <summary>Gets the message for a closed failure reason.</summary>
    /// <param name="reason">The failure reason.</param>
    /// <returns>The message.</returns>
    public static string ForFailure(DispatchFailureReason reason) => reason switch
    {
        DispatchFailureReason.Expired => "The command expired before the device received it.",
        DispatchFailureReason.Revoked => "The device or command was revoked.",
        DispatchFailureReason.SignatureInvalid => "The device rejected the command's signature.",
        DispatchFailureReason.UnsupportedVersion => "The device runs a protocol version this app cannot use.",
        DispatchFailureReason.UnsupportedOperation => "The device does not support this operation.",
        DispatchFailureReason.CapabilityDisabled => "This capability is turned off on the device.",
        DispatchFailureReason.ApprovalDenied => "You denied the command on the device.",
        DispatchFailureReason.ApprovalTimeout => "Nobody approved the command in time.",
        DispatchFailureReason.PolicyDenied => "The device's policy refused the command.",
        DispatchFailureReason.PayloadInvalid => "The command's payload is not valid for this operation.",
        DispatchFailureReason.PayloadTooLarge => "The command's payload is larger than the device accepts.",
        DispatchFailureReason.DeviceBusy => "The device is already running a conflicting command.",
        DispatchFailureReason.DeviceRestarted => "The device restarted, so the command did not finish.",
        DispatchFailureReason.ExecutionError => "The command failed on the device.",
        DispatchFailureReason.Timeout => "The command ran past its time limit.",
        DispatchFailureReason.CancelledByUser => "You cancelled the command.",
        DispatchFailureReason.Internal => "The command failed for an unexpected reason.",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}
