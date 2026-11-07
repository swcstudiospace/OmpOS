namespace Autonogrammer.Harness.ComputerUse.Abstractions;

/// <summary>Stable remediation message ids for <see cref="ComputerAvailability"/>. One id per state.</summary>
public static class ComputerAvailabilityRemediation
{
    /// <summary>Message id for <see cref="ComputerAvailability.Available"/>.</summary>
    public const string Available = "computer.available";

    /// <summary>Message id for <see cref="ComputerAvailability.Disabled"/>.</summary>
    public const string Disabled = "computer.unavailable.disabled";

    /// <summary>Message id for <see cref="ComputerAvailability.UnsupportedPlatform"/>.</summary>
    public const string UnsupportedPlatform = "computer.unavailable.platform";

    /// <summary>Message id for <see cref="ComputerAvailability.WaylandUnsupported"/>.</summary>
    public const string WaylandUnsupported = "computer.unavailable.wayland";

    /// <summary>Message id for <see cref="ComputerAvailability.NoDisplay"/>.</summary>
    public const string NoDisplay = "computer.unavailable.display";

    /// <summary>Message id for <see cref="ComputerAvailability.PermissionMissing"/>.</summary>
    public const string PermissionMissing = "computer.unavailable.permission";

    /// <summary>Message id for <see cref="ComputerAvailability.InputInjectionUnavailable"/>.</summary>
    public const string InputInjectionUnavailable = "computer.unavailable.input";

    /// <summary>Message id for <see cref="ComputerAvailability.KillSwitchUnavailable"/>.</summary>
    public const string KillSwitchUnavailable = "computer.unavailable.killswitch";

    /// <summary>Message id for <see cref="ComputerAvailability.IndicatorUnavailable"/>.</summary>
    public const string IndicatorUnavailable = "computer.unavailable.indicator";

    /// <summary>Message id for <see cref="ComputerAvailability.AuditUnavailable"/>.</summary>
    public const string AuditUnavailable = "computer.unavailable.audit";

    /// <summary>Message id for <see cref="ComputerAvailability.SecureDesktop"/>.</summary>
    public const string SecureDesktop = "computer.unavailable.securedesktop";

    /// <summary>Returns the single remediation id for <paramref name="availability"/>.</summary>
    /// <param name="availability">The availability state.</param>
    /// <returns>The message id.</returns>
    public static string For(ComputerAvailability availability) => availability switch
    {
        ComputerAvailability.Available => Available,
        ComputerAvailability.Disabled => Disabled,
        ComputerAvailability.UnsupportedPlatform => UnsupportedPlatform,
        ComputerAvailability.WaylandUnsupported => WaylandUnsupported,
        ComputerAvailability.NoDisplay => NoDisplay,
        ComputerAvailability.PermissionMissing => PermissionMissing,
        ComputerAvailability.InputInjectionUnavailable => InputInjectionUnavailable,
        ComputerAvailability.KillSwitchUnavailable => KillSwitchUnavailable,
        ComputerAvailability.IndicatorUnavailable => IndicatorUnavailable,
        ComputerAvailability.AuditUnavailable => AuditUnavailable,
        ComputerAvailability.SecureDesktop => SecureDesktop,
        _ => throw new ArgumentOutOfRangeException(nameof(availability)),
    };
}
