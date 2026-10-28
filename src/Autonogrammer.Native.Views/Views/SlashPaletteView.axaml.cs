using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Commands;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The slash palette of the composer: a list above the prompt of the commands the bound <see cref="SlashPaletteModel"/> lists. It is visible
/// while the model is open, shows at most <see cref="SlashPaletteModel.MaxVisibleRows"/> rows before the list scrolls, and keeps the highlighted
/// row equal to the model's selection. Focus never leaves the prompt: the composer forwards the arrow, Enter, Tab and Escape keys to the
/// model. A click on a row selects it and raises <see cref="EntryChosen"/>; the composer then completes the draft. The view runs no command.
/// </summary>
internal partial class SlashPaletteView : UserControl
{
    private readonly ModelWatcher<SlashPaletteModel> _watch;

    /// <summary>Creates the palette with no model. It stays hidden until a model that is open is bound.</summary>
    public SlashPaletteView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<SlashPaletteModel>(Refresh);
        CommandList.Tapped += OnListTapped;
        Refresh();
    }

    /// <summary>Raised when the user clicked a row. The model's selection is that row; the composer completes the draft with it.</summary>
    public event EventHandler? EntryChosen;

    /// <summary>Gets the model the palette draws, or null.</summary>
    internal SlashPaletteModel? Model => DataContext as SlashPaletteModel;

    /// <summary>Gets the list of commands.</summary>
    internal ListBox Rows => CommandList;

    /// <summary>Gets the live region that says how many commands match.</summary>
    internal TextBlock CountLine => CountText;

    /// <summary>Gets the line that says why no command is listed, or why the highlighted command cannot be chosen.</summary>
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

    /// <summary>Caps the list at the visible-row count: a row is one control height tall, so the list is that many control heights high at most.</summary>
    private void LimitRows()
    {
        if (this.TryFindResource("Autonogrammer.Size.ControlHeightSm", ActualThemeVariant, out var resource) && resource is double rowHeight)
        {
            CommandList.MaxHeight = rowHeight * SlashPaletteModel.MaxVisibleRows;
        }
    }

    private void Refresh()
    {
        var model = Model;
        IsVisible = model is { IsOpen: true };
        if (model is null)
        {
            CommandList.ItemsSource = null;
            CountText.Text = string.Empty;
            MessageText.IsVisible = false;
            return;
        }

        if (!ReferenceEquals(CommandList.ItemsSource, model.Entries))
        {
            CommandList.ItemsSource = model.Entries;
        }

        CommandList.IsVisible = model.Entries.Count > 0;
        CommandList.SelectedIndex = model.SelectedIndex;
        // The model raises Entries before SelectedIndex, so a redraw in between can see an index that the new list does not hold yet.
        if (model.SelectedIndex >= 0 && model.SelectedIndex < model.Entries.Count)
        {
            CommandList.ScrollIntoView(model.SelectedIndex);
        }

        CountText.Text = model.Announcement ?? string.Empty;
        var message = model.Notice ?? model.NoMatchMessage;
        MessageText.Text = message ?? string.Empty;
        MessageText.IsVisible = message is not null;
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (Model is not { } model || e.Source is not Visual source || source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is not { } item)
        {
            return;
        }

        var index = CommandList.IndexFromContainer(item);
        if (index < 0)
        {
            return;
        }

        model.Select(index);
        EntryChosen?.Invoke(this, EventArgs.Empty);
    }
}
