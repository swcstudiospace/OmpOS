using System.Globalization;
using System.Windows.Input;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Observable;

namespace OmpOS.Native.Views.Terminal;

/// <summary>
/// The state of the interactive omp terminal: starting, running, exited with a code, or unavailable with the text that tells
/// the user how to fix it. The model owns the omp session it started and disposes it when it stops, restarts, closes or is
/// disposed, so no omp child outlives it. It never throws a launch failure: a missing omp and a failed launch are states.
/// </summary>
/// <remarks>
/// <see cref="StartAsync"/>, <see cref="StopAsync"/> and the commands are called on the UI thread, and their continuations
/// return to it. <see cref="OutputReceived"/> is the one member raised on another thread: terminal output arrives on the
/// session's reader thread.
/// </remarks>
internal sealed class OmpTerminalViewModel : ObservableModel, IAsyncDisposable
{
    private const int DefaultColumns = 120;
    private const int DefaultRows = 40;

    private readonly IOmpTerminalStarter _starter;
    private readonly ForwardingSink _sink;
    private OmpTerminalState _state = OmpTerminalState.Idle;
    private OmpTerminalStarted? _started;
    private Action? _cancelStart;
    private int? _exitCode;
    private string _statusText = string.Empty;
    private string _fixText = string.Empty;
    private long _generation;
    private int _columns = DefaultColumns;
    private int _rows = DefaultRows;
    private bool _hasRun;
    private bool _disposed;

    /// <summary>Creates a terminal model that starts omp through <paramref name="starter"/>. Nothing starts until <see cref="StartAsync"/>.</summary>
    /// <param name="starter">Starts omp. The shell builds the request.</param>
    /// <exception cref="ArgumentNullException"><paramref name="starter"/> is null.</exception>
    public OmpTerminalViewModel(IOmpTerminalStarter starter)
    {
        ArgumentNullException.ThrowIfNull(starter);
        _starter = starter;
        _sink = new ForwardingSink(this);
        RestartCommand = new AgentPanelCommand(
            _ => StartAsync(),
            ReportCommandFault,
            _ => State is OmpTerminalState.Exited or OmpTerminalState.Unavailable);
        CloseCommand = new AgentPanelCommand(_ => CloseAsync(), ReportCommandFault);
    }

    /// <summary>Raised with each piece of terminal output, on the thread the session reads it on. A listener must be thread-safe.</summary>
    public event EventHandler<OmpTerminalOutputEventArgs>? OutputReceived;

    /// <summary>
    /// Raised when the session the user types into changes: with the running session when omp started, and with null when it
    /// ended or was stopped. Raised on the UI thread.
    /// </summary>
    public event EventHandler<OmpTerminalSessionEventArgs>? SessionChanged;

    /// <summary>Raised on the UI thread before a start that follows an earlier run, so the view can clear the old screen.</summary>
    public event EventHandler? ScreenResetRequested;

    /// <summary>Raised on the UI thread when <see cref="CloseCommand"/> has stopped the session, so the shell can remove the terminal.</summary>
    public event EventHandler? CloseRequested;

    /// <summary>Gets what the terminal is doing.</summary>
    public OmpTerminalState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                NotifyPropertyChanged(nameof(CanRestart));
                ((AgentPanelCommand)RestartCommand).NotifyCanExecuteChanged();
            }
        }
    }

    /// <summary>Gets the exit code of omp while <see cref="State"/> is <see cref="OmpTerminalState.Exited"/>; null when it is unknown or omp has not exited.</summary>
    public int? ExitCode
    {
        get => _exitCode;
        private set => SetProperty(ref _exitCode, value);
    }

    /// <summary>Gets the one-line status for the live region: starting, running, "omp exited with code N", or the unavailable summary.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    /// <summary>
    /// Gets the text that explains why omp is unavailable and what to do about it, from the not-found guidance or the launch
    /// failure. It is empty in every other state.
    /// </summary>
    public string FixText
    {
        get => _fixText;
        private set => SetProperty(ref _fixText, value);
    }

    /// <summary>Gets a value indicating whether Restart is available: omp exited or could not be started.</summary>
    public bool CanRestart => State is OmpTerminalState.Exited or OmpTerminalState.Unavailable;

    /// <summary>Gets the running session, or null when omp is not running.</summary>
    public ITerminalSession? Session => _started?.Session;

    /// <summary>Gets the command that starts omp again. It can run when omp exited or was unavailable.</summary>
    public ICommand RestartCommand { get; }

    /// <summary>Gets the command that stops omp, disposes its session and raises <see cref="CloseRequested"/>.</summary>
    public ICommand CloseCommand { get; }

    /// <summary>
    /// Remembers the terminal size the next start uses. The view calls it whenever the terminal grid changes, so a Restart
    /// starts omp at the size the user sees.
    /// </summary>
    /// <param name="columns">The number of columns. Values below 1 are ignored.</param>
    /// <param name="rows">The number of rows. Values below 1 are ignored.</param>
    public void SetSize(int columns, int rows)
    {
        if (columns < 1 || rows < 1)
        {
            return;
        }

        _columns = columns;
        _rows = rows;
    }

    /// <summary>
    /// Starts omp, unless it is starting or running. The launch outcome becomes the state; the method does not throw it. A start
    /// that is stopped while it is launching disposes the session it produced.
    /// </summary>
    /// <returns>A task that completes when omp is running, or when the start ended in <see cref="OmpTerminalState.Unavailable"/>.</returns>
    public async Task StartAsync()
    {
        if (_disposed || State is OmpTerminalState.Starting or OmpTerminalState.Running)
        {
            return;
        }

        var generation = ++_generation;
        using var cancellation = new CancellationTokenSource();
        Action cancelThisStart = cancellation.Cancel;
        _cancelStart = cancelThisStart;
        try
        {
            if (_hasRun)
            {
                ScreenResetRequested?.Invoke(this, EventArgs.Empty);
            }

            _hasRun = true;
            ExitCode = null;
            FixText = string.Empty;
            StatusText = "Starting omp";
            State = OmpTerminalState.Starting;

            OmpTerminalLaunchResult result;
            try
            {
                result = await _starter.StartAsync(_columns, _rows, _sink, cancellation.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                if (generation == _generation)
                {
                    ShowStopped();
                }

                return;
            }
            catch (ArgumentException exception)
            {
                if (generation == _generation)
                {
                    ShowUnavailable("omp could not be started", $"omp could not be started: {exception.Message}");
                }

                return;
            }

            await ApplyAsync(result, generation).ConfigureAwait(true);
        }
        finally
        {
            if (ReferenceEquals(_cancelStart, cancelThisStart))
            {
                _cancelStart = null;
            }
        }
    }

    /// <summary>
    /// Stops omp: cancels a launch in progress, disposes the session so no omp child remains, and returns to
    /// <see cref="OmpTerminalState.Idle"/>. It does nothing when nothing is running.
    /// </summary>
    /// <returns>A task that completes when the session is disposed.</returns>
    public async Task StopAsync()
    {
        _generation++;
        CancelStart();
        await ReleaseSessionAsync().ConfigureAwait(true);
        ShowStopped();
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _generation++;
        CancelStart();
        await ReleaseSessionAsync().ConfigureAwait(true);
    }

    private async Task CloseAsync()
    {
        await StopAsync().ConfigureAwait(true);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task ApplyAsync(OmpTerminalLaunchResult result, long generation)
    {
        if (result is OmpTerminalStarted started)
        {
            if (generation != _generation || _disposed)
            {
                // The terminal was stopped while omp was starting. Nobody will use this session, so end it now.
                await started.DisposeAsync().ConfigureAwait(true);
                return;
            }

            _started = started;
            StatusText = "omp is running";
            State = OmpTerminalState.Running;
            SessionChanged?.Invoke(this, new OmpTerminalSessionEventArgs(started.Session));
            _ = WatchAsync(started, generation);
            return;
        }

        if (generation != _generation)
        {
            return;
        }

        switch (result)
        {
            case OmpTerminalNotFound notFound:
                ShowUnavailable("omp was not found", DescribeNotFound(notFound));
                break;
            case OmpTerminalLaunchFailed failed:
                ShowUnavailable(
                    "omp could not be started",
                    $"{failed.Reason} Check that the project folder exists and that omp starts from a terminal there, then choose Restart omp.");
                break;
            default:
                ShowUnavailable("omp could not be started", "omp could not be started. Choose Restart omp to try again.");
                break;
        }
    }

    /// <summary>
    /// Waits for omp to exit and shows the exit code. The wait never throws: the session being disposed by a stop, or its output
    /// failing, ends it quietly. The session is disposed once omp has exited, so its terminal is released at once.
    /// </summary>
    private async Task WatchAsync(OmpTerminalStarted started, long generation)
    {
        int? code;
        try
        {
            code = await started.Session.WaitForExitAsync(CancellationToken.None).ConfigureAwait(true);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException)
        {
            // The session's output failed, so the exit code is unknown. The terminal still ended.
            code = null;
        }

        if (generation != _generation || !ReferenceEquals(_started, started))
        {
            return;
        }

        _started = null;
        SessionChanged?.Invoke(this, new OmpTerminalSessionEventArgs(null));
        await started.DisposeAsync().ConfigureAwait(true);

        if (generation != _generation)
        {
            return;
        }

        ExitCode = code;
        StatusText = code is { } value
            ? string.Create(CultureInfo.InvariantCulture, $"omp exited with code {value}")
            : "omp exited";
        State = OmpTerminalState.Exited;
    }

    private async Task ReleaseSessionAsync()
    {
        var started = _started;
        if (started is null)
        {
            return;
        }

        _started = null;
        SessionChanged?.Invoke(this, new OmpTerminalSessionEventArgs(null));
        await started.DisposeAsync().ConfigureAwait(true);
    }

    private void CancelStart()
    {
        var cancel = _cancelStart;
        _cancelStart = null;
        try
        {
            cancel?.Invoke();
        }
        catch (ObjectDisposedException)
        {
            // The launch finished and released its token between the check and the cancel: nothing is left to cancel.
        }
    }

    private void ShowStopped()
    {
        ExitCode = null;
        FixText = string.Empty;
        StatusText = string.Empty;
        State = OmpTerminalState.Idle;
    }

    private void ShowUnavailable(string summary, string fixText)
    {
        ExitCode = null;
        FixText = fixText;
        StatusText = summary;
        State = OmpTerminalState.Unavailable;
    }

    private void ReportCommandFault(Exception fault) =>
        ShowUnavailable("omp could not be started", $"omp could not be started: {fault.Message}");

    private void RaiseOutput(string text) => OutputReceived?.Invoke(this, new OmpTerminalOutputEventArgs(text));

    private static string DescribeNotFound(OmpTerminalNotFound notFound)
    {
        var text = $"omp was not found. {notFound.InstallGuidance}";
        return notFound.DetectedPaths.Count == 0
            ? text
            : $"{text}{Environment.NewLine}Looked for omp in:{Environment.NewLine}{string.Join(Environment.NewLine, notFound.DetectedPaths)}";
    }

    /// <summary>Hands terminal output to <see cref="OmpTerminalViewModel.OutputReceived"/>.</summary>
    private sealed class ForwardingSink(OmpTerminalViewModel model) : IProcessOutputSink
    {
        /// <inheritdoc/>
        public void Write(string text) => model.RaiseOutput(text);
    }
}
