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
/// shared control styles, and opens the main window on a classic desktop lifetime. It receives the approval bridge and the
/// run input the main window routes through.
/// </summary>
internal partial class App : Application
{
    private readonly IToolApprovalBridge _approvalBridge;
    private readonly IRunInput _runInput;

    /// <summary>
    /// Creates the application with the production defaults. No approval service and no run input are connected in this build,
    /// so every decision and steer fails visibly and nothing is allowed.
    /// </summary>
    public App()
        : this(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput())
    {
    }

    /// <summary>Creates the application with the approval bridge and run input its main window uses.</summary>
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
    /// Creates the application over a composed engine. Its approval bridge is the engine's, so each decision settles the request
    /// the engine parked. The run input stays unconnected until a run input is composed.
    /// </summary>
    /// <param name="engine">The composed engine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
    public App(EngineComposition engine)
        : this(ApprovalBridgeOf(engine), new UnconnectedRunInput())
    {
    }

    /// <summary>
    /// Creates the application over an engine composed on first use. The engine is composed on the first tool-approval decision,
    /// so launching the application composes nothing and starts no process.
    /// </summary>
    /// <param name="engine">The lazily composed engine.</param>
    /// <exception cref="ArgumentNullException"><paramref name="engine"/> is null.</exception>
    public App(Lazy<EngineComposition> engine)
        : this(new ComposedApprovalBridge(engine), new UnconnectedRunInput())
    {
    }

    private static IToolApprovalBridge ApprovalBridgeOf(EngineComposition engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return engine.ApprovalBridge;
    }

    /// <summary>Loads the application XAML.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>Opens the main window once the desktop lifetime is available. Headless tests create their own windows.</summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(_approvalBridge, _runInput, MotionPreference.System);
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
}
