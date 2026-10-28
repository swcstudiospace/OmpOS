using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// The form in which a container environment is added or edited. It edits text fields, shows the problems of the current values live (the rules of
/// <see cref="EnvironmentProfileValidator"/>) and builds the profile only when there are none. It has no field for a container runtime other than podman and none
/// for a secret. A new form can start from a running container (<see cref="ForNew"/> with a name and a folder).
/// </summary>
public sealed class ContainerProfileEditor : ObservableModel
{
    private readonly string? _existingId;
    private readonly IReadOnlySet<string> _takenIds;
    private readonly ObservableCollection<RemoteFieldIssue> _issues = [];
    private string _displayName = string.Empty;
    private string _container = string.Empty;
    private string _remoteUser = string.Empty;
    private string _workspaceFolder = string.Empty;
    private string _podmanPath = string.Empty;
    private string _ompPath = string.Empty;

    private ContainerProfileEditor(string? existingId, IReadOnlySet<string> takenIds)
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
    public string Heading => IsNew ? ContainerCopy.AddContainerHeading : ContainerCopy.EditContainerHeading;

    /// <summary>Gets whether every field is acceptable.</summary>
    public bool IsValid => _issues.Count == 0;

    /// <summary>Gets or sets the name shown in lists and in the New session picker.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => Change(ref _displayName, value);
    }

    /// <summary>Gets or sets the podman container name or id.</summary>
    public string Container
    {
        get => _container;
        set => Change(ref _container, value);
    }

    /// <summary>Gets or sets the user omp runs as inside the container, or an empty string for the container's default user.</summary>
    public string RemoteUser
    {
        get => _remoteUser;
        set => Change(ref _remoteUser, value);
    }

    /// <summary>Gets or sets the absolute folder inside the container that omp works in.</summary>
    public string WorkspaceFolder
    {
        get => _workspaceFolder;
        set => Change(ref _workspaceFolder, value);
    }

    /// <summary>Gets or sets the path of the podman executable on this computer, or an empty string for <c>podman</c> from <c>PATH</c>.</summary>
    public string PodmanPath
    {
        get => _podmanPath;
        set => Change(ref _podmanPath, value);
    }

    /// <summary>Gets or sets the omp executable inside the container (an absolute path or a command name), or an empty string for <c>omp</c> from the container's <c>PATH</c>.</summary>
    public string OmpPath
    {
        get => _ompPath;
        set => Change(ref _ompPath, value);
    }

    /// <summary>Starts the form for a new container environment.</summary>
    /// <param name="takenIds">The identifiers of the saved environments, so the new one gets its own.</param>
    /// <param name="container">The container name to start from (for example a running container the user picked), or <see langword="null"/> for an empty form.</param>
    /// <param name="workspaceFolder">The folder inside the container to start from, or <see langword="null"/>.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="takenIds"/> is null.</exception>
    public static ContainerProfileEditor ForNew(IReadOnlySet<string> takenIds, string? container = null, string? workspaceFolder = null)
    {
        ArgumentNullException.ThrowIfNull(takenIds);
        var editor = new ContainerProfileEditor(null, takenIds);
        if (container is not null)
        {
            editor._container = container;
            editor._displayName = container;
        }

        if (workspaceFolder is not null)
        {
            editor._workspaceFolder = workspaceFolder;
        }

        editor.Recompute();
        return editor;
    }

    /// <summary>Starts the form for an existing environment.</summary>
    /// <param name="profile">The environment. It must be a container environment.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The environment is not a container environment.</exception>
    public static ContainerProfileEditor ForEdit(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Container is not { } container)
        {
            throw new ArgumentException("Only a container environment can be edited here.", nameof(profile));
        }

        var editor = new ContainerProfileEditor(profile.Id, new HashSet<string>(StringComparer.Ordinal))
        {
            _displayName = profile.DisplayName,
            _container = container.Container,
            _remoteUser = container.RemoteUser ?? string.Empty,
            _workspaceFolder = container.WorkspaceFolder,
            _podmanPath = container.PodmanPath ?? string.Empty,
            _ompPath = container.OmpPath ?? string.Empty,
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

    // The validator names the profile properties; the identifier is derived from the container name, so a problem with it is shown on that field.
    private static string FieldOf(string property)
    {
        const string Prefix = "Container.";
        var name = property.StartsWith(Prefix, StringComparison.Ordinal) ? property[Prefix.Length..] : property;
        return string.Equals(name, nameof(EnvironmentProfile.Id), StringComparison.Ordinal) ? nameof(Container) : name;
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
        Id = _existingId ?? EnvironmentIds.Make("container-", _container.Trim(), _takenIds),
        DisplayName = _displayName.Trim(),
        Kind = EnvironmentKind.Container,
        Container = new ContainerProfile
        {
            Container = _container.Trim(),
            RemoteUser = EnvironmentIds.NullIfBlank(_remoteUser),
            WorkspaceFolder = _workspaceFolder.Trim(),
            PodmanPath = EnvironmentIds.NullIfBlank(_podmanPath),
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
