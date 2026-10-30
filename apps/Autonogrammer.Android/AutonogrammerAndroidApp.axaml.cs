using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Autonogrammer.Native;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Android;

/// <summary>
/// The Avalonia application of the Android head. It loads the application XAML, which merges the token dictionary and the shared
/// control styles, and shows the shared main view in the single-view lifetime of the activity. No engine and no approval
/// service are connected in this build, so every decision fails visibly and nothing is allowed. Sending is refused.
/// </summary>
public sealed partial class AutonogrammerAndroidApp : Avalonia.Application
{
    // The unconnected panel is owned by the application for the activity's lifetime. The single-view lifetime has no shutdown
    // hook, and the panel holds no engine, so the process releases it.
    private AgentPanelViewModel? _panel;

    /// <summary>Loads the application XAML.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Shows the main view once the single-view lifetime is available. The workspace is the app's private files folder, which
    /// is created before the view is built. The view is not rooted in the current directory, which is not an Android folder.
    /// The view is built with the motion preference read from the Android "Remove animations" setting, so the application
    /// styles can remove the shared transitions.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var workspaceRoot = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            Directory.CreateDirectory(workspaceRoot);
            var motion = AndroidMotion.Read();
            _panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot, motion);
            singleView.MainView = new MainView(_panel, workspaceRoot, motion);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
