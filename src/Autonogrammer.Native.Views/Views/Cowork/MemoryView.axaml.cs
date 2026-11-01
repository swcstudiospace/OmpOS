using Autonogrammer.Presentation.Cowork.Memory;
using Avalonia.Controls;

namespace Autonogrammer.Native.Views.Cowork;

/// <summary>The memory list and settings. The residual-risk sentence is always shown.</summary>
public partial class MemoryView : UserControl
{
    /// <summary>The class set when motion is reduced.</summary>
    public const string ReducedMotionClass = "reduced-motion";

    /// <summary>Creates the view.</summary>
    public MemoryView()
    {
        InitializeComponent();
        RiskText.Text = MemorySettingsModel.ResidualRiskSentence;
        LimitationText.Text = MemorySettingsModel.PoisoningLimitationNotice;
    }

    /// <summary>Gets the named controls in keyboard order.</summary>
    public IReadOnlyList<Control> KeyboardOrder => [EntryRows, DeleteEntryButton, EnabledBox, PausedBox, SensitiveBox];

    /// <summary>Applies reduced motion.</summary>
    /// <param name="reduced">True when motion is reduced.</param>
    public void ApplyMotion(bool reduced) => Classes.Set(ReducedMotionClass, reduced);

    /// <summary>Binds the list and settings.</summary>
    /// <param name="list">The list.</param>
    /// <param name="settings">The settings.</param>
    public void Attach(MemoryListModel list, MemorySettingsModel settings)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(settings);
        EntryRows.ItemsSource = list.Rows;
        StatusText.Text = list.Sentence ?? settings.Sentence ?? list.State.ToString();
        EnabledBox.IsChecked = settings.Enabled;
        PausedBox.IsChecked = settings.Paused;
        SensitiveBox.IsChecked = settings.IncludeSensitive;
    }
}
