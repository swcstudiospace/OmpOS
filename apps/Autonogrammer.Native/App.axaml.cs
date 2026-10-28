using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Branding;

namespace Autonogrammer.Native;

/// <summary>
/// The Avalonia application root. It loads the application XAML, which merges the token dictionary and the shared control styles, and
/// on a classic desktop lifetime composes the head of the Agent Panel and opens the main window over it. The head is composed and its
/// registry loaded behind the open window, so the panel shows its Loading state first; on shutdown the head stops every engine before
/// the process ends.
/// </summary>
internal partial class App : Application
{
    private const int ShutdownNotRequested = 0;
    private const int ShutdownRunning = 1;
    private const int ShutdownDone = 2;

    private readonly Func<AgentHead>? _headFactory;
    private readonly Func<Func<TopLevel?>, CancellationToken, Task<AgentHead>>? _headFactoryAsync;
    private AgentHead? _head;
    private Task _initialisation = Task.CompletedTask;
    private int _shutdownState;

    /// <summary>Creates the application with the production head, which reads the plugins, the state and the Substrate from the process environment.</summary>
    public App()
    {
        _headFactoryAsync = AgentHead.CreateProductionAsync;
    }

    /// <summary>Creates the application over a head that is composed when the main window opens.</summary>
    /// <param name="headFactory">Composes the head. It runs once, on the desktop lifetime, never in a headless test.</param>
    /// <exception cref="ArgumentNullException"><paramref name="headFactory"/> is null.</exception>
    public App(Func<AgentHead> headFactory)
    {
        ArgumentNullException.ThrowIfNull(headFactory);
        _headFactory = headFactory;
    }

    /// <summary>
    /// Loads the application XAML. The stderr trace listener is installed first, so traces raised from here on reach the operator.
    /// </summary>
    public override void Initialize()
    {
        InstallStderrTraceListener(Console.Error);
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>
    /// Installs the stderr trace listener and removes any listener this method installed before, so at most one is present. The
    /// head reports through <see cref="Trace"/>; this listener is the operator's output in the desktop app.
    /// </summary>
    /// <param name="error">The writer the listener writes to. The application passes the process's standard error.</param>
    /// <returns>The installed listener.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="error"/> is null.</exception>
    internal static StderrTraceListener InstallStderrTraceListener(TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(error);
        foreach (var previous in Trace.Listeners.OfType<StderrTraceListener>().ToArray())
        {
            Trace.Listeners.Remove(previous);
        }

        var listener = new StderrTraceListener(error);
        Trace.Listeners.Add(listener);
        return listener;
    }

    /// <summary>
    /// Composes the head and opens the main window once the desktop lifetime is available, then loads the registry behind the open
    /// window. Headless tests create their own windows.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownRequested += OnShutdownRequested;
            if (_headFactory is { } factory)
            {
                var head = factory();
                _head = head;
                _initialisation = OpenWindowAsync(desktop, head);
            }
            else
            {
                // The services every window shares are opened asynchronously, so the window opens as soon as they are ready.
                _initialisation = OpenAsync(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task OpenAsync(IClassicDesktopStyleApplicationLifetime desktop)
    {
        AgentHead head;
        try
        {
            head = await _headFactoryAsync!(() => desktop.MainWindow, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Trace.TraceError($"The agent head could not be composed ({exception.GetType().Name}): {exception.Message}");
            desktop.Shutdown(1);
            return;
        }

        _head = head;
        var window = CreateWindow(head);
        desktop.MainWindow = window;
        window.Show();
        await head.InitializeAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private Task OpenWindowAsync(IClassicDesktopStyleApplicationLifetime desktop, AgentHead head)
    {
        desktop.MainWindow = CreateWindow(head);
        return head.InitializeAsync(CancellationToken.None);
    }

    private MainWindow CreateWindow(AgentHead head)
    {
        RequestedThemeVariant = HeadSettings.VariantOf(head.Settings.Theme);
        var window = new MainWindow(head.Panel, head.Settings.Motion, Environment.CurrentDirectory, head.Board, head.Services);
        AppIcon.Apply(window);
        head.Settings.Attach(window);
        return window;
    }

    /// <summary>
    /// The first request to shut down is held while the head stops every engine, so no <c>omp</c> child outlives the window; the request
    /// is then repeated and allowed.
    /// </summary>
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        if (_head is null || Volatile.Read(ref _shutdownState) == ShutdownDone)
        {
            return;
        }

        e.Cancel = true;
        if (Interlocked.CompareExchange(ref _shutdownState, ShutdownRunning, ShutdownNotRequested) == ShutdownNotRequested
            && sender is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _ = StopHeadThenShutDownAsync(_head, desktop);
        }
    }

    private async Task StopHeadThenShutDownAsync(AgentHead head, IClassicDesktopStyleApplicationLifetime desktop)
    {
        try
        {
            await _initialisation.WaitAsync(CancellationToken.None).ConfigureAwait(true);
            await head.DisposeAsync().ConfigureAwait(true);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Stopping the agent engines failed ({exception.GetType().Name}).");
        }
        finally
        {
            Volatile.Write(ref _shutdownState, ShutdownDone);
            desktop.Shutdown();
        }
    }

    /// <summary>
    /// A trace listener that writes each trace line to the given writer. The application points it at standard error, so the head's
    /// traces reach the operator who launched it.
    /// </summary>
    internal sealed class StderrTraceListener(TextWriter error) : TraceListener
    {
        /// <inheritdoc/>
        public override void Write(string? message) => error.Write(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => error.WriteLine(message);
    }
}
