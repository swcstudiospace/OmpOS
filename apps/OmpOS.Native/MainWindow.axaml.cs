using Avalonia.Controls;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native;

/// <summary>
/// The one main window of the native head. It is the window shell around <see cref="MainView"/>: the window properties, the
/// reduced-motion class the shared styles read on the window, and the close of the main view when the window closes. It has no
/// dialog and no second window. The panel it shows, and the engines behind it, belong to the head that created them: closing the
/// window does not stop an engine, the head's disposal does.
/// </summary>
internal partial class MainWindow : Window
{
    private const string ReducedMotionClass = MainView.ReducedMotionClass;

    private readonly MainView _main;

    /// <summary>Creates the window over the Agent Panel of a head. The workspace is the current directory unless one is given.</summary>
    /// <param name="panel">The panel the main view shows.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <param name="workspaceRoot">The workspace folder the tools work in, or null for the current directory. It must exist.</param>
    /// <exception cref="ArgumentNullException"><paramref name="panel"/> is null.</exception>
    /// <exception cref="DirectoryNotFoundException"><paramref name="workspaceRoot"/> does not exist.</exception>
    public MainWindow(AgentPanelViewModel panel, MotionPreference motion, string? workspaceRoot = null)
    {
        ArgumentNullException.ThrowIfNull(panel);

        InitializeComponent();
        _main = new MainView(panel, workspaceRoot, motion);
        Content = _main;
        Classes.Set(ReducedMotionClass, IsReducedMotion(motion));
    }

    /// <summary>
    /// Resolves a motion setting to the reduced-motion flag. <see cref="MotionPreference.System"/> resolves to full
    /// motion, because Avalonia 12.1.4 exposes no operating-system reader (D-20-08, OQ-34).
    /// </summary>
    private static bool IsReducedMotion(MotionPreference motion) => motion == MotionPreference.Reduced;

    /// <summary>Closes the main view when the window closes, so its models are released.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _main.Close();
        base.OnClosed(e);
    }
}
