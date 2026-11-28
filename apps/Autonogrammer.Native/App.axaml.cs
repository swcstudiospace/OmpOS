using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings;
using Autonogrammer.Native.Views.Branding;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

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
    private static readonly Lock LogListenerLock = new();
    private static bool _logListenerInstalled;

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
    /// Loads the application XAML. The trace listener is installed first (to a log file; the app has no console), so traces raised from here on reach the operator.
    /// </summary>
    [SuppressMessage("Reliability", "CA2000", Justification = "Ownership transfer: the created StreamWriter is hosted by the installed StderrTraceListener, which flushes and disposes it when replaced via InstallStderrTraceListener or when a later App instance supersedes it.")]
    public override void Initialize()
    {
        // The app is a WinExe (no console); traces go to a log file, never Console.Error. The listener is process-global, so it
        // is installed at most once: headless PerTest isolation constructs many App instances, and one undisposed StreamWriter
        // per instance would leak the log handle and make later opens race on FileShare.
        var install = false;
        lock (LogListenerLock)
        {
            if (!_logListenerInstalled)
            {
                _logListenerInstalled = true;
                install = true;
            }
        }

        if (install)
        {
            try
            {
                var logDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Autonogrammer", "logs");
                Directory.CreateDirectory(logDirectory);
                var logPath = Path.Combine(logDirectory, $"autonogrammer-{DateTime.Now:yyyy-MM-dd}.log");
                InstallStderrTraceListener(new StreamWriter(logPath, append: true) { AutoFlush = true });
            }
            catch (IOException)
            {
                // Best-effort only: a missing log directory must never break startup.
            }
            catch (UnauthorizedAccessException)
            {
                // Best-effort only: a missing log directory must never break startup.
            }
        }

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
            previous.Dispose();
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
        var window = new MainWindow(head.Panel, head.Settings.Motion, head.WorkspaceRoot, head.Board, head.Services, Autonogrammer.Native.Cowork.CoworkSurfaceServicesFactory.TryBuild(head));
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
    internal sealed class StderrTraceListener(TextWriter error) : TraceListener, IDisposable
    {
        /// <inheritdoc/>
        public override void Write(string? message) => error.Write(message);

        /// <inheritdoc/>
        public override void WriteLine(string? message) => error.WriteLine(message);

        /// <inheritdoc/>
        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                error.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
