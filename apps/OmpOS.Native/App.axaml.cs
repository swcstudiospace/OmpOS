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
}
