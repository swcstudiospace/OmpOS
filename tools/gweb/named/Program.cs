using Avalonia;
using Avalonia.Browser;

namespace Autonogrammer.Gweb.Named;

internal static class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp().StartBrowserAppAsync("out");

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
