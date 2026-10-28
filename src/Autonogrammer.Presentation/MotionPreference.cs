namespace Autonogrammer.Presentation;

/// <summary>
/// The motion setting a head applies to its shared controls. <c>System</c> defers to the operating
/// system; a head that cannot read the operating-system setting treats it as <c>Full</c>.
/// </summary>
public enum MotionPreference
{
    /// <summary>Defer to the operating-system reduced-motion setting.</summary>
    System,

    /// <summary>Remove non-essential transitions and animations.</summary>
    Reduced,

    /// <summary>Keep every transition and animation.</summary>
    Full,
}
