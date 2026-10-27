using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.Composer;

namespace OmpOS.Native.Views;

/// <summary>
/// A chip of the composer footer for one option the engine owns (its mode or its thinking level), and the short list behind it. The chip
/// shows the value of the bound <see cref="OptionChipModel"/> and is hidden while the model is null or offers no values. The list names
/// each value the engine offers and marks the one in use; a click or Enter on a value asks the model to change it. Escape closes the list
/// and asks the composer to take focus back (<see cref="FocusReturnRequested"/>). The popup has no animation, so reduced motion changes
/// nothing here.
/// </summary>
internal partial class OptionChipView : UserControl
{
    private readonly ModelWatcher<OptionChipModel> _watch;
    private bool _returnFocusOnClose;
    private IReadOnlyList<OptionChoice>? _rowsFor;
    private string? _rowsCurrent;

    /// <summary>Creates the chip with no model. It stays hidden until a model is bound.</summary>
    public OptionChipView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<OptionChipModel>(Refresh);
        ChoiceList.Tapped += OnListTapped;
        ChoiceList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        ChoiceFlyout.Opened += OnFlyoutOpened;
        ChoiceFlyout.Closed += OnFlyoutClosed;
        Refresh();
    }

    /// <summary>Raised when the list closed by Escape or by a change, so the composer can move focus back to the prompt.</summary>
    public event EventHandler? FocusReturnRequested;

    private Flyout ChoiceFlyout => (Flyout)ChipButton.Flyout!;

    /// <summary>Gets the chip button that opens the list.</summary>
    internal Button Chip => ChipButton;

    /// <summary>Gets the text of the chip.</summary>
    internal TextBlock ChipLabel => LabelText;

    /// <summary>Gets the list of values of the flyout.</summary>
    internal ListBox Choices => ChoiceList;

    /// <summary>Gets the model the chip draws, or null.</summary>
    internal OptionChipModel? Model => DataContext as OptionChipModel;

    /// <summary>Gets a value indicating whether the flyout is open.</summary>
    internal bool IsOpen => ChoiceFlyout.IsOpen;

    /// <summary>Opens the list, which takes focus. Does nothing while the chip is disabled.</summary>
    /// <returns>True when the list was opened.</returns>
    internal bool Open()
    {
        if (!ChipButton.IsEffectivelyEnabled)
        {
            return false;
        }

        ChoiceFlyout.ShowAt(ChipButton);
        return true;
    }

    /// <summary>Closes the list.</summary>
    internal void Close() => ChoiceFlyout.Hide();

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

    private static string OptionName(OptionChipModel? chip) =>
        chip is null || chip.OptionId.Length == 0 ? string.Empty : string.Concat(char.ToUpperInvariant(chip.OptionId[0]).ToString(), chip.OptionId.AsSpan(1));

    private void Refresh()
    {
        var chip = Model;
        IsVisible = chip is { IsVisible: true };
        ChipButton.IsEnabled = chip is { IsEnabled: true };
        var label = chip?.Label ?? string.Empty;
        LabelText.Text = label;
        AutomationProperties.SetName(ChipButton, $"{OptionName(chip)}: {label}");
        AutomationProperties.SetHelpText(ChipButton, chip?.ToolTipText);
        ToolTip.SetTip(ChipButton, chip?.ToolTipText);

        NoticeText.Text = chip?.Notice ?? string.Empty;
        NoticeText.IsVisible = chip is { HasNotice: true };

        RefreshRows(chip);
    }

    // The list is rebuilt only when the engine's values or the value in use changed, so a redraw for another property keeps the cursor.
    private void RefreshRows(OptionChipModel? chip)
    {
        if (chip is null)
        {
            _rowsFor = null;
            _rowsCurrent = null;
            ChoiceList.ItemsSource = null;
            return;
        }

        if (ReferenceEquals(_rowsFor, chip.Choices) && string.Equals(_rowsCurrent, chip.CurrentValue, StringComparison.Ordinal))
        {
            return;
        }

        _rowsFor = chip.Choices;
        _rowsCurrent = chip.CurrentValue;
        var rows = chip.Choices.Select(choice => new OptionChoiceRow(choice, string.Equals(choice.Value, chip.CurrentValue, StringComparison.Ordinal))).ToList();
        ChoiceList.ItemsSource = rows;
        ChoiceList.SelectedItem = rows.Find(static row => row.IsCurrent);
    }

    private void OnFlyoutOpened(object? sender, EventArgs e)
    {
        Refresh();
        ChoiceList.FocusWhenReady();
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>()?.DataContext is OptionChoiceRow row)
        {
            _ = ConfirmAsync(row);
        }
    }

    private void OnListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.None)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter or Key.Space when ChoiceList.SelectedItem is OptionChoiceRow row:
                e.Handled = true;
                _ = ConfirmAsync(row);
                break;
            case Key.Escape:
                e.Handled = true;
                CloseAndReturnFocus();
                break;
            default:
                break;
        }
    }

    /// <summary>Asks the model to change to the value of the row. The list closes when the change took, and stays open with the engine's reason when it did not.</summary>
    private async Task ConfirmAsync(OptionChoiceRow row)
    {
        if (Model is not { } chip)
        {
            return;
        }

        await chip.SelectCommand.ExecuteAsync(row.Choice).ConfigureAwait(true);
        if (!chip.HasNotice)
        {
            CloseAndReturnFocus();
        }
    }

    private void CloseAndReturnFocus()
    {
        if (ChoiceFlyout.IsOpen)
        {
            // The popup can move focus itself while it closes, so the request is repeated once it has closed.
            _returnFocusOnClose = true;
            ChoiceFlyout.Hide();
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
