namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>
/// Whether Computer Use can arm. Each value has exactly one remediation message id from
/// <see cref="ComputerAvailabilityRemediation"/>. Wayland and platform-less hosts stay unavailable and register no tools.
/// </summary>
public enum ComputerAvailability
{
    /// <summary>The desktop session can arm. Message id <c>computer.available</c>.</summary>
    Available = 0,

    /// <summary>The feature, account, or Cowork session is off. Message id <c>computer.unavailable.disabled</c>.</summary>
    Disabled = 1,

    /// <summary>This head has no desktop session. Message id <c>computer.unavailable.platform</c>.</summary>
    UnsupportedPlatform = 2,

    /// <summary>The session is Wayland, including XWayland. Message id <c>computer.unavailable.wayland</c>.</summary>
    WaylandUnsupported = 3,

    /// <summary>No display is connected. Message id <c>computer.unavailable.display</c>.</summary>
    NoDisplay = 4,

    /// <summary>An OS permission is missing. Message id <c>computer.unavailable.permission</c>.</summary>
    PermissionMissing = 5,

    /// <summary>Input injection is not available. Message id <c>computer.unavailable.input</c>.</summary>
    InputInjectionUnavailable = 6,

    /// <summary>The kill-switch hotkey did not register. Message id <c>computer.unavailable.killswitch</c>.</summary>
    KillSwitchUnavailable = 7,

    /// <summary>The indicator is missing or minimised. Message id <c>computer.unavailable.indicator</c>.</summary>
    IndicatorUnavailable = 8,

    /// <summary>The audit log cannot be written. Message id <c>computer.unavailable.audit</c>.</summary>
    AuditUnavailable = 9,

    /// <summary>The secure desktop is active. Message id <c>computer.unavailable.securedesktop</c>.</summary>
    SecureDesktop = 10,
}

