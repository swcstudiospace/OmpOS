namespace OmpOS.Presentation.Settings;

/// <summary>
/// The Network page: a Run diagnostics button and the result of the latest run. A run sends one unauthenticated request to each
/// endpoint the source knows; it starts only when the user asks and never at load. The page has nothing to edit and nothing to save. A run
/// can be cancelled; a failed or cancelled run never throws out of the page, it only changes what the page announces.
/// </summary>
public sealed class NetworkSettingsPage : SettingsPageBase
{
    private readonly INetworkDiagnosticsSource _source;
    private CancellationTokenSource? _run;
    private IReadOnlyList<NetworkCheckRow> _rows = [];
    private bool _hasReport;
    private bool _isRunning;
    private string _announcement = string.Empty;
    private string _checkedAt = string.Empty;

    /// <summary>Initializes a new instance of the <see cref="NetworkSettingsPage"/> class.</summary>
    /// <param name="source">Where a diagnostics run is made.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public NetworkSettingsPage(INetworkDiagnosticsSource source)
        : base(SettingsSection.Network, NetworkCopy.Title, SettingsPageState.Loading)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>Gets the lines of the latest result, or none before the first finished run.</summary>
    public IReadOnlyList<NetworkCheckRow> Rows => _rows;

    /// <summary>Gets a value indicating whether a run has finished since the page was created.</summary>
    public bool HasReport => _hasReport;

    /// <summary>Gets a value indicating whether no run has finished yet, so the page shows that nothing was checked.</summary>
    public bool IsNotRun => State == SettingsPageState.Ready && !_hasReport && !_isRunning;

    /// <summary>Gets a value indicating whether the latest run found no endpoint to check.</summary>
    public bool IsEmpty => _hasReport && _rows.Count == 0;

    /// <summary>Gets a value indicating whether a run is in progress.</summary>
    public bool IsRunning => _isRunning;

    /// <summary>Gets a value indicating whether Run diagnostics can be pressed: the page is ready and no run is in progress.</summary>
    public bool CanRun => State == SettingsPageState.Ready && !_isRunning;

    /// <summary>Gets the text of the polite live region: that a run is in progress, how it ended, or empty before the first run.</summary>
    public string Announcement => _announcement;

    /// <summary>Gets the line that says when the latest result was made, or an empty string before the first finished run.</summary>
    public string CheckedAtText => _checkedAt;

    /// <inheritdoc/>
    public override Task LoadAsync(CancellationToken cancellationToken) => RunLoadAsync(static _ => Task.CompletedTask, cancellationToken);

    /// <summary>
    /// Runs the diagnostics. It does nothing while a run is in progress or the page is not ready. The page shows the run as in progress,
    /// then shows the result and announces it. A cancelled run keeps the previous result; a failed run keeps it too and announces the failure.
    /// This method does not throw.
    /// </summary>
    /// <param name="cancellationToken">Cancels the run together with <see cref="CancelRun"/>.</param>
    /// <returns>A task that completes when the run ended, failed or was cancelled.</returns>
    public async Task RunDiagnosticsAsync(CancellationToken cancellationToken)
    {
        if (!CanRun)
        {
            return;
        }

        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _run = run;
        SetRunning(true);
        SetAnnouncement(NetworkCopy.Running);
        try
        {
            // The continuation must run on the context that owns the model, so the context is captured.
            var report = await _source.RunAsync(run.Token).ConfigureAwait(true);
            Apply(report);
        }
        catch (OperationCanceledException)
        {
            SetAnnouncement(NetworkCopy.Cancelled);
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            SetAnnouncement(fault is SettingsSourceException { Message.Length: > 0 } expected ? expected.Message : NetworkCopy.RunFailed);
        }
        finally
        {
            _run = null;
            SetRunning(false);
        }
    }

    /// <summary>Cancels the run that is in progress. It does nothing when no run is in progress.</summary>
    public void CancelRun() => _run?.Cancel();

    /// <inheritdoc/>
    protected override void OnStateChanged()
    {
        NotifyPropertyChanged(nameof(CanRun));
        NotifyPropertyChanged(nameof(IsNotRun));
    }

    private static NetworkCheckRow ToRow(EndpointCheck check)
    {
        var host = EndpointDisplay.HostAndPort(check.Host) ?? NetworkCopy.HostHidden;
        var label = NetworkCopy.StatusLabel(check.Status);
        var detail = NetworkCopy.Detail(check);
        return new NetworkCheckRow(check.Name, host, label, detail, NetworkCopy.RowSummary(check.Name, host, label, detail), check.Status);
    }

    private void Apply(NetworkDiagnosticsReport report)
    {
        _rows = [.. report.Checks.Select(ToRow)];
        _hasReport = true;
        _checkedAt = NetworkCopy.CheckedAt(report.At);
        NotifyPropertyChanged(nameof(Rows));
        NotifyPropertyChanged(nameof(HasReport));
        NotifyPropertyChanged(nameof(IsNotRun));
        NotifyPropertyChanged(nameof(IsEmpty));
        NotifyPropertyChanged(nameof(CheckedAtText));
        var reachable = _rows.Count(static row => row.Status == EndpointStatus.Reachable);
        SetAnnouncement(_rows.Count == 0 ? NetworkCopy.NoEndpoints : NetworkCopy.Finished(reachable, _rows.Count));
    }

    private void SetRunning(bool running)
    {
        if (SetProperty(ref _isRunning, running, nameof(IsRunning)))
        {
            NotifyPropertyChanged(nameof(CanRun));
            NotifyPropertyChanged(nameof(IsNotRun));
        }
    }

    private void SetAnnouncement(string text) => SetProperty(ref _announcement, text, nameof(Announcement));
}
