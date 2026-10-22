using Avalonia;
using OmpOS.Native.Engine;

namespace OmpOS.Native;

/// <summary>
/// The single composition root of the native head. It builds the Avalonia application and starts it on the
/// classic desktop lifetime. OQ-33 names <c>StartWithClassicDesktopLifetime</c>; the package documentation for 12.1.4 confirms it.
/// </summary>
internal static class Program
{
    /// <summary>Starts the native desktop head. No engine is configured, so tool approvals stay unconnected.</summary>
    /// <param name="args">Command-line arguments passed to the desktop lifetime.</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the application with the windowing platform detected at run time and no engine configured.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() => BuildAvaloniaApp(composeEngine: null);

    /// <summary>
    /// Builds the application with the windowing platform detected at run time. When an engine is configured,
    /// <paramref name="composeEngine"/> runs on the first tool-approval decision, never at launch.
    /// </summary>
    /// <param name="composeEngine">Composes the engine on first use. Null means no engine is configured, and approvals stay unconnected.</param>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp(Func<EngineComposition>? composeEngine)
    {
        if (composeEngine is null)
        {
            return AppBuilder.Configure<App>().UsePlatformDetect();
        }

        var engine = new Lazy<EngineComposition>(composeEngine);
        return AppBuilder.Configure(() => new App(engine)).UsePlatformDetect();
    }
}
