using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using OmpOS.Native;
using OmpOS.Native.Engine;
using OmpOS.Native.Views.Sessions;

namespace OmpOS.Android;

/// <summary>
/// The Avalonia application of the Android head. It loads the application XAML, which merges the token dictionary and the shared
/// control styles, and shows the shared main view in the single-view lifetime of the activity. No engine and no approval
/// service are connected in this build, so every decision fails visibly and nothing is allowed. Sending is refused.
/// </summary>
public sealed partial class OmpOSAndroidApp : Avalonia.Application
{
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
            singleView.MainView = new MainView(UnconnectedAgentPanel.Create(new UnconnectedToolApprovalBridge(), workspaceRoot, motion), workspaceRoot, motion);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
