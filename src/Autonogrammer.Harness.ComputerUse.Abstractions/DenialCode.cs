namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Stable gate refusal. Each value has one <see cref="DenialCodeCatalog.UserMessageId"/>.</summary>
public enum DenialCode
{
    /// <summary>Feature off, signed out, or not a Cowork session. Message id <c>computer.denial.disabled</c>.</summary>
    Disabled = 0,

    /// <summary>The session has not opted in. Message id <c>computer.denial.not-opted-in</c>.</summary>
    NotOptedIn = 1,

    /// <summary>Kill switch tripped or the grant was revoked. Message id <c>computer.denial.revoked</c>.</summary>
    Revoked = 2,

    /// <summary>Platform or Wayland unavailable. Message id <c>computer.denial.platform-unavailable</c>.</summary>
    PlatformUnavailable = 3,

    /// <summary>An operating-system permission is missing. Message id <c>computer.denial.permission-missing</c>.</summary>
    PermissionMissing = 4,

    /// <summary>Kill switch is not registered. Message id <c>computer.denial.kill-switch-unavailable</c>.</summary>
    KillSwitchUnavailable = 5,

    /// <summary>Indicator is missing or minimised. Message id <c>computer.denial.indicator-unavailable</c>.</summary>
    IndicatorUnavailable = 6,

    /// <summary>Frame id is not the latest, or the layout changed. Message id <c>computer.denial.stale-frame</c>.</summary>
    StaleFrame = 7,

    /// <summary>Coordinates are outside the frame. Message id <c>computer.denial.out-of-bounds</c>.</summary>
    OutOfBounds = 8,

    /// <summary>The application is on the denylist. Message id <c>computer.denial.denied-application</c>.</summary>
    DeniedApplication = 9,

    /// <summary>The target is this process. Message id <c>computer.denial.own-window</c>.</summary>
    OwnWindow = 10,

    /// <summary>No grant covers the target. Message id <c>computer.denial.no-grant</c>.</summary>
    NoGrant = 11,

    /// <summary>The grant tier is too low. Message id <c>computer.denial.tier-too-low</c>.</summary>
    TierTooLow = 12,

    /// <summary>The chord is hard-denied. Message id <c>computer.denial.forbidden-chord</c>.</summary>
    ForbiddenChord = 13,

    /// <summary>The Windows target is elevated. Message id <c>computer.denial.elevated-target</c>.</summary>
    ElevatedTarget = 14,

    /// <summary>The secure desktop is active. Message id <c>computer.denial.secure-desktop</c>.</summary>
    SecureDesktop = 15,

    /// <summary>The rate limit refused the action. Message id <c>computer.denial.rate-limited</c>.</summary>
    RateLimited = 16,

    /// <summary>The session step limit was reached. Message id <c>computer.denial.step-limit</c>.</summary>
    StepLimit = 17,

    /// <summary>The user moved the pointer. Message id <c>computer.denial.user-took-over</c>.</summary>
    UserTookOver = 18,

    /// <summary>The session image budget is exhausted. Message id <c>computer.denial.image-budget</c>.</summary>
    ImageBudget = 19,

    /// <summary>A frame could not be encoded under the byte cap. Message id <c>computer.denial.frame-too-large</c>.</summary>
    FrameTooLarge = 20,

    /// <summary>The local user denied the prompt. Message id <c>computer.denial.approval-denied</c>.</summary>
    ApprovalDenied = 21,

    /// <summary>The local prompt timed out to deny. Message id <c>computer.denial.approval-timeout</c>.</summary>
    ApprovalTimeout = 22,

    /// <summary>No local user can answer. Message id <c>computer.denial.no-local-approver</c>.</summary>
    NoLocalApprover = 23,

    /// <summary>The write-ahead audit record failed. Message id <c>computer.denial.audit-unavailable</c>.</summary>
    AuditUnavailable = 24,

    /// <summary>The gate failed closed. Message id <c>computer.denial.internal-error</c>.</summary>
    InternalError = 25,
}

