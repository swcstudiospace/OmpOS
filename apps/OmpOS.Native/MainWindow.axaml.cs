using Avalonia.Controls;
using Avalonia.Interactivity;
using OmpOS.Native.Engine;
using OmpOS.Native.Views;
using OmpOS.Native.Views.RunControls;
using OmpOS.Presentation;
using OmpOS.Presentation.Approvals;
using OmpOS.Presentation.Components;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;

namespace OmpOS.Native;

/// <summary>
/// The one main window of the native head. It hosts the session list, the transcript, the status line, the run controls
/// (pending tool approvals) and the composer, and it binds the presentation view models the web head binds. It has no
/// dialog and no second window.
/// </summary>
internal partial class MainWindow : Window
{
    /// <summary>The status line text after a send the host refuses (UI-SPEC, Copywriting Contract).</summary>
    public const string RefusedSendStatus = "Message not sent. Sending is unavailable.";

    /// <summary>The status line text after Stop with no response running (UI-SPEC, Copywriting Contract).</summary>
    public const string NoResponseStatus = "No response is running.";

    /// <summary>The status line text prefix after a composer action faults. The fault's own message follows it.</summary>
    public const string FaultedStatus = "Something went wrong.";

    private const string ReducedMotionClass = "reduced-motion";

    /// <summary>
    /// Creates the window with the production defaults: no approval service and no run input are connected in this build, so
    /// every decision and steer fails visibly and nothing is allowed.
    /// </summary>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    public MainWindow(MotionPreference motion)
        : this(new UnconnectedToolApprovalBridge(), new UnconnectedRunInput(), motion)
    {
    }

    /// <summary>Creates the window with the approval bridge and the run input it routes decisions and messages through.</summary>
    /// <param name="approvalBridge">The bridge that records each tool-approval decision.</param>
    /// <param name="runInput">The input channel of the active run.</param>
    /// <param name="motion">The motion setting. Only <see cref="MotionPreference.Reduced"/> removes transitions.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public MainWindow(IToolApprovalBridge approvalBridge, IRunInput runInput, MotionPreference motion)
    {
        ArgumentNullException.ThrowIfNull(approvalBridge);
        ArgumentNullException.ThrowIfNull(runInput);

        InitializeComponent();

        // Phase 20 has no session transport. The shell starts with a loaded, empty list and a loaded, empty transcript,
        // which are the empty states the UI-SPEC copy describes.
        ConversationList.State = ConversationListState.Loaded;
        ConversationList.Conversations = Array.Empty<ConversationSummary>();

        Session = new SessionViewModel();
        Session.MarkHistoryLoaded();
        TranscriptHost.Content = ViewMapping.Create(Session);

        RunControls = new RunControlsViewModel(approvalBridge);
        RunControlsHost.Content = new RunControlsView { DataContext = RunControls };

        Runs = new RunInputScheduler(runInput);
        Composer = new ComposerViewModel(
            _ => RefuseSendAsync(),
            StopIdleAsync);
        var composerView = (ComposerView)ViewMapping.Create(Composer);
        composerView.Runs = Runs;
        composerView.WorkFaulted += OnComposerWorkFaulted;
        ComposerHost.Content = composerView;

        Classes.Set(ReducedMotionClass, IsReducedMotion(motion));
    }

    /// <summary>Gets the session view model the transcript binds.</summary>
    public SessionViewModel Session { get; }

    /// <summary>Gets the composer view model the prompt, send and stop controls bind.</summary>
    public ComposerViewModel Composer { get; }

    /// <summary>Gets the pending tool-approval requests and the coordinator that sends each decision.</summary>
    public RunControlsViewModel RunControls { get; }

    /// <summary>Gets the scheduler that routes steering messages and queued follow-ups to the active run.</summary>
    public RunInputScheduler Runs { get; }

    /// <summary>
    /// Resolves a motion setting to the reduced-motion flag. <see cref="MotionPreference.System"/> resolves to full
    /// motion, because Avalonia 12.1.4 exposes no operating-system reader (D-20-08, OQ-34).
    /// </summary>
    private static bool IsReducedMotion(MotionPreference motion) => motion == MotionPreference.Reduced;

    private Task<bool> RefuseSendAsync()
    {
        SetStatus(RefusedSendStatus);
        return Task.FromResult(false);
    }

    private Task StopIdleAsync()
    {
        SetStatus(NoResponseStatus);
        return Task.CompletedTask;
    }

    private void SetStatus(string text)
    {
        StatusLine.Text = text;
        StatusLine.IsVisible = text.Length > 0;
    }

    private void OnComposerWorkFaulted(object? sender, ComposerWorkFaultedEventArgs e) =>
        SetStatus($"{FaultedStatus} {e.Exception.GetBaseException().Message}");

    private void OnSkipLinkClick(object? sender, RoutedEventArgs e) => (TranscriptHost.Content as SessionView)?.FocusTranscript();
}
