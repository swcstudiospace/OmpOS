using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views;

/// <summary>
/// The conversation search overlay: a query box over the results of the bound <see cref="ConversationSearchModel"/>. It is visible while the model is open and takes the focus
/// into the query box when it opens. Focus stays in the box: Up and Down move the highlight, Enter opens the highlighted conversation, Escape closes the search. A click on a result opens it.
/// The host mounts the view in the rail or the shell and answers <see cref="SessionChosen"/> (open that session) and <see cref="Dismissed"/> (return the focus).
/// </summary>
internal partial class ConversationSearchView : UserControl
{
    private readonly ModelWatcher<ConversationSearchModel> _watch;
    private bool _wasOpen;
    private bool _syncing;

    /// <summary>Creates the overlay with no model. It stays hidden until a model that is open is bound.</summary>
    public ConversationSearchView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<ConversationSearchModel>(Refresh);
        QueryInput.PropertyChanged += OnQueryPropertyChanged;
        QueryInput.AddHandler(KeyDownEvent, OnQueryKeyDown, RoutingStrategies.Tunnel);
        ResultList.Tapped += OnListTapped;
        Refresh();
    }

    /// <summary>Raised when the user opened a conversation. The argument names the session.</summary>
    public event EventHandler<SessionSelectionChangedEventArgs>? SessionChosen;

    /// <summary>Raised when the user closed the search with Escape, so the host can return the focus to where it was.</summary>
    public event EventHandler? Dismissed;

    /// <summary>Gets the model the overlay draws, or null.</summary>
    internal ConversationSearchModel? Model => DataContext as ConversationSearchModel;

    /// <summary>Gets the query box.</summary>
    internal TextBox Query => QueryInput;

    /// <summary>Gets the list of results.</summary>
    internal ListBox Results => ResultList;

    /// <summary>Gets the line shown instead of the list: the prompt or the no-match sentence.</summary>
    internal TextBlock Empty => EmptyText;

    /// <summary>Gets the live region that says how many conversations match.</summary>
    internal TextBlock CountLine => CountText;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watch.Set(Model);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _watch.Set(null);
        base.OnDetachedFromVisualTree(e);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (VisualRoot is not null)
        {
            _watch.Set(Model);
        }

        Refresh();
    }

    private void Refresh()
    {
        var model = Model;
        var open = model is { IsOpen: true };
        IsVisible = open;
        if (model is null)
        {
            ResultList.ItemsSource = null;
            _wasOpen = false;
            return;
        }

        _syncing = true;
        try
        {
            if (!string.Equals(QueryInput.Text ?? string.Empty, model.Query, StringComparison.Ordinal))
            {
                QueryInput.Text = model.Query;
            }

            if (!ReferenceEquals(ResultList.ItemsSource, model.Results))
            {
                ResultList.ItemsSource = model.Results;
            }

            ResultList.IsVisible = model.Results.Count > 0;
            ResultList.SelectedIndex = model.SelectedIndex;
            if (model.SelectedIndex >= 0 && model.SelectedIndex < model.Results.Count)
            {
                ResultList.ScrollIntoView(model.SelectedIndex);
            }

            EmptyText.Text = model.EmptyMessage ?? string.Empty;
            EmptyText.IsVisible = model.EmptyMessage is not null;
            CountText.Text = model.Announcement ?? string.Empty;
            CountText.IsVisible = model.Announcement is not null;
        }
        finally
        {
            _syncing = false;
        }

        if (open && !_wasOpen)
        {
            QueryInput.FocusWhenReady();
        }

        _wasOpen = open;
    }

    private void OnQueryPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_syncing && e.Property == TextBox.TextProperty && Model is { } model)
        {
            model.SetQuery(QueryInput.Text ?? string.Empty);
        }
    }

    private void OnQueryKeyDown(object? sender, KeyEventArgs e)
    {
        if (Model is not { } model || e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                model.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                model.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                e.Handled = true;
                Choose(model);
                break;
            case Key.Escape:
                e.Handled = true;
                model.Close();
                Dismissed?.Invoke(this, EventArgs.Empty);
                break;
            default:
                break;
        }
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (Model is not { } model || e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } item)
        {
            return;
        }

        var index = ResultList.IndexFromContainer(item);
        if (index >= 0 && model.Select(index))
        {
            Choose(model);
        }
    }

    private void Choose(ConversationSearchModel model)
    {
        if (model.Accept() is { } sessionId)
        {
            SessionChosen?.Invoke(this, new SessionSelectionChangedEventArgs(sessionId));
        }
    }
}
