using Avalonia.iOS;
using Foundation;

namespace Autonogrammer.iOS;

/// <summary>
/// The UIKit application delegate of the iOS head. UIKit creates it at launch; it builds the Avalonia application with the iOS
/// platform, so the scene can host the shared main view.
/// </summary>
[Register("AppDelegate")]
public partial class AppDelegate : AvaloniaAppDelegate<AutonogrammerIOSApp>
{
}
