using System.Text.Json.Serialization;

namespace Autonogrammer.Contracts.Dispatch;

/// <summary>Closed failure reasons shown to the user. Seventeen values, no extras.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DispatchFailureReason>))]
public enum DispatchFailureReason
{
    /// <summary>The delivery deadline passed.</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,

    /// <summary>The device or command was revoked.</summary>
    [JsonStringEnumMemberName("revoked")]
    Revoked,

    /// <summary>The command signature did not verify.</summary>
    [JsonStringEnumMemberName("signature_invalid")]
    SignatureInvalid,

    /// <summary>The protocol version is not supported.</summary>
    [JsonStringEnumMemberName("unsupported_version")]
    UnsupportedVersion,

    /// <summary>The operation is not in the closed set.</summary>
    [JsonStringEnumMemberName("unsupported_operation")]
    UnsupportedOperation,

    /// <summary>The desktop has the capability disabled.</summary>
    [JsonStringEnumMemberName("capability_disabled")]
    CapabilityDisabled,

    /// <summary>The local user denied the command.</summary>
    [JsonStringEnumMemberName("approval_denied")]
    ApprovalDenied,

    /// <summary>The local approval wait expired.</summary>
    [JsonStringEnumMemberName("approval_timeout")]
    ApprovalTimeout,

    /// <summary>Desktop policy refused the command.</summary>
    [JsonStringEnumMemberName("policy_denied")]
    PolicyDenied,

    /// <summary>The payload is not valid for the operation.</summary>
    [JsonStringEnumMemberName("payload_invalid")]
    PayloadInvalid,

    /// <summary>The payload exceeds the size cap.</summary>
    [JsonStringEnumMemberName("payload_too_large")]
    PayloadTooLarge,

    /// <summary>The device is already running a conflicting command.</summary>
    [JsonStringEnumMemberName("device_busy")]
    DeviceBusy,

    /// <summary>The device restarted and no longer runs the command.</summary>
    [JsonStringEnumMemberName("device_restarted")]
    DeviceRestarted,

    /// <summary>Execution failed on the device.</summary>
    [JsonStringEnumMemberName("execution_error")]
    ExecutionError,

    /// <summary>The run limit elapsed.</summary>
    [JsonStringEnumMemberName("timeout")]
    Timeout,

    /// <summary>The user cancelled the command.</summary>
    [JsonStringEnumMemberName("cancelled_by_user")]
    CancelledByUser,

    /// <summary>An internal failure with no more specific reason.</summary>
    [JsonStringEnumMemberName("internal")]
    Internal,
}
