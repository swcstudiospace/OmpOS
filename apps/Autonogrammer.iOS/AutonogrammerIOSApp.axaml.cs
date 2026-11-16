using Autonogrammer.Native;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Views.Sessions;
using Autonogrammer.Presentation;
using Autonogrammer.Presentation.AgentPanel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using UIKit;

namespace Autonogrammer.iOS;

/// <summary>
/// The Avalonia application of the iOS head. It loads the application XAML, which merges the token dictionary and the shared
/// control styles, and shows the shared main view in the single-view lifetime of the scene. No engine and no approval service are
/// connected in this build, so every decision fails visibly and nothing is allowed. Sending is refused.
/// </summary>
public sealed partial class AutonogrammerIOSApp : Avalonia.Application
{
    // The unconnected panel is owned by the application for the scene's lifetime. The panel holds no engine, so the process releases it.
    private AgentPanelViewModel? _panel;

    /// <summary>Loads the application XAML.</summary>
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Shows the main view once the single-view lifetime is available. The workspace is the app's Personal folder, which is created
    /// before the view is built. The view is built with the motion preference read from UIKit's Reduce Motion setting, so the
    /// application styles can remove the shared transitions.
    /// </summary>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            var workspaceRoot = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
            Directory.CreateDirectory(workspaceRoot);
            var motion = ReadMotionPreference();
            _panel = UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot, motion);
            singleView.MainView = new MainView(_panel, workspaceRoot, motion);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Reads the iOS Reduce Motion setting. The setting is on when the user asked for less motion, and the shared view then removes
    /// its non-essential transitions.
    /// </summary>
    /// <returns><see cref="MotionPreference.Reduced"/> when Reduce Motion is on, otherwise <see cref="MotionPreference.Full"/>.</returns>
    private static MotionPreference ReadMotionPreference() =>
        UIAccessibility.IsReduceMotionEnabled ? MotionPreference.Reduced : MotionPreference.Full;
}
