using System.Diagnostics;
using System.Text.Json;
using OmpOS.EngineHost;

namespace OmpOS.App;

/// <summary>
/// Application shell: opens the session page and replays missed remote events on foreground resume.
/// </summary>
public partial class App : Application
{
    private readonly IEngineHost _host;

    /// <summary>Initializes a new instance of the <see cref="App"/> class.</summary>
    /// <param name="host">The platform engine host, resolved from DI.</param>
    public App(IEngineHost host)
    {
        InitializeComponent();
        _host = host;
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(new MainPage());
        window.Resumed += OnWindowResumed;
        return window;
    }

    private async void OnWindowResumed(object? sender, EventArgs e)
    {
        // Desktop hosts stream in-process, so only the remote (mobile) host has anything to resume.
        if (_host is not RemoteEngineHost remote)
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            // The fetch holds the missed events and raises MissedEventsHeld; SessionHost renders them via TakeHeldEvents.
            await remote.ReconnectAsync(timeout.Token).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            // Resume is best-effort; the next stream replays from the stored cursor.
            Debug.WriteLine($"Foreground resume skipped: {ex.Message}");
        }
    }
}
