using Avalonia.Controls;
using OmpOS.Native.Composition;
using OmpOS.Native.Settings;
using OmpOS.Native.Views.Branding;
using OmpOS.Native.Views.Shell;
using OmpOS.Presentation;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;

namespace OmpOS.Native;

internal partial class MainWindow : Window
{
    private const string ReducedMotionClass = MainView.ReducedMotionClass;

    private readonly MainView _main;

    public MainWindow(AgentPanelViewModel panel, MotionPreference motion, string? workspaceRoot = null, KanbanBoardModel? board = null, HeadServices? services = null)
    {
        ArgumentNullException.ThrowIfNull(panel);

        InitializeComponent();
        AppIcon.Apply(this);
        _main = new MainView(panel, workspaceRoot, motion, mcpHost: null, board, ShellServicesOf(services));
        Content = _main;
        Classes.Set(ReducedMotionClass, IsReducedMotion(motion));
    }

    // The views library cannot see the head's composition, so its members go over as the seams the views know. A window built without services keeps only the features that need none.
    private static ShellServices ShellServicesOf(HeadServices? services) => services is null
        ? new ShellServices()
        : new ShellServices
        {
            Hooks = services.Hooks,
            RemoteEnvironments = services.RemoteEnvironments,
            NewSessionTarget = services.NewSessionTarget,
            Checkpoints = services.CheckpointBackend,
            Worktrees = services.Worktrees,
            BestOfN = services.BestOfN,
            CreateModeSkills = services.CreateModeSkills,
            About = services.About,
            UrlLauncher = services.UrlLauncher,
            Display = services.Display,
            WorkspaceSets = services.WorkspaceSets,
            WorkspacePicker = new WorkspaceSetPickerBackend(),
            Cloud = services.Cloud,
        };

    private static bool IsReducedMotion(MotionPreference motion) => motion == MotionPreference.Reduced;

    protected override void OnClosed(EventArgs e)
    {
        _main.Close();
        base.OnClosed(e);
    }
}
