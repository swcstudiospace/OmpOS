using System.Diagnostics;
using Avalonia.Controls;
using OmpOS.Native.Agents;
using OmpOS.Presentation;
using OmpOS.Presentation.Settings;

namespace OmpOS.Native.Settings;

/// <summary>
/// The head's side of the Settings window. It owns the settings file, the one Settings window and the sweeper that puts the Runtime
/// settings in force, and it connects the main window to them: the rail's Settings button and Ctrl+, open the window, the saved terminal
/// auto-run choice is what the Terminal tool reads, and a saved motion choice is applied to the shell at once. Every value comes from the
/// settings file; nothing here holds key material.
/// </summary>
internal sealed class HeadSettings : IAsyncDisposable
{
    private readonly SettingsWindowHost _host;
    private readonly SessionLifecycleSweeper? _sweeper;
    private readonly TimeSpan? _sweepInterval;
    private readonly IDisposable? _owned;
    private readonly CancellationTokenSource _lifetime = new();
    private StoredSettings _applied;
    private MainView? _main;
    private Window? _owner;
    private int _disposed;

    private HeadSettings(
        FileSettingsStore store,
        SettingsWindowHost host,
        SessionLifecycleSweeper? sweeper,
        TimeSpan? sweepInterval,
        IDisposable? owned,
        StoredSettings applied)
    {
        Store = store;
        _host = host;
        _sweeper = sweeper;
        _sweepInterval = sweepInterval;
        _owned = owned;
        _applied = applied;
    }

    /// <summary>Gets the settings file.</summary>
    public FileSettingsStore Store { get; }

    /// <summary>Gets the window host. It keeps the one Settings window.</summary>
    public SettingsWindowHost Host => _host;

    /// <summary>Gets the sweeper that puts the Runtime settings in force, or null when the head runs none.</summary>
    public SessionLifecycleSweeper? Sweeper => _sweeper;

    /// <summary>Gets the motion setting that was saved when the settings were created. A file that cannot be used gives <see cref="MotionPreference.System"/>.</summary>
    public MotionPreference Motion { get; private init; }

    /// <summary>Reads the stored values, or the defaults when the file cannot be used.</summary>
    /// <param name="store">The settings file.</param>
    /// <returns>The values.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public static StoredSettings ReadOrDefaults(FileSettingsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        try
        {
            return store.Read();
        }
        catch (SettingsSourceException)
        {
            return StoredSettings.Defaults;
        }
    }

    /// <summary>Creates the head's settings over the engines and the settings file.</summary>
    /// <param name="engines">The engines and the registry the Runtime settings apply to.</param>
    /// <param name="store">The settings file.</param>
    /// <param name="clock">The clock the Runtime windows are measured on.</param>
    /// <param name="dispatch">Runs work on the context that owns the models, or null to run it inline.</param>
    /// <param name="options">The seams and what the head owns for the Models page.</param>
    /// <returns>The settings. The sweeper starts with <see cref="Start"/>; the window is connected with <see cref="Attach"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public static HeadSettings Create(SessionEngineSet engines, FileSettingsStore store, TimeProvider clock, Action<Action>? dispatch, HeadSettingsOptions options)
    {
        ArgumentNullException.ThrowIfNull(engines);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        var stored = ReadOrDefaults(store);
        var sweeper = options.SweepInterval is null ? null : new SessionLifecycleSweeper(engines, stored.Runtime, clock, dispatch);
        var windowOptions = new SettingsWindowOptions
        {
            Store = store,
            WorkspaceRoot = engines.Loop.WorkspaceRoot,
            PluginsRoot = engines.Loop.PluginsRoot,
            ModelsPageFactory = options.ModelsPageFactory,
            GetVariable = options.GetVariable,
            Commands = options.Commands,
            ApplyRuntime = values => sweeper?.Apply(values),
            RuntimeSaveFailed = static (_, failure) => Trace.TraceWarning($"A Runtime setting could not be saved. {failure.Failure.Message}"),
        };
        var host = new SettingsWindowHost(
            () => SettingsWindowFactory.CreateModel(windowOptions),
            options.WindowFactory ?? (static model => new AvaloniaSettingsWindow(model)));
        return new HeadSettings(store, host, sweeper, options.SweepInterval, options.Owned, stored) { Motion = stored.Motion };
    }

    /// <summary>Starts the sweeper, which makes a first pass over the sessions the registry holds. Call it after the registry is loaded. It does nothing when the head runs no sweeper.</summary>
    public void Start() => _sweeper?.Start(_sweepInterval ?? SessionLifecycleSweeper.DefaultInterval);

    /// <summary>
    /// Connects the main window: the Settings button and Ctrl+, of its main view open the window over it, the Terminal tool reads the saved
    /// auto-run choice, a saved motion choice is applied to the shell, and the Settings window closes with the main window.
    /// </summary>
    /// <param name="owner">The main window. Its content is the <see cref="MainView"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    /// <exception cref="ArgumentException">The window's content is not a main view.</exception>
    /// <exception cref="InvalidOperationException">The settings are already attached to a window.</exception>
    public void Attach(Window owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Content is not MainView main)
        {
            throw new ArgumentException("The window does not show a main view.", nameof(owner));
        }

        if (_main is not null)
        {
            throw new InvalidOperationException("The settings are already attached to a window.");
        }

        _main = main;
        _owner = owner;
        main.TerminalSettingsProvider = () => AgentsSettingsSource.ToTerminalSettings(Store);
        main.SettingsRequested += OnSettingsRequested;
        Store.Changed += OnStoreChanged;
        owner.Closing += OnOwnerClosing;
        owner.Closed += OnOwnerClosed;
    }

    /// <summary>Opens the Settings window over the main window, or brings the open one forward.</summary>
    /// <returns>A task that completes when the pages of a new window have loaded. It never faults: a failure is traced.</returns>
    public async Task OpenAsync()
    {
        try
        {
            await _host.ShowAsync(_owner, _lifetime.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The head is closing.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Trace.TraceWarning($"Opening the Settings window failed ({exception.GetType().Name}).");
        }
    }

    /// <summary>Closes the Settings window, stops the sweeper and releases what the head owns for the Models page.</summary>
    /// <returns>A task that completes when the sweeper is stopped.</returns>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Detach();
        await _lifetime.CancelAsync().ConfigureAwait(true);
        _host.CloseForShutdown();
        if (_sweeper is not null)
        {
            await _sweeper.DisposeAsync().ConfigureAwait(true);
        }

        _owned?.Dispose();
        _lifetime.Dispose();
    }

    private void OnSettingsRequested(object? sender, EventArgs e) => _ = OpenAsync();

    private void OnOwnerClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!e.Cancel)
        {
            _host.CloseForShutdown();
        }
    }

    private void OnOwnerClosed(object? sender, EventArgs e) => Detach();

    private void Detach()
    {
        if (_main is { } main)
        {
            main.SettingsRequested -= OnSettingsRequested;
            main.TerminalSettingsProvider = null;
            _main = null;
        }

        Store.Changed -= OnStoreChanged;
        if (_owner is { } owner)
        {
            owner.Closing -= OnOwnerClosing;
            owner.Closed -= OnOwnerClosed;
            _owner = null;
        }
    }

    // A change was written: whatever differs from what is in force is put in force now.
    private void OnStoreChanged(object? sender, EventArgs e)
    {
        var now = ReadOrDefaults(Store);
        var before = _applied;
        _applied = now;
        if (now.Motion != before.Motion)
        {
            _main?.ApplyMotion(now.Motion);
            _owner?.Classes.Set(MainView.ReducedMotionClass, now.Motion == MotionPreference.Reduced);
        }

        if (now.TerminalAutoRun != before.TerminalAutoRun)
        {
            _main?.ApplyTerminalSettings();
        }

        if (now.Runtime != before.Runtime)
        {
            _sweeper?.Apply(now.Runtime);
        }
    }
}
