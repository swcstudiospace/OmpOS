using OmpOS.Presentation;

namespace OmpOS.Android;

/// <summary>
/// Reads the Android "Remove animations" setting. Avalonia 12.1.4 exposes no reduced-motion reader (OQ-34), but Android keeps one
/// number the user controls: the animator duration scale, which is 0 when animations are removed.
/// </summary>
internal static class AndroidMotion
{
    private const float NormalScale = 1f;

    /// <summary>Reads the system setting and resolves it to the motion preference the shared view applies.</summary>
    /// <returns><see cref="MotionPreference.Reduced"/> when the user removed animations, otherwise <see cref="MotionPreference.Full"/>.</returns>
    public static MotionPreference Read()
    {
        var resolver = global::Android.App.Application.Context.ContentResolver;
        var scale = global::Android.Provider.Settings.Global.GetFloat(
            resolver,
            global::Android.Provider.Settings.Global.AnimatorDurationScale,
            NormalScale);
        return MotionPreferenceResolver.FromAnimationDurationScale(scale);
    }
}
