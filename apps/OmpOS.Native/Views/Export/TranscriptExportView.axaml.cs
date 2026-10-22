using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using OmpOS.GenUi;
using OmpOS.Presentation.Export;

namespace OmpOS.Native.Views.Export;

/// <summary>
/// Exports a transcript to a Markdown file at a typed path, and lists the usage history per day or per month.
/// </summary>
/// <remarks>
/// The export is the pure <see cref="TranscriptMarkdownExporter"/> output written as UTF-8 without a byte-order mark. The
/// path is read when Export is pressed. A failed write shows its reason and leaves <see cref="LastExportedPath"/> empty.
/// </remarks>
internal partial class TranscriptExportView : UserControl
{
    private readonly UiTree _transcript;
    private readonly string _title;
    private readonly UsageHistory _usage;

    /// <summary>Creates the export view for one transcript and its usage history.</summary>
    /// <param name="transcript">The transcript to export. It is read when Export is pressed.</param>
    /// <param name="title">The document title written as the level-one heading. Not empty.</param>
    /// <param name="usage">The usage history listed below the export controls.</param>
    /// <exception cref="ArgumentNullException"><paramref name="transcript"/> or <paramref name="usage"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="title"/> is null, empty or white space.</exception>
    public TranscriptExportView(UiTree transcript, string title, UsageHistory usage)
    {
        ArgumentNullException.ThrowIfNull(transcript);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(usage);

        _transcript = transcript;
        _title = title;
        _usage = usage;

        InitializeComponent();
        PeriodSelector.ItemsSource = Enum.GetValues<UsagePeriod>();
        PeriodSelector.SelectedItem = UsagePeriod.Day;
        PeriodSelector.SelectionChanged += OnPeriodChanged;
        ExportButton.Click += OnExportClick;
        RenderUsage();
    }

    /// <summary>Gets the path the last successful export wrote, or <see langword="null"/> when the last export did not write a file.</summary>
    public string? LastExportedPath { get; private set; }

    private void OnExportClick(object? sender, RoutedEventArgs e) => Observe(ExportAsync(PathInput.Text ?? string.Empty));

    private async Task ExportAsync(string typedPath)
    {
        var path = typedPath.Trim();
        LastExportedPath = null;

        if (path.Length == 0)
        {
            StatusText.Text = "Enter a file path to export to.";
            return;
        }

        var markdown = TranscriptMarkdownExporter.Export(_transcript, _title);
        try
        {
            await File.WriteAllTextAsync(path, markdown).ConfigureAwait(true);
        }
        catch (IOException error)
        {
            ShowFailure(error);
            return;
        }
        catch (UnauthorizedAccessException error)
        {
            ShowFailure(error);
            return;
        }
        catch (ArgumentException error)
        {
            ShowFailure(error);
            return;
        }
        catch (NotSupportedException error)
        {
            ShowFailure(error);
            return;
        }

        LastExportedPath = path;
        StatusText.Text = $"Exported to {path}.";
    }

    private void ShowFailure(Exception error) => StatusText.Text = $"Export failed: {error.Message}";

    private void OnPeriodChanged(object? sender, SelectionChangedEventArgs e) => RenderUsage();

    private void RenderUsage()
    {
        var period = PeriodSelector.SelectedItem is UsagePeriod selected ? selected : UsagePeriod.Day;
        var totals = _usage.Totals(period);
        UsageList.ItemsSource = totals;
        UsageEmptyText.IsVisible = totals.Count == 0;
    }

    // Surfaces a fault from background work on the UI thread, where it propagates like any other unhandled error.
    private static void Observe(Task work) => _ = work.ContinueWith(
        faulted => Dispatcher.UIThread.Post(() => ExceptionDispatchInfo.Throw(faulted.Exception!.GetBaseException())),
        CancellationToken.None,
        TaskContinuationOptions.OnlyOnFaulted,
        TaskScheduler.Default);
}
