namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Wire names for <see cref="DispatchFailureReason"/>.</summary>
public static class DispatchFailureReasonWire
{
    /// <summary>Parses one closed failure reason.</summary>
    /// <param name="text">The wire value.</param>
    /// <param name="reason">The reason when parsing succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is one of the seventeen closed names.</returns>
    public static bool TryParse(string? text, out DispatchFailureReason reason)
    {
        switch (text)
        {
            case "expired":
                reason = DispatchFailureReason.Expired;
                return true;
            case "revoked":
                reason = DispatchFailureReason.Revoked;
                return true;
            case "signature_invalid":
                reason = DispatchFailureReason.SignatureInvalid;
                return true;
            case "unsupported_version":
                reason = DispatchFailureReason.UnsupportedVersion;
                return true;
            case "unsupported_operation":
                reason = DispatchFailureReason.UnsupportedOperation;
                return true;
            case "capability_disabled":
                reason = DispatchFailureReason.CapabilityDisabled;
                return true;
            case "approval_denied":
                reason = DispatchFailureReason.ApprovalDenied;
                return true;
            case "approval_timeout":
                reason = DispatchFailureReason.ApprovalTimeout;
                return true;
            case "policy_denied":
                reason = DispatchFailureReason.PolicyDenied;
                return true;
            case "payload_invalid":
                reason = DispatchFailureReason.PayloadInvalid;
                return true;
            case "payload_too_large":
                reason = DispatchFailureReason.PayloadTooLarge;
                return true;
            case "device_busy":
                reason = DispatchFailureReason.DeviceBusy;
                return true;
            case "device_restarted":
                reason = DispatchFailureReason.DeviceRestarted;
                return true;
            case "execution_error":
                reason = DispatchFailureReason.ExecutionError;
                return true;
            case "timeout":
                reason = DispatchFailureReason.Timeout;
                return true;
            case "cancelled_by_user":
                reason = DispatchFailureReason.CancelledByUser;
                return true;
            case "internal":
                reason = DispatchFailureReason.Internal;
                return true;
            default:
                reason = default;
                return false;
        }
    }

    /// <summary>Returns the wire name of a failure reason.</summary>
    /// <param name="reason">The reason.</param>
    /// <returns>The exact protocol string.</returns>
    public static string ToWire(DispatchFailureReason reason) => reason switch
    {
        DispatchFailureReason.Expired => "expired",
        DispatchFailureReason.Revoked => "revoked",
        DispatchFailureReason.SignatureInvalid => "signature_invalid",
        DispatchFailureReason.UnsupportedVersion => "unsupported_version",
        DispatchFailureReason.UnsupportedOperation => "unsupported_operation",
        DispatchFailureReason.CapabilityDisabled => "capability_disabled",
        DispatchFailureReason.ApprovalDenied => "approval_denied",
        DispatchFailureReason.ApprovalTimeout => "approval_timeout",
        DispatchFailureReason.PolicyDenied => "policy_denied",
        DispatchFailureReason.PayloadInvalid => "payload_invalid",
        DispatchFailureReason.PayloadTooLarge => "payload_too_large",
        DispatchFailureReason.DeviceBusy => "device_busy",
        DispatchFailureReason.DeviceRestarted => "device_restarted",
        DispatchFailureReason.ExecutionError => "execution_error",
        DispatchFailureReason.Timeout => "timeout",
        DispatchFailureReason.CancelledByUser => "cancelled_by_user",
        DispatchFailureReason.Internal => "internal",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };
}
