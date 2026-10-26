using Avalonia;
using Avalonia.Controls;
using OmpOS.Presentation.AgentPanel;

namespace OmpOS.Native.Views.AgentPanel;

/// <summary>
/// The Pipeline tab: one block per source in pipeline order, each with its rolled-up status and its rows (glyph, title, detail, effect
/// line, Retry stage). It draws the <see cref="StageTimelineModel"/> that is its data context. The run summary sentence is given
/// separately through <see cref="RunSummary"/>, because only the session view model builds it.
/// </summary>
internal partial class PipelineTabView : UserControl
{
    /// <summary>The run summary sentence shown above the blocks, or null or empty for none.</summary>
    public static readonly StyledProperty<string?> RunSummaryProperty =
        AvaloniaProperty.Register<PipelineTabView, string?>(nameof(RunSummary));

    /// <summary>Creates a tab with no model.</summary>
    public PipelineTabView()
    {
        InitializeComponent();
        PipelineDetails.AddHandler(Button.ClickEvent, OnRetryClick);
        ShowRunSummary();
    }

    /// <summary>Gets or sets the run summary sentence, for example <c>Finished. Ultrathink: planned.</c>, or null while the run has no result.</summary>
    public string? RunSummary
    {
        get => GetValue(RunSummaryProperty);
        set => SetValue(RunSummaryProperty, value);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RunSummaryProperty)
        {
            ShowRunSummary();
        }
    }

    private void ShowRunSummary()
    {
        var summary = RunSummary;
        RunSummaryText.Text = summary;
        RunSummaryBlock.IsVisible = !string.IsNullOrWhiteSpace(summary);
    }

    private void OnRetryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is Button { DataContext: StageRow row } && DataContext is StageTimelineModel timeline)
        {
            timeline.RetryStageCommand(row.Source).Execute(null);
        }
    }
}
