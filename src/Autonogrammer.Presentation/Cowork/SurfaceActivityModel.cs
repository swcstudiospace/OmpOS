using Autonogrammer.Contracts.Cowork;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Cowork;

/// <summary>
/// Counts running, needs-approval and failed work on each surface, and the chip the hidden surface shows.
/// Code rows arrive through a reader and a change signal. Cowork rows arrive through <see cref="ICoworkSessionCatalog"/>.
/// </summary>
public sealed class SurfaceActivityModel : ObservableModel, IDisposable
{
    private readonly SurfaceSwitchModel _switch;
    private readonly Func<IReadOnlyList<AgentRow>> _readCodeRows;
    private readonly Action<EventHandler> _unsubscribeCode;
    private readonly ICoworkSessionCatalog _cowork;
    private readonly EventHandler _onCodeChanged;
    private readonly EventHandler _onCoworkChanged;
    private readonly EventHandler _onSwitchChanged;
    private bool _disposed;

    /// <summary>Creates the model and reads the first counts.</summary>
    /// <param name="surface">The switch whose hidden side the chip describes.</param>
    /// <param name="readCodeRows">Reads the Code rows the head already holds. Running is Running or Queued.</param>
    /// <param name="subscribeCode">Subscribes the change signal for those rows.</param>
    /// <param name="unsubscribeCode">Removes that subscription.</param>
    /// <param name="cowork">The Cowork catalog. Needs approval is <see cref="CoworkSessionStatus.WaitingForDecision"/>.</param>
    public SurfaceActivityModel(
        SurfaceSwitchModel surface,
        Func<IReadOnlyList<AgentRow>> readCodeRows,
        Action<EventHandler> subscribeCode,
        Action<EventHandler> unsubscribeCode,
        ICoworkSessionCatalog cowork)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(readCodeRows);
        ArgumentNullException.ThrowIfNull(subscribeCode);
        ArgumentNullException.ThrowIfNull(unsubscribeCode);
        ArgumentNullException.ThrowIfNull(cowork);
        _switch = surface;
        _readCodeRows = readCodeRows;
        _unsubscribeCode = unsubscribeCode;
        _cowork = cowork;
        _onCodeChanged = (_, _) => Refresh();
        _onCoworkChanged = (_, _) => Refresh();
        _onSwitchChanged = (_, _) => Refresh();
        subscribeCode(_onCodeChanged);
        cowork.Changed += _onCoworkChanged;
        surface.Changed += _onSwitchChanged;
        Refresh();
    }

    /// <summary>Occurs after the counts or the chip change.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets how many Code rows are Running or Queued.</summary>
    public int CodeRunning { get; private set; }

    /// <summary>Gets how many Code rows need approval.</summary>
    public int CodeNeedsApproval { get; private set; }

    /// <summary>Gets how many Code rows failed.</summary>
    public int CodeFailed { get; private set; }

    /// <summary>Gets how many Cowork tasks are running.</summary>
    public int CoworkRunning { get; private set; }

    /// <summary>Gets how many Cowork tasks are waiting for a decision.</summary>
    public int CoworkNeedsApproval { get; private set; }

    /// <summary>Gets how many Cowork tasks failed.</summary>
    public int CoworkFailed { get; private set; }

    /// <summary>Gets the chip for the hidden surface, or empty when that surface has nothing to report.</summary>
    public string ChipText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the same status as <see cref="ChipText"/>, for a polite live region. It is never a command and never an assertive alert.
    /// </summary>
    public string PoliteAnnouncement { get; private set; } = string.Empty;

    /// <summary>The live-region setting. It is always polite.</summary>
    public const string LiveSetting = CoworkCopy.PoliteLiveSetting;

    /// <summary>Reads both sources again.</summary>
    public void Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var code = _readCodeRows() ?? [];
        CodeRunning = code.Count(row => row.Status is AgentRowStatus.Running or AgentRowStatus.Queued);
        CodeNeedsApproval = code.Count(row => row.Status == AgentRowStatus.NeedsApproval);
        CodeFailed = code.Count(row => row.Status == AgentRowStatus.Failed);

        var cowork = _cowork.Snapshot();
        CoworkRunning = cowork.Count(row => row.Status == CoworkSessionStatus.Running);
        CoworkNeedsApproval = cowork.Count(row => row.Status == CoworkSessionStatus.WaitingForDecision);
        CoworkFailed = cowork.Count(row => row.Status == CoworkSessionStatus.Failed);

        var hidden = _switch.Selected == SessionSurface.Cowork ? SessionSurface.Code : SessionSurface.Cowork;
        var name = hidden == SessionSurface.Cowork ? "Cowork" : "Code";
        var running = hidden == SessionSurface.Cowork ? CoworkRunning : CodeRunning;
        var needsApproval = hidden == SessionSurface.Cowork ? CoworkNeedsApproval : CodeNeedsApproval;
        var failed = hidden == SessionSurface.Cowork ? CoworkFailed : CodeFailed;
        ChipText = needsApproval > 0
            ? CoworkCopy.NeedsApprovalChip(name)
            : running > 0
                ? CoworkCopy.RunningChip(name, running)
                : failed > 0
                    ? CoworkCopy.FailedChip(name, failed)
                    : string.Empty;
        PoliteAnnouncement = ChipText;
        NotifyPropertyChanged(nameof(CodeRunning));
        NotifyPropertyChanged(nameof(CodeNeedsApproval));
        NotifyPropertyChanged(nameof(CodeFailed));
        NotifyPropertyChanged(nameof(CoworkRunning));
        NotifyPropertyChanged(nameof(CoworkNeedsApproval));
        NotifyPropertyChanged(nameof(CoworkFailed));
        NotifyPropertyChanged(nameof(ChipText));
        NotifyPropertyChanged(nameof(PoliteAnnouncement));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _unsubscribeCode(_onCodeChanged);
        _cowork.Changed -= _onCoworkChanged;
        _switch.Changed -= _onSwitchChanged;
    }
}
