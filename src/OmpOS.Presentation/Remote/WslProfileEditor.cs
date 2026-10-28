using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Observable;

namespace OmpOS.Presentation.Remote;

/// <summary>
/// The form in which a WSL environment is added or edited. It edits text fields, shows the problems of the current values live (the rules of
/// <see cref="EnvironmentProfileValidator"/>) and builds the profile only when there are none. It has no field for a secret.
/// </summary>
public sealed class WslProfileEditor : ObservableModel
{
    private readonly string? _existingId;
    private readonly IReadOnlySet<string> _takenIds;
    private readonly ObservableCollection<RemoteFieldIssue> _issues = [];
    private string _displayName = string.Empty;
    private string _distro = string.Empty;
    private string _user = string.Empty;
    private string _workspacePath = string.Empty;
    private string _ompPath = string.Empty;

    private WslProfileEditor(string? existingId, IReadOnlySet<string> takenIds)
    {
        _existingId = existingId;
        _takenIds = takenIds;
        Issues = new ReadOnlyObservableCollection<RemoteFieldIssue>(_issues);
    }

    /// <summary>Gets the problems of the current values, one per wrong field. Empty when the environment can be saved.</summary>
    public ReadOnlyObservableCollection<RemoteFieldIssue> Issues { get; }

    /// <summary>Gets whether a new environment is being added (false when an existing one is edited).</summary>
    public bool IsNew => _existingId is null;

    /// <summary>Gets the heading of the form.</summary>
    public string Heading => IsNew ? ContainerCopy.AddWslHeading : ContainerCopy.EditWslHeading;

    /// <summary>Gets whether every field is acceptable.</summary>
    public bool IsValid => _issues.Count == 0;

    /// <summary>Gets or sets the name shown in lists and in the New session picker.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => Change(ref _displayName, value);
    }

    /// <summary>Gets or sets the WSL distribution name, as <c>wsl --list</c> prints it.</summary>
    public string Distro
    {
        get => _distro;
        set => Change(ref _distro, value);
    }

    /// <summary>Gets or sets the user omp runs as in the distribution, or an empty string for the default user.</summary>
    public string User
    {
        get => _user;
        set => Change(ref _user, value);
    }

    /// <summary>Gets or sets the absolute folder in the distribution that omp works in.</summary>
    public string WorkspacePath
    {
        get => _workspacePath;
        set => Change(ref _workspacePath, value);
    }

    /// <summary>Gets or sets the omp executable in the distribution (an absolute path or a command name), or an empty string for <c>omp</c> from the distribution's <c>PATH</c>.</summary>
    public string OmpPath
    {
        get => _ompPath;
        set => Change(ref _ompPath, value);
    }

    /// <summary>Starts the form for a new WSL environment.</summary>
    /// <param name="takenIds">The identifiers of the saved environments, so the new one gets its own.</param>
    /// <param name="distro">The distribution to start from, or <see langword="null"/> for an empty form.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="takenIds"/> is null.</exception>
    public static WslProfileEditor ForNew(IReadOnlySet<string> takenIds, string? distro = null)
    {
        ArgumentNullException.ThrowIfNull(takenIds);
        var editor = new WslProfileEditor(null, takenIds);
        if (distro is not null)
        {
            editor._distro = distro;
            editor._displayName = distro;
        }

        editor.Recompute();
        return editor;
    }

    /// <summary>Starts the form for an existing environment.</summary>
    /// <param name="profile">The environment. It must be a WSL environment.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The environment is not a WSL environment.</exception>
    public static WslProfileEditor ForEdit(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Wsl is not { } wsl)
        {
            throw new ArgumentException("Only a WSL environment can be edited here.", nameof(profile));
        }

        var editor = new WslProfileEditor(profile.Id, new HashSet<string>(StringComparer.Ordinal))
        {
            _displayName = profile.DisplayName,
            _distro = wsl.Distro,
            _user = wsl.User ?? string.Empty,
            _workspacePath = wsl.WorkspacePath,
            _ompPath = wsl.OmpPath ?? string.Empty,
        };
        editor.Recompute();
        return editor;
    }

    /// <summary>Builds the environment from the current values.</summary>
    /// <param name="profile">The environment, when there is no problem.</param>
    /// <returns><see langword="true"/> when <see cref="Issues"/> is empty and <paramref name="profile"/> is set.</returns>
    public bool TryBuild(out EnvironmentProfile? profile)
    {
        profile = null;
        Recompute();
        if (!IsValid)
        {
            return false;
        }

        profile = Candidate();
        return true;
    }

    private static string FieldOf(string property)
    {
        const string Prefix = "Wsl.";
        var name = property.StartsWith(Prefix, StringComparison.Ordinal) ? property[Prefix.Length..] : property;
        if (string.Equals(name, nameof(EnvironmentProfile.Id), StringComparison.Ordinal))
        {
            return nameof(Distro);
        }

        return string.Equals(name, nameof(WslProfile.User), StringComparison.Ordinal) ? nameof(User) : name;
    }

    private void Change(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref field, value, propertyName))
        {
            Recompute();
        }
    }

    private EnvironmentProfile Candidate() => new()
    {
        Id = _existingId ?? EnvironmentIds.Make("wsl-", _distro.Trim(), _takenIds),
        DisplayName = _displayName.Trim(),
        Kind = EnvironmentKind.Wsl,
        Wsl = new WslProfile
        {
            Distro = _distro.Trim(),
            User = EnvironmentIds.NullIfBlank(_user),
            WorkspacePath = _workspacePath.Trim(),
            OmpPath = EnvironmentIds.NullIfBlank(_ompPath),
        },
    };

    private void Recompute()
    {
        var found = new List<RemoteFieldIssue>();
        var reported = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issue in EnvironmentProfileValidator.Validate(Candidate()))
        {
            var field = FieldOf(issue.Property);
            if (reported.Add(field))
            {
                found.Add(new RemoteFieldIssue(field, issue.Message));
            }
        }

        _issues.Clear();
        foreach (var issue in found)
        {
            _issues.Add(issue);
        }

        NotifyPropertyChanged(nameof(IsValid));
    }
}
