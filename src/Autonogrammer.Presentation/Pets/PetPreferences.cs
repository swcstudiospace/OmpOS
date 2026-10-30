namespace Autonogrammer.Presentation.Pets;

/// <summary>
/// The user's companion preferences: the hide toggle and the motion setting. The record carries no platform state; the operating-system
/// reduced-motion flag is passed to <see cref="IsMotionReduced"/> by the head that can read it.
/// </summary>
/// <param name="IsHidden">True when the user hid the companion. A hidden companion is removed, not just frozen.</param>
/// <param name="Motion">The motion setting. <see cref="MotionPreference.System"/> defers to the operating-system flag.</param>
public sealed record PetPreferences(bool IsHidden = false, MotionPreference Motion = MotionPreference.System)
{
    /// <summary>Returns whether the companion's animation must be off.</summary>
    /// <param name="systemPrefersReducedMotion">The operating-system reduced-motion flag, false when the head cannot read it (OQ-34).</param>
    /// <returns>
    /// True when <see cref="Motion"/> is <see cref="MotionPreference.Reduced"/>, or is <see cref="MotionPreference.System"/> and the system asks for
    /// reduced motion. An undefined setting is treated as reduced, the safe side.
    /// </returns>
    public bool IsMotionReduced(bool systemPrefersReducedMotion) => Motion switch
    {
        MotionPreference.Full => false,
        MotionPreference.System => systemPrefersReducedMotion,
        _ => true,
    };
}
