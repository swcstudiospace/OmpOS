using UIKit;

namespace Autonogrammer.iOS;

/// <summary>
/// The single composition root of the iOS head. UIKit starts the application through the delegate, which builds the Avalonia
/// application. No process starts and no network call is made at launch.
/// </summary>
internal static class Program
{
    /// <summary>Starts UIKit with the head's application delegate.</summary>
    /// <param name="args">Command-line arguments passed to UIKit.</param>
    private static void Main(string[] args) => UIApplication.Main(args, null, typeof(AppDelegate));
}
