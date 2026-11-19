using System.Collections.ObjectModel;
using System.Globalization;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// One saved ssh environment on the remote page: its name, where it points, when it was last used, its status in words (the outcome of the last
/// connection test) and the findings of the last test, each with its fix.
/// </summary>
public sealed class RemoteProfileRow : ObservableModel
{
    private readonly ObservableCollection<RemoteDiagnosisRow> _diagnoses = [];
    private RemoteEnvironmentStatus _tested = RemoteEnvironmentStatus.NotChecked;
    private bool _isTesting;
    private string _environmentText = string.Empty;
    private readonly string _lastUsedText;

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

    /// <summary>Gets the status the row shows: a running test, else the outcome of the last test.</summary>
    public RemoteEnvironmentStatus Status => _isTesting ? RemoteEnvironmentStatus.Testing : _tested;

    /// <summary>Gets the short label of <see cref="Status"/>.</summary>
    public string StatusLabel => RemoteStatusDescriber.LabelOf(Status);

    /// <summary>Gets the sentence that explains <see cref="Status"/> and what to do.</summary>
    public string StatusDetail => RemoteStatusDescriber.DetailOf(Status);

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

    // After a reload the row keeps the test outcome only while the ssh settings it was made with are unchanged.
    internal void CarryOver(RemoteProfileRow earlier)
    {
        _isTesting = earlier._isTesting;
        if (!SameSshSettings(Ssh, earlier.Ssh))
        {
            return;
        }

        _tested = earlier._tested;
        _environmentText = earlier._environmentText;
        foreach (var diagnosis in earlier._diagnoses)
        {
            _diagnoses.Add(diagnosis);
        }
    }

    // Every setting the connection test is made with: a change to any of them makes the outcome of the test stale.
    private static bool SameSshSettings(SshProfile current, SshProfile earlier) =>
        string.Equals(current.Alias, earlier.Alias, StringComparison.Ordinal)
        && string.Equals(current.Hostname, earlier.Hostname, StringComparison.Ordinal)
        && current.Port == earlier.Port
        && string.Equals(current.User, earlier.User, StringComparison.Ordinal)
        && string.Equals(current.IdentityFile, earlier.IdentityFile, StringComparison.Ordinal)
        && string.Equals(current.ProxyJump, earlier.ProxyJump, StringComparison.Ordinal)
        && SameOptions(current.Options, earlier.Options)
        && current.ForwardAgent == earlier.ForwardAgent
        && current.KeepAliveSeconds == earlier.KeepAliveSeconds
        && string.Equals(current.RemoteWorkspaceRoot, earlier.RemoteWorkspaceRoot, StringComparison.Ordinal)
        && string.Equals(current.RemoteOmpPath, earlier.RemoteOmpPath, StringComparison.Ordinal);

    private static bool SameOptions(IReadOnlyDictionary<string, string> current, IReadOnlyDictionary<string, string> earlier) =>
        current.Count == earlier.Count
        && current.All(pair => earlier.TryGetValue(pair.Key, out var value) && string.Equals(pair.Value, value, StringComparison.Ordinal));

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
    }
}
