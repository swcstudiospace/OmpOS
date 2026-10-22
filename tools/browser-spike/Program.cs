using Avalonia;
using Avalonia.Platform;

namespace OmpOS.BrowserSpike;

/// <summary>Composition root of the ADR-0009 browser spike.</summary>
internal static class Program
{
    /// <summary>Environment variable that forces one <see cref="WebViewAdapterType"/> by name, for measuring a single engine.</summary>
    internal const string AdapterOverrideVariable = "OMPOS_SPIKE_ADAPTER";

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--list-adapters", StringComparer.Ordinal))
        {
            foreach (string name in Enum.GetNames<WebViewAdapterType>())
            {
                Console.WriteLine(name);
            }

            return 0;
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<SpikeApplication>().UsePlatformDetect();
}
