using Avalonia;

namespace OmpOS.Native;

/// <summary>
/// The single composition root of the native head. It builds the Avalonia application and starts it on the
/// classic desktop lifetime. OQ-33 names <c>StartWithClassicDesktopLifetime</c>; the package documentation for 12.1.4 confirms it.
/// </summary>
internal static class Program
{
    /// <summary>Starts the native desktop head.</summary>
    /// <param name="args">Command-line arguments passed to the desktop lifetime.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the application with the windowing platform detected at run time.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect();
}
