using Avalonia.Controls;
using OmpOS.Native.Engine;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Runs;

namespace OmpOS.Native;

/// <summary>
/// The one main window of the native head. It is the window shell around <see cref="MainView"/>: the window properties, the
/// reduced-motion class the shared styles read on the window, and the close of the main view when the window closes. It has no
/// dialog and no second window.
/// </summary>
internal partial class MainWindow : Window
{
    private const string ReducedMotionClass = "reduced-motion";

    private readonly MainView _main;
    private readonly IPromptChannel? _prompts;

    /// <summary>
    /// Creates the window with the production defaults: no approval service and no run input are connected in this build, so
    /// every decision and steer fails visibly and nothing is allowed.
    /// </summary>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    public MainWindow(MotionPreference motion)
        : this(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), motion)
    {
    }

    /// <summary>
    /// Creates the window with the approval bridge and the run input it routes decisions and messages through. The workspace is the
    /// current directory.
    /// </summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion)
        : this(approvalBridge, runInput, motion, Environment.CurrentDirectory)
    {
    }

    /// <summary>Creates the window with the approval bridge, the run input and the workspace folder its tools work in.</summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <param name="workspaceRoot">The workspace folder. It must exist.</param>
    /// <param name="engine">The lazily composed engine the window sends prompts through. Null refuses every send and stop.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="workspaceRoot"/> is null, empty or white space.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="workspaceRoot"/> does not exist.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion, string workspaceRoot, Lazy<EngineComposition>? engine = null)
    {
        InitializeComponent();

        _prompts = engine is null ? null : new EnginePromptChannel(engine);
        _main = new MainView(approvalBridge, runInput, workspaceRoot, _prompts);
        Content = _main;
        Classes.Set(ReducedMotionClass, IsReducedMotion(motion));
    }

    /// <summary>
    /// Resolves a motion setting to the reduced-motion flag. <see cref="MotionPreference.System"/> resolves to full
    /// motion, because Avalonia 12.1.4 exposes no operating-system reader (D-20-08, OQ-34).
    /// </summary>
    private static bool IsReducedMotion(MotionPreference motion) => motion == MotionPreference.Reduced;

    /// <summary>Closes the main view when the window closes, so its models and the composed send path are released.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _main.Close();
        base.OnClosed(e);
    }
}
