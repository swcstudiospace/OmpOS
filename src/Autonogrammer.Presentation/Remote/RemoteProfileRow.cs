using System.Collections.ObjectModel;
using System.Globalization;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// One saved ssh environment on the remote page: its name, where it points, when it was last used, its status in words (the outcome of the last
/// connection test, or the live state of a session on it) and the findings of the last test, each with its fix.
/// </summary>
public sealed class RemoteProfileRow : ObservableModel
{
    private readonly ObservableCollection<RemoteDiagnosisRow> _diagnoses = [];
    private RemoteEnvironmentStatus _tested = RemoteEnvironmentStatus.NotChecked;
    private RemoteEnvironmentStatus? _live;
    private bool _isTesting;
    private int _reconnectAttempt;
    private string _environmentText = string.Empty;
    private readonly string _lastUsedText;
    private RemoteOmpReport? _omp;
    private string _ompResultText = string.Empty;
    private IReadOnlyList<string> _ompDriftLines = Array.Empty<string>();
    private bool _ompOffered;
    private bool _ompBusy;

    internal RemoteProfileRow(RemoteStoredProfile stored)
    {
        Profile = stored.Profile;
        Ssh = stored.Profile.Ssh ?? throw new ArgumentException("The profile has no ssh details.", nameof(stored));
        _lastUsedText = FormatLastUsed(stored.LastUsedUtc);
        Diagnoses = new ReadOnlyObservableCollection<RemoteDiagnosisRow>(_diagnoses);
    }

    /// <summary>Gets the profile the row shows.</summary>
    public EnvironmentProfile Profile { get; }

    /// <summary>Gets the ssh details of the profile.</summary>
    public SshProfile Ssh { get; }

    /// <summary>Gets the stable identifier of the profile.</summary>
    public string Id => Profile.Id;

    /// <summary>Gets the name shown on the row.</summary>
    public string DisplayName => Profile.DisplayName;

    /// <summary>Gets where the row points, as <c>user@host:port</c> with what is set, or the alias.</summary>
    public string DestinationText
    {
        get
        {
            var host = Ssh.Hostname ?? Ssh.Alias;
            var user = Ssh.User is null ? string.Empty : Ssh.User + "@";
            var port = Ssh.Port is { } number ? ":" + number.ToString(CultureInfo.InvariantCulture) : string.Empty;
            return user + host + port;
        }
    }

    /// <summary>Gets the line about the remote folder.</summary>
    public string RootText => RemoteCopy.RemoteFolderPrefix + Ssh.RemoteWorkspaceRoot;

    /// <summary>Gets when a session last started on the environment, in words, or that none has.</summary>
    public string LastUsedText => _lastUsedText;

    /// <summary>Gets what the last test found on the host (omp version and platform), or an empty string before a passing test.</summary>
    public string EnvironmentText => _environmentText;

    /// <summary>Gets the findings of the last test: errors first, each with its fix. Empty after a clean test and before the first one.</summary>
    public ReadOnlyObservableCollection<RemoteDiagnosisRow> Diagnoses { get; }

    /// <summary>Gets whether the last test produced findings.</summary>
    public bool HasDiagnoses => _diagnoses.Count > 0;

    /// <summary>Gets the status the row shows: a running test, else the live session state, else the outcome of the last test.</summary>
    public RemoteEnvironmentStatus Status => _isTesting ? RemoteEnvironmentStatus.Testing : _live ?? _tested;

    /// <summary>Gets the short label of <see cref="Status"/>.</summary>
    public string StatusLabel => RemoteStatusDescriber.LabelOf(Status);

    /// <summary>Gets the sentence that explains <see cref="Status"/> and what to do.</summary>
    public string StatusDetail => RemoteStatusDescriber.DetailOf(Status, _reconnectAttempt);

    /// <summary>Gets whether a connection test is running.</summary>
    public bool IsTesting => _isTesting;

    /// <summary>Gets whether the Test connection button can be pressed.</summary>
    public bool CanTest => !_isTesting;

    /// <summary>Gets whether the host trust terminal is offered: the host key is not trusted yet.</summary>
    public bool CanTrustHost => Status == RemoteEnvironmentStatus.HostKeyUnconfirmed && !_isTesting;

    /// <summary>Gets whether the status is a success state.</summary>
    public bool IsGood => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Good;

    /// <summary>Gets whether the status is a caveat or a recovery.</summary>
    public bool IsWarning => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Warning;

    /// <summary>Gets whether the status is a failure.</summary>
    public bool IsError => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Error;

    /// <summary>Gets whether the status is a step in progress.</summary>
    public bool IsProgress => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Progress;

    /// <summary>Gets the text a screen reader announces for the row: name, destination and status.</summary>
    public string AccessibleName => DisplayName + ", " + DestinationText + ", " + StatusLabel;

    /// <summary>Gets whether the page offers Check Omp and Install Omp for this environment: the head provides an Omp provisioner.</summary>
    public bool OmpOffered => _ompOffered;

    /// <summary>Gets whether an Omp check or install of this environment is running.</summary>
    public bool IsOmpBusy => _ompBusy;

    /// <summary>Gets whether the Check Omp button can be pressed.</summary>
    public bool CanCheckOmp => _ompOffered && !_ompBusy && !_isTesting;

    /// <summary>Gets whether the Install Omp button can be pressed: the last check planned lines and nothing has run since.</summary>
    public bool CanInstallOmp => CanCheckOmp && _omp?.Outcome == RemoteOmpOutcome.Planned;

    /// <summary>Gets whether an Omp check or install result is shown.</summary>
    public bool HasOmpResult => _omp is not null;

    /// <summary>Gets the result of the last Omp check or install in words, or an empty string before one.</summary>
    public string OmpResultText => _ompResultText;

    /// <summary>Gets what the last Omp check found, one line per item. Empty before a check.</summary>
    public IReadOnlyList<string> OmpDriftLines => _ompDriftLines;

    /// <summary>Gets the lines the last Omp check planned: exactly the lines Install Omp applies. Empty before a check and when it planned none.</summary>
    public IReadOnlyList<string> OmpActionLines => _omp?.PlannedActions ?? Array.Empty<string>();

    /// <summary>Gets whether the last Omp check found any item that differs.</summary>
    public bool HasOmpDrift => _ompDriftLines.Count > 0;

    /// <summary>Gets whether the last Omp check planned any line an install applies.</summary>
    public bool HasOmpActions => OmpActionLines.Count > 0;

    internal void BeginTest()
    {
        _isTesting = true;
        Refresh();
    }

    internal void EndTest()
    {
        _isTesting = false;
        Refresh();
    }

    internal void ApplyReport(RemotePreflightReport report)
    {
        _tested = RemoteStatusDescriber.FromReport(report);
        _diagnoses.Clear();
        foreach (var diagnosis in report.Diagnoses)
        {
            _diagnoses.Add(RemoteDiagnosisRow.From(diagnosis));
        }

        _environmentText = Describe(report);
        _isTesting = false;
        Refresh();
    }

    internal void ApplyTestFailure(string message)
    {
        _tested = RemoteEnvironmentStatus.Unreachable;
        _diagnoses.Clear();
        _diagnoses.Add(new RemoteDiagnosisRow
        {
            SeverityLabel = RemoteCopy.ErrorLabel,
            IsError = true,
            Summary = message,
            FixText = "Check that ssh works for this host in a terminal, then test the connection again.",
        });
        _environmentText = string.Empty;
        _isTesting = false;
        Refresh();
    }

    internal void ApplyConnection(RemoteConnectionState state, int reconnectAttempt, bool turnInterrupted)
    {
        _live = RemoteStatusDescriber.FromConnection(state, turnInterrupted);
        _reconnectAttempt = reconnectAttempt;
        Refresh();
    }

    internal void ApplyAuthenticating()
    {
        _live = RemoteEnvironmentStatus.Authenticating;
        Refresh();
    }

    internal void ClearConnection()
    {
        _live = null;
        _reconnectAttempt = 0;
        Refresh();
    }

    internal void OfferOmp(bool offered)
    {
        _ompOffered = offered;
        RefreshOmp();
    }

    internal void BeginOmp()
    {
        _ompBusy = true;
        RefreshOmp();
    }

    internal void EndOmp()
    {
        _ompBusy = false;
        RefreshOmp();
    }

    internal void ApplyOmp(RemoteOmpReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        _omp = report;
        _ompResultText = RemoteOmpDescriber.ResultText(report);
        _ompDriftLines = [.. report.Drift.Select(line => RemoteOmpDescriber.DriftText(line))];
        _ompBusy = false;
        RefreshOmp();
    }

    private void RefreshOmp()
    {
        NotifyPropertyChanged(nameof(OmpOffered));
        NotifyPropertyChanged(nameof(IsOmpBusy));
        NotifyPropertyChanged(nameof(CanCheckOmp));
        NotifyPropertyChanged(nameof(CanInstallOmp));
        NotifyPropertyChanged(nameof(HasOmpResult));
        NotifyPropertyChanged(nameof(OmpResultText));
        NotifyPropertyChanged(nameof(OmpDriftLines));
        NotifyPropertyChanged(nameof(OmpActionLines));
        NotifyPropertyChanged(nameof(HasOmpDrift));
        NotifyPropertyChanged(nameof(HasOmpActions));
    }

    // After a reload the row keeps what it knew: the live session state always, the test outcome only while the host settings it was made with are unchanged.
    internal void CarryOver(RemoteProfileRow earlier)
    {
        _live = earlier._live;
        _reconnectAttempt = earlier._reconnectAttempt;
        _isTesting = earlier._isTesting;
        var sameHost = string.Equals(Ssh.Alias, earlier.Ssh.Alias, StringComparison.Ordinal)
            && string.Equals(Ssh.Hostname, earlier.Ssh.Hostname, StringComparison.Ordinal)
            && Ssh.Port == earlier.Ssh.Port
            && string.Equals(Ssh.User, earlier.Ssh.User, StringComparison.Ordinal)
            && string.Equals(Ssh.RemoteWorkspaceRoot, earlier.Ssh.RemoteWorkspaceRoot, StringComparison.Ordinal)
            && string.Equals(Ssh.RemoteOmpPath, earlier.Ssh.RemoteOmpPath, StringComparison.Ordinal);
        if (sameHost)
        {
            _tested = earlier._tested;
            _environmentText = earlier._environmentText;
            foreach (var diagnosis in earlier._diagnoses)
            {
                _diagnoses.Add(diagnosis);
            }
        }

        // A reload while an install runs keeps no planned lines: the install may still be writing, so Install Omp must not be offered for them.
        if (sameHost && !earlier._ompBusy)
        {
            _omp = earlier._omp;
            _ompResultText = earlier._ompResultText;
            _ompDriftLines = earlier._ompDriftLines;
        }
    }

    private static string FormatLastUsed(DateTimeOffset? lastUsed) =>
        lastUsed is { } moment
            ? RemoteCopy.LastUsedPrefix + moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : RemoteCopy.NeverUsed;

    private static string Describe(RemotePreflightReport report)
    {
        var parts = new List<string>();
        if (report.OmpVersion is { Length: > 0 } version)
        {
            parts.Add("omp " + version);
        }

        if (report.Platform is { Length: > 0 } platform)
        {
            parts.Add(platform);
        }

        return string.Join(", ", parts);
    }

    private void Refresh()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(StatusLabel));
        NotifyPropertyChanged(nameof(StatusDetail));
        NotifyPropertyChanged(nameof(IsTesting));
        NotifyPropertyChanged(nameof(CanTest));
        NotifyPropertyChanged(nameof(CanTrustHost));
        NotifyPropertyChanged(nameof(IsGood));
        NotifyPropertyChanged(nameof(IsWarning));
        NotifyPropertyChanged(nameof(IsError));
        NotifyPropertyChanged(nameof(IsProgress));
        NotifyPropertyChanged(nameof(AccessibleName));
        NotifyPropertyChanged(nameof(HasDiagnoses));
        NotifyPropertyChanged(nameof(EnvironmentText));
        NotifyPropertyChanged(nameof(CanCheckOmp));
        NotifyPropertyChanged(nameof(CanInstallOmp));
    }
}
