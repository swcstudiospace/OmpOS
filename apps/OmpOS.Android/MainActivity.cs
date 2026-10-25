using Android.Content.PM;
using Avalonia.Android;

namespace OmpOS.Android;

/// <summary>
/// The single Android activity. It hosts the Avalonia application, which shows the shared main view in a single-view lifetime.
/// The activity handles the configuration changes itself, so a rotation or a window resize keeps the view and its state.
/// </summary>
[Activity(
    Label = "OmpOS",
    Theme = "@style/OmpOSTheme",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
