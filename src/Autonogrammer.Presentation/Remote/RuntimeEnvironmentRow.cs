using System.Collections.ObjectModel;
using System.Globalization;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// One saved container or WSL environment on the page: its name, kind, where it points, when it was last used, its status in words (the outcome of the last test or
/// of the last start) and the findings of the last test, each with its fix. A stopped container offers Start container.
/// </summary>
public sealed class RuntimeEnvironmentRow : ObservableModel
{
    private readonly ObservableCollection<RemoteDiagnosisRow> _diagnoses = [];
    private readonly string _lastUsedText;
    private RemoteEnvironmentStatus _tested = RemoteEnvironmentStatus.NotChecked;
    private bool _isBusy;
    private bool _isTesting;
    private string _environmentText = string.Empty;
    private RuntimeEnvironmentRow? _successor;

    internal RuntimeEnvironmentRow(RemoteStoredProfile stored)
    {
        Profile = stored.Profile;
        if (stored.Profile.Kind == EnvironmentKind.Container && stored.Profile.Container is { } container)
        {
            Container = container;
        }
        else if (stored.Profile.Kind == EnvironmentKind.Wsl && stored.Profile.Wsl is { } wsl)
        {
            Wsl = wsl;
        }
        else
        {
            throw new ArgumentException("The profile is not a container or WSL environment with its details.", nameof(stored));
        }

        _lastUsedText = stored.LastUsedUtc is { } moment
            ? RemoteCopy.LastUsedPrefix + moment.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture)
            : RemoteCopy.NeverUsed;
        Diagnoses = new ReadOnlyObservableCollection<RemoteDiagnosisRow>(_diagnoses);
    }

    /// <summary>Gets the profile the row shows.</summary>
    public EnvironmentProfile Profile { get; }

    /// <summary>Gets the container details, or <see langword="null"/> for a WSL environment.</summary>
    public ContainerProfile? Container { get; }

    /// <summary>Gets the WSL details, or <see langword="null"/> for a container environment.</summary>
    public WslProfile? Wsl { get; }

    /// <summary>Gets the stable identifier of the profile.</summary>
    public string Id => Profile.Id;

    /// <summary>Gets the name shown on the row.</summary>
    public string DisplayName => Profile.DisplayName;

    /// <summary>Gets what kind of environment the row is.</summary>
    public EnvironmentKind Kind => Profile.Kind;

    /// <summary>Gets the word for the kind: <c>Container</c> or <c>WSL</c>, so the kind never depends on an icon.</summary>
    public string KindLabel => Container is not null ? ContainerCopy.ContainerKindLabel : ContainerCopy.WslKindLabel;

    /// <summary>Gets where the row points: <c>name as user</c> for a container, <c>distribution as user</c> for WSL.</summary>
    public string DestinationText
    {
        get
        {
            var (name, user) = Container is { } container ? (container.Container, container.RemoteUser) : (Wsl!.Distro, Wsl.User);
            return user is null ? name : name + " as " + user;
        }
    }

    /// <summary>Gets the line about the folder.</summary>
    public string RootText => Container is { } container
        ? ContainerCopy.FolderInContainerPrefix + container.WorkspaceFolder
        : ContainerCopy.FolderInDistroPrefix + Wsl!.WorkspacePath;

    /// <summary>Gets when a session last started on the environment, in words, or that none has.</summary>
    public string LastUsedText => _lastUsedText;

    /// <summary>Gets what the last test found (omp version and platform), or an empty string before a passing test.</summary>
    public string EnvironmentText => _environmentText;

    /// <summary>Gets the findings of the last test: errors first, each with its fix.</summary>
    public ReadOnlyObservableCollection<RemoteDiagnosisRow> Diagnoses { get; }

    /// <summary>Gets whether the last test produced findings.</summary>
    public bool HasDiagnoses => _diagnoses.Count > 0;

    /// <summary>Gets the status the row shows: a running test or start, else the outcome of the last test.</summary>
    public RemoteEnvironmentStatus Status => _isTesting ? RemoteEnvironmentStatus.Testing : _tested;

    /// <summary>Gets the short label of <see cref="Status"/>.</summary>
    public string StatusLabel => RemoteStatusDescriber.LabelOf(Status);

    /// <summary>Gets the sentence that explains <see cref="Status"/> and what to do.</summary>
    public string StatusDetail => RemoteStatusDescriber.DetailOf(Status);

    /// <summary>Gets whether a test or a start is running.</summary>
    public bool IsBusy => _isBusy;

    /// <summary>Gets whether a connection test is running.</summary>
    public bool IsTesting => _isTesting;

    /// <summary>Gets whether the Test button can be pressed.</summary>
    public bool CanTest => !_isBusy;

    /// <summary>Gets whether the Start container button is offered: a container environment whose last test found the container stopped.</summary>
    public bool CanStartContainer => Container is not null && Status == RemoteEnvironmentStatus.NotRunning && !_isBusy;

    /// <summary>Gets whether the status is a success state.</summary>
    public bool IsGood => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Good;

    /// <summary>Gets whether the status is a caveat.</summary>
    public bool IsWarning => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Warning;

    /// <summary>Gets whether the status is a failure.</summary>
    public bool IsError => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Error;

    /// <summary>Gets whether the status is a step in progress.</summary>
    public bool IsProgress => RemoteStatusDescriber.SeverityOf(Status) == RemoteStatusSeverity.Progress;

    /// <summary>Gets the text a screen reader announces for the row: name, kind, destination and status.</summary>
    public string AccessibleName => DisplayName + ", " + KindLabel + ", " + DestinationText + ", " + StatusLabel;

    // The row that replaced this one after a reload. An operation that started on this instance applies its outcome to the live row.
    private RuntimeEnvironmentRow Live
    {
        get
        {
            var row = this;
            while (row._successor is { } next)
            {
                row = next;
            }

            return row;
        }
    }

    internal void BeginTest()
    {
        var live = Live;
        live._isTesting = true;
        live._isBusy = true;
        live.Refresh();
    }

    internal void EndTest()
    {
        var live = Live;
        live._isTesting = false;
        live._isBusy = false;
        live.Refresh();
    }

    internal void BeginStart()
    {
        var live = Live;
        live._isBusy = true;
        live.Refresh();
    }

    internal void EndStart()
    {
        var live = Live;
        live._isBusy = false;
        live.Refresh();
    }

    internal void ApplyReport(RemotePreflightReport report)
    {
        var live = Live;
        live._tested = RemoteStatusDescriber.FromReport(report);
        live._diagnoses.Clear();
        foreach (var diagnosis in report.Diagnoses)
        {
            live._diagnoses.Add(RemoteDiagnosisRow.From(diagnosis));
        }

        var parts = new List<string>();
        if (report.OmpVersion is { Length: > 0 } version)
        {
            parts.Add("omp " + version);
        }

        if (report.Platform is { Length: > 0 } platform)
        {
            parts.Add(platform);
        }

        live._environmentText = string.Join(", ", parts);
        live._isTesting = false;
        live._isBusy = false;
        live.Refresh();
    }

    // A failed start or test keeps the finding on the row with its fix.
    internal void ApplyDiagnosis(RemoteDiagnosis diagnosis)
    {
        var live = Live;
        live._tested = RemoteStatusDescriber.FromReport(new RemotePreflightReport { Succeeded = false, Diagnoses = [diagnosis] });
        live._diagnoses.Clear();
        live._diagnoses.Add(RemoteDiagnosisRow.From(diagnosis));
        live._environmentText = string.Empty;
        live._isTesting = false;
        live._isBusy = false;
        live.Refresh();
    }

    // After a start the old findings no longer hold: the row goes back to "not checked" and asks for a new test.
    internal void ApplyStarted()
    {
        var live = Live;
        live._tested = RemoteEnvironmentStatus.NotChecked;
        live._diagnoses.Clear();
        live._environmentText = string.Empty;
        live._isBusy = false;
        live.Refresh();
    }

    internal void ApplyTestFailure(string message)
    {
        var live = Live;
        live._tested = RemoteEnvironmentStatus.RuntimeUnavailable;
        live._diagnoses.Clear();
        live._diagnoses.Add(new RemoteDiagnosisRow
        {
            SeverityLabel = RemoteCopy.ErrorLabel,
            IsError = true,
            Summary = message,
            FixText = live.Container is not null
                ? "Check that podman works in a terminal ('podman ps'), then test the environment again."
                : "Check that WSL works in a terminal ('wsl --list'), then test the environment again.",
        });
        live._environmentText = string.Empty;
        live._isTesting = false;
        live._isBusy = false;
        live.Refresh();
    }

    // After a reload the row keeps the test outcome only while the settings it was made with are unchanged. An operation still in flight on the
    // earlier instance keeps its busy flags here and reports to this row.
    internal void CarryOver(RuntimeEnvironmentRow earlier)
    {
        earlier._successor = this;
        _isTesting = earlier._isTesting;
        _isBusy = earlier._isBusy;
        if (!SameTarget(earlier))
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

    private bool SameTarget(RuntimeEnvironmentRow earlier) =>
        Container is { } container
            ? earlier.Container is { } before
                && string.Equals(container.Container, before.Container, StringComparison.Ordinal)
                && string.Equals(container.RemoteUser, before.RemoteUser, StringComparison.Ordinal)
                && string.Equals(container.WorkspaceFolder, before.WorkspaceFolder, StringComparison.Ordinal)
                && string.Equals(container.PodmanPath, before.PodmanPath, StringComparison.Ordinal)
                && string.Equals(container.OmpPath, before.OmpPath, StringComparison.Ordinal)
            : earlier.Wsl is { } wasWsl
                && string.Equals(Wsl!.Distro, wasWsl.Distro, StringComparison.Ordinal)
                && string.Equals(Wsl.User, wasWsl.User, StringComparison.Ordinal)
                && string.Equals(Wsl.WorkspacePath, wasWsl.WorkspacePath, StringComparison.Ordinal)
                && string.Equals(Wsl.OmpPath, wasWsl.OmpPath, StringComparison.Ordinal);

    private void Refresh()
    {
        NotifyPropertyChanged(nameof(Status));
        NotifyPropertyChanged(nameof(StatusLabel));
        NotifyPropertyChanged(nameof(StatusDetail));
        NotifyPropertyChanged(nameof(IsBusy));
        NotifyPropertyChanged(nameof(IsTesting));
        NotifyPropertyChanged(nameof(CanTest));
        NotifyPropertyChanged(nameof(CanStartContainer));
        NotifyPropertyChanged(nameof(IsGood));
        NotifyPropertyChanged(nameof(IsWarning));
        NotifyPropertyChanged(nameof(IsError));
        NotifyPropertyChanged(nameof(IsProgress));
        NotifyPropertyChanged(nameof(AccessibleName));
        NotifyPropertyChanged(nameof(HasDiagnoses));
        NotifyPropertyChanged(nameof(EnvironmentText));
    }
}
