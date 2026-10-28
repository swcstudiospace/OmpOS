using Android.Runtime;
using Avalonia.Android;

namespace Autonogrammer.Android;

/// <summary>
/// The Android application object. Android creates it before the activity; it builds the Avalonia application with the Android
/// platform, so the activity can host the shared main view.
/// </summary>
[Application]
public sealed class AutonogrammerAndroidApplication : AvaloniaAndroidApplication<AutonogrammerAndroidApp>
{
    /// <summary>Creates the application object from its Java peer. Android calls this constructor.</summary>
    /// <param name="handle">The Java peer handle.</param>
    /// <param name="transfer">The handle ownership.</param>
    public AutonogrammerAndroidApplication(nint handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
    }
}
