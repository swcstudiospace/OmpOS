using Avalonia;
using Avalonia.Browser;

namespace OmpOS.Gweb.Stock;

internal static class Program
{
    private static Task Main(string[] args) => BuildAvaloniaApp().StartBrowserAppAsync("out");

    private static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();
}
