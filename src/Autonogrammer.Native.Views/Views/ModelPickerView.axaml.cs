using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Composer;

namespace Autonogrammer.Native.Views;

/// <summary>
/// The model chip of the composer footer and the flyout behind it. The chip shows the model of the bound <see cref="ModelPickerModel"/> and
/// is hidden while the model has nothing to offer. The flyout holds a search box and the models grouped by provider. Focus stays in the
/// search box: Up and Down move the highlighted model, Enter switches to it, and Escape closes the flyout and asks the composer to take
/// focus back (<see cref="FocusReturnRequested"/>). The popup has no animation, so reduced motion changes nothing here.
/// </summary>
internal partial class ModelPickerView : UserControl
{
    /// <summary>How many models Page Up and Page Down move.</summary>
    private const int PageSize = 6;

    private readonly ModelWatcher<ModelPickerModel> _watch;
    private bool _syncingSearch;
    private bool _returnFocusOnClose;

    /// <summary>Creates the chip with no model. It stays hidden until a model is bound.</summary>
    public ModelPickerView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<ModelPickerModel>(Refresh);
        SearchBox.AddHandler(KeyDownEvent, OnSearchKeyDown, RoutingStrategies.Tunnel);
        SearchBox.PropertyChanged += OnSearchPropertyChanged;
        ModelFlyout.Opened += OnFlyoutOpened;
        ModelFlyout.Closed += OnFlyoutClosed;
        ModelList.Tapped += OnListTapped;
        Refresh();
    }

    /// <summary>Raised when the list closed by Escape or by a switch, so the composer can move focus back to the prompt.</summary>
    public event EventHandler? FocusReturnRequested;

    private Flyout ModelFlyout => (Flyout)ChipButton.Flyout!;

    /// <summary>Gets the chip button that opens the list.</summary>
    internal Button Chip => ChipButton;

    /// <summary>Gets the text of the chip.</summary>
    internal TextBlock ChipLabel => LabelText;

    /// <summary>Gets the search box of the flyout.</summary>
    internal TextBox Search => SearchBox;

    /// <summary>Gets the list of provider headings and models of the flyout.</summary>
    internal ListBox Models => ModelList;

    /// <summary>Gets the status line of the flyout, a polite live region.</summary>
    internal TextBlock Status => StatusText;

    /// <summary>Gets a value indicating whether the flyout is open.</summary>
    internal bool IsOpen => ModelFlyout.IsOpen;

    /// <summary>Gets the model the chip draws, or null.</summary>
    internal ModelPickerModel? Picker => DataContext as ModelPickerModel;

    /// <summary>Opens the flyout, which takes focus into the search box. Does nothing while the chip is disabled.</summary>
    /// <returns>True when the flyout was opened.</returns>
    internal bool Open()
    {
        if (!ChipButton.IsEffectivelyEnabled)
        {
            return false;
        }

        ModelFlyout.ShowAt(ChipButton);
        return true;
    }

    /// <summary>Closes the flyout.</summary>
    internal void Close() => ModelFlyout.Hide();

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _watch.Set(Picker);
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
            _watch.Set(Picker);
        }

        Refresh();
    }

    private void Refresh()
    {
        var picker = Picker;
        IsVisible = picker is { IsVisible: true };
        ChipButton.IsEnabled = picker is { IsEnabled: true };
        var label = picker?.Label ?? ModelPickerModel.PendingLabel;
        LabelText.Text = label;
        AutomationProperties.SetName(ChipButton, $"Model: {label}");
        AutomationProperties.SetHelpText(ChipButton, picker?.ToolTipText);
        ToolTip.SetTip(ChipButton, picker?.ToolTipText);

        if (!ReferenceEquals(ModelList.ItemsSource, picker?.Rows))
        {
            ModelList.ItemsSource = picker?.Rows;
        }

        // The selection of the list is the keyboard cursor of the model: the app's ListBoxItem:selected style draws it.
        if (!ReferenceEquals(ModelList.SelectedItem, picker?.HighlightedRow))
        {
            ModelList.SelectedItem = picker?.HighlightedRow;
            if (picker?.HighlightedRow is { } highlighted)
            {
                ModelList.ScrollIntoView(highlighted);
            }
        }

        RefreshStatus(picker);
    }

    private void RefreshStatus(ModelPickerModel? picker)
    {
        var tone = picker is { HasNotice: true };
        StatusText.Text = picker switch
        {
            null => string.Empty,
            { IsLoading: true } => "Loading models.",
            { HasNotice: true, Notice: var notice } => notice,
            { HasNoMatches: true } => "No models match.",
            { HighlightedRow.Choice: { } choice } => choice.Value,
            _ => string.Empty,
        };
        SetClass(StatusText, "muted", !tone);
        SetClass(StatusText, "error", tone);
    }

    private static void SetClass(StyledElement element, string name, bool on)
    {
        if (on)
        {
            if (!element.Classes.Contains(name))
            {
                element.Classes.Add(name);
            }
        }
        else
        {
            element.Classes.Remove(name);
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        _syncingSearch = true;
        SearchBox.Text = string.Empty;
        _syncingSearch = false;
        if (Picker is { } picker)
        {
            picker.FilterText = string.Empty;

            // The first open starts the engine session when the models are not known yet. A failure becomes the notice of the list.
            _ = picker.EnsureLoadedAsync(CancellationToken.None);
        }

        SearchBox.FocusWhenReady();
        Refresh();
    }

    private void OnSearchPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (!_syncingSearch && e.Property == TextBox.TextProperty && Picker is { } picker)
        {
            picker.FilterText = SearchBox.Text ?? string.Empty;
        }
    }

    private void OnContainerPrepared(object? sender, ContainerPreparedEventArgs e)
    {
        // A provider heading names a group: the pointer passes through it, so it is never selected.
        e.Container.IsHitTestVisible = e.Container.DataContext is not ModelPickerRow { IsHeading: true };
        e.Container.Focusable = false;
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None || Picker is not { } picker)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                e.Handled = true;
                picker.MoveHighlight(1);
                break;
            case Key.Up:
                e.Handled = true;
                picker.MoveHighlight(-1);
                break;
            case Key.PageDown:
                e.Handled = true;
                picker.MoveHighlight(PageSize);
                break;
            case Key.PageUp:
                e.Handled = true;
                picker.MoveHighlight(-PageSize);
                break;
            case Key.Enter:
                e.Handled = true;
                _ = ConfirmAsync(picker);
                break;
            case Key.Escape:
                e.Handled = true;
                CloseAndReturnFocus();
                break;
            default:
                break;
        }
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (Picker is not { } picker
            || e.Source is not Visual source
            || source.FindAncestorOfType<ListBoxItem>()?.DataContext is not ModelPickerRow { IsHeading: false } row)
        {
            return;
        }

        picker.Highlight(row);
        _ = ConfirmAsync(picker);
    }

    /// <summary>Switches to the highlighted model. The list closes when the switch took, and stays open with the notice when it did not.</summary>
    private async Task ConfirmAsync(ModelPickerModel picker)
    {
        await picker.ConfirmHighlightedAsync().ConfigureAwait(true);
        if (!picker.HasNotice)
        {
            CloseAndReturnFocus();
        }
    }

    private void CloseAndReturnFocus()
    {
        if (ModelFlyout.IsOpen)
        {
            // The popup can move focus itself while it closes, so the request is repeated once it has closed.
            _returnFocusOnClose = true;
            ModelFlyout.Hide();
        }

        FocusReturnRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnFlyoutClosed(object? sender, EventArgs e)
    {
        if (_returnFocusOnClose)
        {
            _returnFocusOnClose = false;
            FocusReturnRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
