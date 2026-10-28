using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Mentions;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The mention picker of the composer: a list above the prompt of the rows the bound <see cref="MentionPickerModel"/> lists. It is visible while the model is open and has
/// something to say (rows or a message), shows at most eight rows before the list scrolls, and keeps the highlighted row equal to the model's selection. Focus never leaves
/// the prompt: the composer forwards the arrow, Enter, Tab and Escape keys to the composer view model. A click on a row selects it and raises <see cref="RowChosen"/>; the
/// composer then attaches it. The view attaches nothing itself.
/// </summary>
internal partial class MentionPickerView : UserControl
{
    private const int VisibleRows = 8;

    private readonly ModelWatcher<MentionPickerModel> _watch;

    /// <summary>Creates the picker with no model. It stays hidden until a model that is open is bound.</summary>
    public MentionPickerView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<MentionPickerModel>(Refresh);
        RowList.Tapped += OnListTapped;
        Refresh();
    }

    /// <summary>Raised when the user clicked a row. The model's selection is that row; the composer attaches it.</summary>
    public event EventHandler? RowChosen;

    /// <summary>Gets the model the picker draws, or null.</summary>
    internal MentionPickerModel? Model => DataContext as MentionPickerModel;

    /// <summary>Gets the list of rows.</summary>
    internal ListBox Rows => RowList;

    /// <summary>Gets the live region that says how many items match.</summary>
    internal TextBlock CountLine => CountText;

    /// <summary>Gets the line that says why nothing is listed, a failure, or the confirmation question.</summary>
    internal TextBlock Message => MessageText;

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watch.Set(Model);
        LimitRows();
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

    /// <summary>Caps the list: a row is one target tall, and a group title adds a little, so the list is the visible-row count of targets plus two titles high at most.</summary>
    private void LimitRows()
    {
        if (this.TryFindResource("Autonogrammer.Size.TargetMin", ActualThemeVariant, out var resource) && resource is double rowHeight)
        {
            RowList.MaxHeight = (rowHeight * VisibleRows) + (rowHeight * 0.75 * 2);
        }
    }

    private void Refresh()
    {
        var model = Model;
        IsVisible = model is { IsOpen: true } && (model.Rows.Count > 0 || model.Message is not null);
        if (model is null)
        {
            RowList.ItemsSource = null;
            CountText.Text = string.Empty;
            MessageText.IsVisible = false;
            return;
        }

        if (!ReferenceEquals(RowList.ItemsSource, model.Rows))
        {
            RowList.ItemsSource = model.Rows;
        }

        RowList.IsVisible = model.Rows.Count > 0;
        RowList.SelectedIndex = model.SelectedIndex;
        // The model raises Rows before SelectedIndex, so a redraw in between can see an index that the new list does not hold yet.
        if (model.SelectedIndex >= 0 && model.SelectedIndex < model.Rows.Count)
        {
            RowList.ScrollIntoView(model.SelectedIndex);
        }

        CountText.Text = model.Announcement ?? string.Empty;
        MessageText.Text = model.Message ?? string.Empty;
        MessageText.IsVisible = model.Message is not null;
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (Model is not { } model || e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } item)
        {
            return;
        }

        var index = RowList.IndexFromContainer(item);
        if (index < 0 || !model.Select(index))
        {
            return;
        }

        RowChosen?.Invoke(this, EventArgs.Empty);
    }
}
