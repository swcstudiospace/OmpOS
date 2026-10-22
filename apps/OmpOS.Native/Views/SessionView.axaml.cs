using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
using OmpOS.Contracts.Ui;
using OmpOS.GenUi;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Views;

/// <summary>
/// The transcript region. It draws the bound <see cref="SessionViewModel"/>: the lifecycle copy, the folded blocks, the
/// streaming text, and the throttled announcement in a polite live region. It redraws when the model raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/>; nothing calls it to refresh.
/// </summary>
internal partial class SessionView : UserControl
{
    private readonly List<UiBlock> _rendered = [];
    private SessionViewModel? _session;

    /// <summary>Creates the transcript with no data context.</summary>
    public SessionView()
    {
        InitializeComponent();
    }

    /// <summary>Moves keyboard focus to the transcript region. The shell's skip link uses this.</summary>
    public void FocusTranscript() => _ = TranscriptScroll.Focus();

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

        if (_session is not null)
        {
            _session.PropertyChanged -= OnSessionPropertyChanged;
        }

        _session = DataContext as SessionViewModel;
        if (_session is not null)
        {
            _session.PropertyChanged += OnSessionPropertyChanged;
        }

        RenderState();
        RenderAnnouncement();
        RenderTree();
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
    }

    private void RenderAnnouncement() => AnnouncementText.Text = _session?.Announcement ?? string.Empty;

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
    }

    private static (string Heading, string Body, bool IsError) Describe(SessionViewState state) => state switch
    {
        SessionViewState.LoadingHistory => ("Loading conversation history…", string.Empty, false),
        SessionViewState.Empty => ("No messages yet.", "Send a message to begin.", false),
        SessionViewState.FatalError or SessionViewState.RecoverableError =>
            ("Something went wrong.", "Close and reopen OmpOS to try again.", true),
        _ => (string.Empty, string.Empty, false),
    };
}
