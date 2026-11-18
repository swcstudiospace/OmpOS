using Autonogrammer.Presentation.Cowork.Memory;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>The memory list and settings. The residual-risk sentence is always shown.</summary>
public partial class MemoryView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    private MemorySettingsModel? _settings;
    private bool _applying;

    /// <summary>Creates the view.</summary>
    public MemoryView()
    {
        InitializeComponent();
        RiskText.Text = MemorySettingsModel.ResidualRiskSentence;
        LimitationText.Text = MemorySettingsModel.PoisoningLimitationNotice;
        EnabledBox.IsCheckedChanged += OnSettingToggled;
        PausedBox.IsCheckedChanged += OnSettingToggled;
        SensitiveBox.IsCheckedChanged += OnSettingToggled;
    }

    /// <summary>Gets the named controls in keyboard order.</summary>
    public IReadOnlyList<Control> KeyboardOrder => [EntryRows, DeleteEntryButton, EnabledBox, PausedBox, SensitiveBox];

    /// <summary>Applies reduced motion.</summary>
    /// <param name="reduced">True when motion is reduced.</param>
    public void ApplyMotion(bool reduced) => Classes.Set(ReducedMotionClass, reduced);

    /// <summary>Binds the list and settings. Changing a checkbox saves the settings through the model.</summary>
    /// <param name="list">The list.</param>
    /// <param name="settings">The settings.</param>
    public void Attach(MemoryListModel list, MemorySettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(settings);
        _settings = settings;
        EntryRows.ItemsSource = list.Rows;
        StatusText.Text = list.Sentence ?? settings.Sentence ?? list.State.ToString();
        _applying = true;
        try
        {
            EnabledBox.IsChecked = settings.Enabled;
            PausedBox.IsChecked = settings.Paused;
            SensitiveBox.IsChecked = settings.IncludeSensitive;
        }
        finally
        {
            _applying = false;
        }
    }

    private void OnSettingToggled(object? sender, RoutedEventArgs e)
    {
        if (_settings is null || _applying)
        {
            return;
        }

        _ = _settings.SaveAsync(
            EnabledBox.IsChecked == true,
            PausedBox.IsChecked == true,
            SensitiveBox.IsChecked == true,
            CancellationToken.None);
    }
}
