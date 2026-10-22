using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace OmpOS.BrowserSpike;

/// <summary>Application that opens the spike window on the classic desktop lifetime.</summary>
internal sealed class SpikeApplication : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new SpikeWindow(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
