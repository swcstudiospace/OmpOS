using Autonogrammer.Native.Views.AgentPanel;
using Autonogrammer.Presentation.Modes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Autonogrammer.Native.Views.Modes;

/// <summary>
/// The custom-mode chip of the composer footer (Cursor's Use as Mode). While the bound <see cref="CustomModeModel"/> has a pinned skill the chip shows its
/// name, a dot in the skill's colour (through the status tokens, never a literal colour) and a clear button that exits the mode. The Use as Mode button is
/// always shown: it opens the list of skills that can be pinned, which the model reads again each time the list opens, and a click or Enter on a skill pins it.
/// The status line says that a mode is on or off, or why a skill was not pinned, in a polite live region. The list is a popup without animation, so reduced
/// motion changes nothing here.
/// </summary>
internal partial class CustomModeChipView : UserControl
{
    private static readonly string[] ToneClasses = ["mode-default", "mode-success", "mode-warning", "mode-error", "mode-accent"];

    private readonly ModelWatcher<CustomModeModel> _watch;

    /// <summary>Creates the chip with no model. It shows only the Use as Mode button until a model with a pinned mode is bound.</summary>
    public CustomModeChipView()
    {
        InitializeComponent();
        _watch = new ModelWatcher<CustomModeModel>(Refresh);
        SkillList.Tapped += OnListTapped;
        SkillList.AddHandler(KeyDownEvent, OnListKeyDown, RoutingStrategies.Tunnel);
        SkillFlyout.Opened += OnFlyoutOpened;
        Refresh();
    }

    /// <summary>Gets the chip that names the pinned mode. It is hidden while no mode is pinned.</summary>
    internal Border Chip => ModeChip;

    /// <summary>Gets the text of the chip.</summary>
    internal TextBlock ChipLabel => ModeText;

    /// <summary>Gets the dot that carries the skill's colour. Its tone class is one of mode-default, mode-success, mode-warning, mode-error and mode-accent.</summary>
    internal Ellipse Dot => ModeDot;

    /// <summary>Gets the button that exits the mode.</summary>
    internal Button ClearButton => ClearModeButton;

    /// <summary>Gets the button that opens the list of skills.</summary>
    internal Button Picker => PickerButton;

    /// <summary>Gets the list of skills that can be pinned.</summary>
    internal ListBox Skills => SkillList;

    /// <summary>Gets the line that says why the list is empty or loading; it is hidden while skills are listed.</summary>
    internal TextBlock EmptyLine => EmptySkillsText;

    /// <summary>Gets the polite live region of the mode status.</summary>
    internal TextBlock Status => StatusLine;

    /// <summary>Gets a value indicating whether the list of skills is open.</summary>
    internal bool IsOpen => SkillFlyout.IsOpen;

    private Flyout SkillFlyout => (Flyout)PickerButton.Flyout!;

    private CustomModeModel? Model => DataContext as CustomModeModel;

    /// <summary>Opens the list of skills. The model reads the skills again and the list takes focus.</summary>
    internal void Open() => SkillFlyout.ShowAt(PickerButton);

    /// <summary>Closes the list of skills.</summary>
    internal void Close() => SkillFlyout.Hide();

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

    private static string ToneOf(string? colorKey) => colorKey switch
    {
        "green" => "mode-success",
        "yellow" or "orange" => "mode-warning",
        "red" => "mode-error",
        null or "default" => "mode-default",
        _ => "mode-accent",
    };

    private void Refresh()
    {
        var model = Model;
        SkillList.ItemsSource = model?.Skills;

        var tone = ToneOf(model?.ChipColorKey);
        foreach (var toneClass in ToneClasses)
        {
            _ = ModeDot.Classes.Remove(toneClass);
        }

        ModeDot.Classes.Add(tone);

        // The sentence under the list: skills still loading, or why there is none. It is empty while skills are listed.
        var empty = model is null || model.Skills.Count > 0 ? string.Empty : model.IsBusy ? CustomModeCopy.Loading : model.EmptyText;
        EmptySkillsText.Text = empty;
        EmptySkillsText.IsVisible = empty.Length > 0;
    }

    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (Model is { } model && model.Clear())
        {
            PickerButton.FocusWhenReady();
        }
    }

    private void OnFlyoutOpened(object? sender, EventArgs e) => _ = LoadAsync();

    // Opening the list reads the skills again, so a skill added since is there. The skill in use, or else the first, is highlighted.
    private async Task LoadAsync()
    {
        if (Model is not { } model)
        {
            return;
        }

        await model.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
        Refresh();
        var active = model.Active?.Skill.Name;
        SkillList.SelectedItem = model.Skills.FirstOrDefault(skill => string.Equals(skill.Name, active, StringComparison.OrdinalIgnoreCase))
            ?? model.Skills.FirstOrDefault();
        SkillList.FocusWhenReady();
    }

    private void OnListTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<ListBoxItem>()?.DataContext is ModeSkill skill)
        {
            _ = UseAsync(skill);
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
            case Key.Enter or Key.Space when SkillList.SelectedItem is ModeSkill skill:
                e.Handled = true;
                _ = UseAsync(skill);
                break;
            case Key.Escape:
                e.Handled = true;
                SkillFlyout.Hide();
                PickerButton.FocusWhenReady();
                break;
            default:
                break;
        }
    }

    /// <summary>Pins the skill. The list closes when the mode is on, and stays open with the status line when the skill could not be pinned.</summary>
    private async Task UseAsync(ModeSkill skill)
    {
        if (Model is not { } model)
        {
            return;
        }

        if (await model.UseAsModeAsync(skill.Name, CancellationToken.None).ConfigureAwait(true))
        {
            SkillFlyout.Hide();
            PickerButton.FocusWhenReady();
        }
    }
}
