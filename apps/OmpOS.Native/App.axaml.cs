using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OmpOS.Native.Engine;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native;

/// <summary>
/// The Avalonia application root. It loads the application XAML, which merges the token dictionary and the
/// shared control styles, and opens the main window on a classic desktop lifetime. It receives the approval bridge, the run input
/// and, when an engine is composed, the lazily composed engine the main window sends prompts through.
/// </summary>
internal partial class App : Application
{
    private readonly IToolApprovalBridge _approvalBridge;
    private readonly IRunInput _runInput;
    private readonly Lazy<EngineComposition>? _engine;

    /// <summary>
    /// Creates the application with the production defaults. No approval service and no run input are connected in this build,
    /// so every decision and steer fails visibly and nothing is allowed.
    /// </summary>
    public App()
        : this(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput())
    {
    }

    /// <summary>Creates the application with the approval bridge and run input its main window uses. Sending is refused.</summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public App(IToolApprovalBridge approvalBridge, IRunInput runInput)
    {
        ArgumentNullException.ThrowIfNull(approvalBridge);
        ArgumentNullException.ThrowIfNull(runInput);

        _approvalBridge = approvalBridge;
        _runInput = runInput;
    }

    /// <summary>
    /// Creates the application over an engine composed on first use. The engine is composed on the first tool-approval decision or
    /// the first send, so launching the application composes nothing and starts no process. Decisions and sends share that engine.
    /// </summary>
    /// <param name="engine">The lazily composed engine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
    public App(Lazy<EngineComposition> engine)
        : this(new ComposedApprovalBridge(engine), new EngineRunInput())
    {
        _engine = engine;
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

    /// <summary>Opens the main window once the desktop lifetime is available. Headless tests create their own windows.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_approvalBridge, _runInput, MotionPreference.System, Environment.CurrentDirectory, _engine);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// The approval bridge of an engine composed on first use. Each decision composes the engine if it is not composed yet, then
    /// settles the request through the engine's own bridge.
    /// </summary>
    internal sealed class ComposedApprovalBridge : IToolApprovalBridge
    {
        private readonly Lazy<EngineComposition> _engine;

        /// <summary>Creates the bridge over a lazily composed engine.</summary>
        /// <param name="engine">The lazily composed engine.</param>
        /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
        public ComposedApprovalBridge(Lazy<EngineComposition> engine)
        {
            ArgumentNullException.ThrowIfNull(engine);
            _engine = engine;
        }

        /// <inheritdoc/>
        public async Task<ToolApprovalOutcome> DecideAsync(string sessionId, string requestId, ToolApprovalDecision decision, CancellationToken cancellationToken) =>
            await _engine.Value.ApprovalBridge.DecideAsync(sessionId, requestId, decision, cancellationToken).ConfigureAwait(false);
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
