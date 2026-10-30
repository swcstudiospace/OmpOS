namespace Autonogrammer.Presentation;

/// <summary>
/// Resolves operating-system animation settings to a <see cref="MotionPreference"/>. The platform reader lives in the head;
/// the decision lives here, so it is tested once and shared.
/// </summary>
public static class MotionPreferenceResolver
{
    /// <summary>
    /// Resolves an animation duration scale, the value behind "Remove animations" on Android (<c>animator_duration_scale</c>).
    /// A scale of zero or below means the user turned animations off.
    /// </summary>
    /// <param name="animationDurationScale">The operating system's animation duration scale. 1 is normal speed; 0 is off.</param>
    /// <returns><see cref="MotionPreference.Reduced"/> when animations are off, otherwise <see cref="MotionPreference.Full"/>.</returns>
    public static MotionPreference FromAnimationDurationScale(float animationDurationScale) =>
        animationDurationScale <= 0f ? MotionPreference.Reduced : MotionPreference.Full;
}
