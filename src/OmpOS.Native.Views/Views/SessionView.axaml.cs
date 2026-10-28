using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Native.Views.Blocks;
using OmpOS.Native.Views.Hooks;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Views;

/// <summary>
/// The transcript region. It draws the bound <see cref="SessionViewModel"/>: the lifecycle copy, the folded blocks, the
/// streaming text, the throttled announcement in a polite live region, the lifecycle notice, and the stop, retry and
/// jump-to-latest controls. It follows the newest output until the user scrolls away from it, and records that pause on the model.
/// It redraws when the model raises <see cref="INotifyPropertyChanged.PropertyChanged"/> while it is shown; nothing calls it to refresh.
/// </summary>
internal partial class SessionView : UserControl
{
    /// <summary>The notice drawn while a turn awaits approval. The decision is made by policy, so the client offers no action.</summary>
    private const string ApprovalNotice = "Approval needed to continue. The decision is made by policy.";

    /// <summary>How far above the end of the transcript a viewport still counts as at the latest output.</summary>
    private const double LatestTolerance = 1;

    private readonly List<UiBlock> _rendered = [];
    private readonly List<(HookNoticeRow Row, HookNoticeRowView View)> _notices = [];
    private SessionViewModel? _session;
    private bool _shown;

    /// <summary>Creates the transcript with no data context.</summary>
    public SessionView()
    {
        InitializeComponent();
        TranscriptScroll.ScrollChanged += OnTranscriptScrollChanged;
        SessionStopButton.Click += OnStopClick;
        SessionRetryButton.Click += OnRetryClick;
        JumpToLatestButton.Click += OnJumpToLatestClick;
    }

    /// <summary>Raised on the UI thread when the user asks to review the hooks a notice names.</summary>
    public event EventHandler? HookReviewRequested;

    /// <summary>
    /// Shows a hook notice: under the tool-call row whose call id the notice names, or under the conversation when it names none or the call is not in the transcript. A notice stays with its call when the rows are rebuilt.
    /// </summary>
    /// <param name="notice">The notice row.</param>
    /// <exception cref="ArgumentNullException"><paramref name="notice"/> is null.</exception>
    internal void AddHookNotice(HookNoticeRow notice)
    {
        ArgumentNullException.ThrowIfNull(notice);

        var view = new HookNoticeRowView { DataContext = notice };
        view.ReviewRequested += (_, _) => HookReviewRequested?.Invoke(this, EventArgs.Empty);
        _notices.Add((notice, view));
        PlaceNotice(notice, view);
    }

    /// <summary>Gets the notice views the transcript holds, oldest first.</summary>
    internal IReadOnlyList<HookNoticeRowView> HookNotices => [.. _notices.Select(static pair => pair.View)];

    private void PlaceNotice(HookNoticeRow notice, HookNoticeRowView view)
    {
        var row = notice.ToolCallId is { } callId
            ? BlockHost.Children.OfType<ToolCallBlockView>().FirstOrDefault(candidate => string.Equals(candidate.CallId, callId, StringComparison.Ordinal))
            : null;
        if (row is not null)
        {
            row.AttachNotice(view);
            return;
        }

        if (view.Parent is Panel parent)
        {
            if (ReferenceEquals(parent, HookNoticeHost))
            {
                return;
            }

            parent.Children.Remove(view);
        }

        HookNoticeHost.Children.Add(view);
    }

    /// <summary>Raised on the UI thread when the user activates the stop control while a turn streams.</summary>
    public event EventHandler? StopRequested;

    /// <summary>Raised on the UI thread when the user activates the retry control after a recoverable failure.</summary>
    public event EventHandler? RetryRequested;

    /// <summary>Moves keyboard focus to the transcript region. The shell's skip link uses this.</summary>
    public void FocusTranscript() => TranscriptScroll.FocusWhenReady();

    /// <summary>
    /// Scrolls the transcript so the block at <paramref name="index"/> is in view. The index is a block index of the folded
    /// tree. The streaming tail (an index equal to the block count) is not a block control, so it and any other index
    /// outside the drawn blocks do nothing.
    /// </summary>
    /// <param name="index">The block index to bring into view.</param>
    public void ScrollToBlock(int index)
    {
        if (index < 0 || index >= BlockHost.Children.Count)
        {
            return;
        }

        BlockHost.Children[index].BringIntoView();
    }

    /// <summary>Binds the transcript to the session view model in the data context and draws its current state.</summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        BindSession(DataContext as SessionViewModel);
        RenderAll();
    }

    /// <summary>Subscribes to the model when the transcript is shown, and draws the state the model reached while it was hidden.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _shown = true;
        BindSession(_session);
        RenderAll();
    }

    /// <summary>Unsubscribes from the model when the transcript leaves the visual tree, so a hidden transcript keeps no subscription.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _shown = false;
        BindSession(_session);
    }

    private void RenderAll()
    {
        RenderState();
        RenderAnnouncement();
        RenderTree();
    }

    /// <summary>Points the transcript at a session model. The model notifies the transcript only while the transcript is shown.</summary>
    private void BindSession(SessionViewModel? session)
    {
        if (_session is not null)
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
        }

        _session = session;
        if (_session is not null && _shown)
        {
            _session.PropertyChanged += OnSessionPropertyChanged;
        }
    }

    private void OnSessionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The model may change off the UI thread; the controls are drawn only on the thread that owns them.
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnSessionPropertyChanged(sender, e));
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(SessionViewModel.State):
                RenderState();
                break;
            case nameof(SessionViewModel.Tree):
                RenderTree();
                break;
            case nameof(SessionViewModel.Announcement):
                RenderAnnouncement();
                break;
            case nameof(SessionViewModel.AutoScrollPaused):
                RenderControls();
                if (_session?.AutoScrollPaused == false)
                {
                    // Resuming follows the newest output again: the user jumped to it or scrolled back to its end.
                    TranscriptScroll.ScrollToEnd();
                }

                break;
        }
    }

    private void RenderState()
    {
        var (heading, body, isError) = _session is null
            ? (string.Empty, string.Empty, false)
            : Describe(_session.State);

        StateHeading.Text = heading;
        StateBody.Text = body;
        StateHeading.IsVisible = heading.Length > 0;
        StateBody.IsVisible = body.Length > 0;
        StateHeading.Classes.Set("error", isError);
        StateBody.Classes.Set("error", isError);
        StateBody.Classes.Set("muted", !isError);
        RenderControls();
        HideAnnouncementWhenItRepeatsTheHeading();
    }

    /// <summary>Draws the lifecycle notice and the affordances the current state offers.</summary>
    private void RenderControls()
    {
        var notice = _session is null ? string.Empty : NoticeFor(_session.State);
        StateNotice.Text = notice;
        StateNotice.IsVisible = notice.Length > 0;
        SessionStopButton.IsVisible = _session?.CanStop == true;
        SessionRetryButton.IsVisible = _session?.CanRetry == true;
        JumpToLatestButton.IsVisible = _session?.AutoScrollPaused == true;
    }

    private void RenderAnnouncement()
    {
        AnnouncementText.Text = _session?.Announcement ?? string.Empty;
        HideAnnouncementWhenItRepeatsTheHeading();
    }

    /// <summary>
    /// The live announcement says what the state heading says on an empty or failed transcript, so the same sentence would be drawn twice.
    /// The line stays in the layout tree (a screen reader still gets the live region) but takes no height and draws nothing then.
    /// </summary>
    private void HideAnnouncementWhenItRepeatsTheHeading()
    {
        var repeats = StateHeading.IsVisible && string.Equals(AnnouncementText.Text, StateHeading.Text, StringComparison.Ordinal);
        AnnouncementText.Opacity = repeats ? 0 : 1;
        AnnouncementText.MaxHeight = repeats ? 0 : double.PositiveInfinity;
    }

    private void RenderTree()
    {
        var tree = _session?.Tree ?? UiTree.Empty;
        ReconcileBlocks(tree.Blocks);

        StreamingText.Text = tree.StreamingText;
        StreamingText.IsVisible = tree.StreamingText.Length > 0;
    }

    /// <summary>
    /// Brings the block controls in line with the folded blocks. The fold keeps unchanged blocks by reference, so only the
    /// first changed block and everything after it are rebuilt; a streaming delta changes no block and rebuilds nothing.
    /// </summary>
    private void ReconcileBlocks(IReadOnlyList<UiBlock> blocks)
    {
        var kept = 0;
        while (kept < _rendered.Count && kept < blocks.Count && ReferenceEquals(_rendered[kept], blocks[kept]))
        {
            kept++;
        }

        if (kept == _rendered.Count && kept == blocks.Count)
        {
            return;
        }

        while (BlockHost.Children.Count > kept)
        {
            BlockHost.Children.RemoveAt(BlockHost.Children.Count - 1);
        }

        _rendered.RemoveRange(kept, _rendered.Count - kept);
        for (var index = kept; index < blocks.Count; index++)
        {
            BlockHost.Children.Add(ViewMapping.CreateBlock(blocks[index]));
            _rendered.Add(blocks[index]);
        }

        foreach (var (row, view) in _notices)
        {
            PlaceNotice(row, view);
        }
    }

    /// <summary>
    /// Follows the newest output while auto-scroll is not paused: when the transcript grows or shrinks, it moves to its end. Any other
    /// scroll is the user's, or a resize: leaving the end pauses following and returning to it resumes. The model changes only on a real
    /// transition, so each one is reported once.
    /// </summary>
    private void OnTranscriptScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (e.ExtentDelta != Vector.Zero && !_session.AutoScrollPaused)
        {
            TranscriptScroll.ScrollToEnd();
            return;
        }

        var maxOffset = Math.Max(0, TranscriptScroll.Extent.Height - TranscriptScroll.Viewport.Height);
        if (TranscriptScroll.Offset.Y >= maxOffset - LatestTolerance)
        {
            _session.ResumeAutoScroll();
        }
        else
        {
            _session.PauseAutoScroll();
        }
    }

    private void OnStopClick(object? sender, RoutedEventArgs e) => StopRequested?.Invoke(this, EventArgs.Empty);

    private void OnRetryClick(object? sender, RoutedEventArgs e) => RetryRequested?.Invoke(this, EventArgs.Empty);

    private void OnJumpToLatestClick(object? sender, RoutedEventArgs e) => _session?.ResumeAutoScroll();

    private static string NoticeFor(SessionViewState state) => state switch
    {
        SessionViewState.AwaitingApproval => ApprovalNotice,
        SessionViewState.Reconnecting => "Reconnecting…",
        SessionViewState.Offline => "You are offline. Showing the last known output.",
        _ => string.Empty,
    };

    private static (string Heading, string Body, bool IsError) Describe(SessionViewState state) => state switch
    {
        SessionViewState.LoadingHistory => ("Loading conversation history…", string.Empty, false),
        SessionViewState.Empty => ("No messages yet.", "Send a message to begin.", false),
        SessionViewState.FatalError or SessionViewState.RecoverableError =>
            ("Something went wrong.", "Close and reopen OmpOS to try again.", true),
        _ => (string.Empty, string.Empty, false),
    };
}
