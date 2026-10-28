using System.Collections.ObjectModel;
using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// The environment selector of the New session dialog: this computer, every saved ssh environment, and the cloud (listed, not yet available). The
/// choice is validated before a session starts: an ssh environment whose profile is invalid, or whose last test failed, cannot be chosen to start, and the
/// message says what to fix. An environment that was never tested can start: starting connects to it.
/// </summary>
public sealed class NewSessionTargetModel : ObservableModel
{
    /// <summary>The identifier of the local option.</summary>
    public const string LocalId = "local";

    /// <summary>The identifier of the cloud option.</summary>
    public const string CloudId = "cloud";

    private readonly ObservableCollection<NewSessionTargetOption> _options = [];
    private readonly Dictionary<string, RemoteEnvironmentStatus> _statuses = new(StringComparer.Ordinal);
    private NewSessionTargetOption? _selected;

    /// <summary>Initializes a new instance of the <see cref="NewSessionTargetModel"/> class with this computer selected.</summary>
    /// <param name="sshProfiles">The saved ssh environments, in the order the picker lists them.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sshProfiles"/> is null.</exception>
    public NewSessionTargetModel(IEnumerable<EnvironmentProfile> sshProfiles)
    {
        ArgumentNullException.ThrowIfNull(sshProfiles);
        Options = new ReadOnlyObservableCollection<NewSessionTargetOption>(_options);
        SetProfiles(sshProfiles);
    }

    /// <summary>Gets the options in display order: this computer, the ssh environments, the cloud.</summary>
    public ReadOnlyObservableCollection<NewSessionTargetOption> Options { get; }

    /// <summary>Gets the option that is chosen. It is never <see langword="null"/> after construction.</summary>
    public NewSessionTargetOption? Selected
    {
        get => _selected;
        private set
        {
            if (SetProperty(ref _selected, value))
            {
                NotifyPropertyChanged(nameof(Validation));
                NotifyPropertyChanged(nameof(CanStart));
            }
        }
    }

    /// <summary>Gets whether the chosen target can start a session.</summary>
    public bool CanStart => Validation.IsValid;

    /// <summary>Gets whether the choice can start a session, and the sentence that says why not or what to expect.</summary>
    public NewSessionTargetValidation Validation => Validate();

    /// <summary>Replaces the ssh environments. The choice stays when its option still exists, else this computer is chosen.</summary>
    /// <param name="sshProfiles">The saved ssh environments.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sshProfiles"/> is null.</exception>
    public void SetProfiles(IEnumerable<EnvironmentProfile> sshProfiles)
    {
        ArgumentNullException.ThrowIfNull(sshProfiles);
        var chosen = _selected?.Id;
        _options.Clear();
        _options.Add(new NewSessionTargetOption(LocalId, SessionTargetKind.Local, RemoteCopy.LocalLabel, RemoteCopy.LocalDetail, profile: null, unavailableReason: null));
        foreach (var profile in sshProfiles.Where(candidate => candidate.Kind == EnvironmentKind.Ssh && candidate.Ssh is not null))
        {
            var ssh = profile.Ssh!;
            var option = new NewSessionTargetOption(
                "ssh:" + profile.Id,
                SessionTargetKind.Ssh,
                RemoteCopy.SshPrefix + profile.DisplayName,
                (ssh.Hostname ?? ssh.Alias) + " " + RemoteCopy.RemoteFolderPrefix + ssh.RemoteWorkspaceRoot,
                profile,
                unavailableReason: null);
            if (_statuses.TryGetValue(profile.Id, out var status))
            {
                option.Status = status;
            }

            _options.Add(option);
        }

        _options.Add(new NewSessionTargetOption(CloudId, SessionTargetKind.Cloud, RemoteCopy.CloudLabel, RemoteCopy.CloudDetail, profile: null, unavailableReason: RemoteCopy.CloudUnavailable));
        Selected = _options.FirstOrDefault(option => string.Equals(option.Id, chosen, StringComparison.Ordinal)) ?? _options[0];
    }

    /// <summary>Records what is known about an ssh environment (the outcome of its last test or the state of its session), so the picker shows it and validates against it.</summary>
    /// <param name="profileId">The identifier of the environment.</param>
    /// <param name="status">The status.</param>
    /// <exception cref="ArgumentNullException"><paramref name="profileId"/> is null.</exception>
    public void SetStatus(string profileId, RemoteEnvironmentStatus status)
    {
        ArgumentNullException.ThrowIfNull(profileId);
        _statuses[profileId] = status;
        var option = _options.FirstOrDefault(candidate => string.Equals(candidate.Profile?.Id, profileId, StringComparison.Ordinal));
        if (option is not null)
        {
            option.Status = status;
            NotifyPropertyChanged(nameof(Validation));
            NotifyPropertyChanged(nameof(CanStart));
        }
    }

    /// <summary>Chooses an option.</summary>
    /// <param name="id">The identifier of the option.</param>
    /// <exception cref="ArgumentException">No option has the identifier.</exception>
    public void Select(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        Selected = _options.FirstOrDefault(option => string.Equals(option.Id, id, StringComparison.Ordinal))
            ?? throw new ArgumentException($"No option has the identifier '{id}'.", nameof(id));
    }

    /// <summary>Returns the chosen target when it can start a session.</summary>
    /// <param name="target">The target, when it can start.</param>
    /// <returns><see langword="true"/> when <see cref="Validation"/> is valid.</returns>
    public bool TryGetTarget(out SessionTarget? target)
    {
        target = null;
        if (_selected is null || !Validate().IsValid)
        {
            return false;
        }

        target = new SessionTarget(_selected.Kind, _selected.Label, _selected.Profile);
        return true;
    }

    private NewSessionTargetValidation Validate()
    {
        if (_selected is null)
        {
            return new NewSessionTargetValidation(false, RemoteCopy.TargetNone);
        }

        if (!_selected.IsAvailable)
        {
            return new NewSessionTargetValidation(false, _selected.UnavailableReason ?? RemoteCopy.TargetNone);
        }

        if (_selected.Kind != SessionTargetKind.Ssh || _selected.Profile is not { } profile)
        {
            return new NewSessionTargetValidation(true, RemoteCopy.TargetOk);
        }

        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            return new NewSessionTargetValidation(false, RemoteCopy.TargetBlockedPrefix + issues[0].Message + " Edit it in Remote environments.");
        }

        return ValidateStatus(_selected.Status);
    }

    private static NewSessionTargetValidation ValidateStatus(RemoteEnvironmentStatus? status)
    {
        if (status is not { } known)
        {
            return new NewSessionTargetValidation(true, RemoteCopy.TargetUntested);
        }

        return RemoteStatusDescriber.BlocksStart(known)
            ? new NewSessionTargetValidation(false, RemoteCopy.TargetBlockedPrefix + RemoteStatusDescriber.LabelOf(known) + ". " + RemoteStatusDescriber.DetailOf(known))
            : new NewSessionTargetValidation(true, known == RemoteEnvironmentStatus.NotChecked ? RemoteCopy.TargetUntested : RemoteCopy.TargetOk);
    }
}
