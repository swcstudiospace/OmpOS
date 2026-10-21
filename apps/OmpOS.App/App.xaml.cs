using System.Diagnostics;
using System.Text.Json;
using OmpOS.EngineHost;

namespace OmpOS.App;

/// <summary>
/// Application shell: opens the session page and replays missed remote events on foreground resume.
/// </summary>
public partial class OmpOsApp : Application
{
    private readonly IEngineHost _host;

    /// <summary>Initializes a new instance of the <see cref="OmpOsApp"/> class.</summary>
    /// <param name="host">The platform engine host, resolved from DI.</param>
    public OmpOsApp(IEngineHost host)
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

    private void OnWindowResumed(object? sender, EventArgs e)
    {
        // Desktop hosts stream in-process, so only the remote (mobile) host has anything to resume.
        if (_host is not RemoteEngineHost remote)
        {
            return;
        }

        // The handler stays synchronous (no async void): the resume runs in the background and its
        // task is observed here, so every failure is logged and none is lost.
        _ = ResumeRemoteAsync(remote).ContinueWith(
            static faulted => Debug.WriteLine($"Foreground resume faulted: {faulted.Exception}"),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    /// <summary>Reconnects the remote host and holds the missed events for rendering.</summary>
    /// <param name="remote">The remote-only engine host bound by DI on mobile targets.</param>
    /// <remarks>Expected resume failures are logged here; an unexpected fault is logged by the
    /// observing continuation in <see cref="OnWindowResumed"/>. Resume is best-effort: the next
    /// stream replays from the stored cursor.</remarks>
    private async Task ResumeRemoteAsync(RemoteEngineHost remote)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            // The fetch holds the missed events and raises MissedEventsHeld; SessionHost renders them via TakeHeldEvents.
            await remote.ReconnectAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            // Resume is best-effort; the next stream replays from the stored cursor.
            Debug.WriteLine($"Foreground resume skipped: {ex.Message}");
        }
    }
}
