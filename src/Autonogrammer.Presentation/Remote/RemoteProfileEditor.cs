using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.Remote;

/// <summary>
/// The form in which a remote environment is added or edited. It edits text fields, shows the problems of the current values live (the rules of
/// <see cref="EnvironmentProfileValidator"/> plus the number fields) and builds the profile only when there are none. It has no field for a password,
/// passphrase, key or token: authentication is whatever ssh provides. A field that is not shown (the extra ssh options of an existing profile) is kept as it was.
/// </summary>
public sealed class RemoteProfileEditor : ObservableModel
{
    private const int MaxIdLength = 64;

    private readonly string? _existingId;
    private readonly IReadOnlyDictionary<string, string> _options;
    private readonly IReadOnlySet<string> _takenIds;
    private readonly ObservableCollection<RemoteFieldIssue> _issues = [];
    private string _displayName = string.Empty;
    private string _alias = string.Empty;
    private string _hostname = string.Empty;
    private string _portText = string.Empty;
    private string _user = string.Empty;
    private string _identityFile = string.Empty;
    private string _proxyJump = string.Empty;
    private string _remoteWorkspaceRoot = string.Empty;
    private string _remoteOmpPath = string.Empty;
    private string _keepAliveText = SshProfileRules.DefaultKeepAliveSeconds.ToString(CultureInfo.InvariantCulture);
    private bool _forwardAgent;

    private RemoteProfileEditor(string? existingId, IReadOnlyDictionary<string, string> options, IReadOnlySet<string> takenIds, bool aliasIsFixed)
    {
        _existingId = existingId;
        _options = options;
        _takenIds = takenIds;
        AliasIsFixed = aliasIsFixed;
        Issues = new ReadOnlyObservableCollection<RemoteFieldIssue>(_issues);
    }

    /// <summary>Gets the problems of the current values, one per wrong field. Empty when the environment can be saved.</summary>
    public ReadOnlyObservableCollection<RemoteFieldIssue> Issues { get; }

    /// <summary>Gets whether a new environment is being added (false when an existing one is edited).</summary>
    public bool IsNew => _existingId is null;

    /// <summary>Gets whether the alias comes from the ssh config and cannot be changed here.</summary>
    public bool AliasIsFixed { get; }

    /// <summary>Gets the heading of the form.</summary>
    public string Heading => IsNew ? RemoteCopy.AddHeading : RemoteCopy.EditHeading;

    /// <summary>Gets whether every field is acceptable.</summary>
    public bool IsValid => _issues.Count == 0;

    /// <summary>Gets whether the agent forwarding warning is shown: the switch is on.</summary>
    public bool ShowForwardAgentWarning => _forwardAgent;

    /// <summary>Gets or sets the name shown in lists and in the New session picker.</summary>
    public string DisplayName
    {
        get => _displayName;
        set => Change(ref _displayName, value);
    }

    /// <summary>Gets or sets the host alias from the ssh config, or the label of a hand-entered host.</summary>
    public string Alias
    {
        get => _alias;
        set => Change(ref _alias, value);
    }

    /// <summary>Gets or sets the host name or address, or an empty string to let the ssh config resolve the alias.</summary>
    public string Hostname
    {
        get => _hostname;
        set => Change(ref _hostname, value);
    }

    /// <summary>Gets or sets the port as text, or an empty string for the configured one.</summary>
    public string PortText
    {
        get => _portText;
        set => Change(ref _portText, value);
    }

    /// <summary>Gets or sets the remote user, or an empty string for the configured one.</summary>
    public string User
    {
        get => _user;
        set => Change(ref _user, value);
    }

    /// <summary>Gets or sets the path of the identity file on this machine, or an empty string for none.</summary>
    public string IdentityFile
    {
        get => _identityFile;
        set => Change(ref _identityFile, value);
    }

    /// <summary>Gets or sets the jump hosts, or an empty string for none.</summary>
    public string ProxyJump
    {
        get => _proxyJump;
        set => Change(ref _proxyJump, value);
    }

    /// <summary>Gets or sets the absolute workspace path on the remote machine.</summary>
    public string RemoteWorkspaceRoot
    {
        get => _remoteWorkspaceRoot;
        set => Change(ref _remoteWorkspaceRoot, value);
    }

    /// <summary>Gets or sets the remote omp path, or an empty string to find omp on the remote PATH.</summary>
    public string RemoteOmpPath
    {
        get => _remoteOmpPath;
        set => Change(ref _remoteOmpPath, value);
    }

    /// <summary>Gets or sets the keep-alive interval in seconds, as text.</summary>
    public string KeepAliveText
    {
        get => _keepAliveText;
        set => Change(ref _keepAliveText, value);
    }

    /// <summary>Gets or sets whether the ssh agent is forwarded to the remote machine.</summary>
    public bool ForwardAgent
    {
        get => _forwardAgent;
        set
        {
            if (SetProperty(ref _forwardAgent, value))
            {
                NotifyPropertyChanged(nameof(ShowForwardAgentWarning));
            }
        }
    }

    /// <summary>Starts the form for a new environment.</summary>
    /// <param name="alias">The alias chosen from the ssh config (then it is fixed), or <see langword="null"/> to enter a host by hand.</param>
    /// <param name="takenIds">The identifiers of the environments that already exist, so the new identifier is unique.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="takenIds"/> is null.</exception>
    public static RemoteProfileEditor ForNew(string? alias, IReadOnlySet<string> takenIds)
    {
        ArgumentNullException.ThrowIfNull(takenIds);
        var editor = new RemoteProfileEditor(null, new Dictionary<string, string>(StringComparer.Ordinal), takenIds, aliasIsFixed: alias is not null);
        if (alias is not null)
        {
            editor._alias = alias;
            editor._displayName = alias;
        }

        editor.Recompute();
        return editor;
    }

    /// <summary>Starts the form for an existing environment.</summary>
    /// <param name="profile">The environment. It must be an ssh environment.</param>
    /// <returns>The editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="profile"/> is null.</exception>
    /// <exception cref="ArgumentException">The environment is not an ssh environment.</exception>
    public static RemoteProfileEditor ForEdit(EnvironmentProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        if (profile.Ssh is not { } ssh)
        {
            throw new ArgumentException("Only an ssh environment can be edited here.", nameof(profile));
        }

        var editor = new RemoteProfileEditor(profile.Id, ssh.Options, new HashSet<string>(StringComparer.Ordinal), aliasIsFixed: false)
        {
            _displayName = profile.DisplayName,
            _alias = ssh.Alias,
            _hostname = ssh.Hostname ?? string.Empty,
            _portText = ssh.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            _user = ssh.User ?? string.Empty,
            _identityFile = ssh.IdentityFile ?? string.Empty,
            _proxyJump = ssh.ProxyJump ?? string.Empty,
            _remoteWorkspaceRoot = ssh.RemoteWorkspaceRoot,
            _remoteOmpPath = ssh.RemoteOmpPath ?? string.Empty,
            _keepAliveText = ssh.KeepAliveSeconds.ToString(CultureInfo.InvariantCulture),
            _forwardAgent = ssh.ForwardAgent,
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

        profile = Candidate(parseIssues: null);
        return true;
    }

    private static string? NullIfBlank(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string Slug(string alias)
    {
        var builder = new StringBuilder("ssh-");
        foreach (var raw in alias)
        {
            var character = char.ToLowerInvariant(raw);
            _ = builder.Append(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-');
        }

        return builder.Length > MaxIdLength ? builder.ToString(0, MaxIdLength) : builder.ToString();
    }

    // The validator names the profile properties; the form's number fields are text, and the identifier is derived from the alias.
    private static string FieldOf(string property)
    {
        const string SshPrefix = "Ssh.";
        var name = property.StartsWith(SshPrefix, StringComparison.Ordinal) ? property[SshPrefix.Length..] : property;
        return name switch
        {
            nameof(SshProfile.Port) => nameof(PortText),
            nameof(SshProfile.KeepAliveSeconds) => nameof(KeepAliveText),
            nameof(EnvironmentProfile.Id) => nameof(Alias),
            _ => name,
        };
    }

    private void Change(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (SetProperty(ref field, value, propertyName))
        {
            Recompute();
        }
    }

    private string NewId()
    {
        if (_existingId is not null)
        {
            return _existingId;
        }

        var seed = Slug(Alias.Trim());
        var candidate = seed;
        for (var suffix = 2; _takenIds.Contains(candidate); suffix++)
        {
            var tail = "-" + suffix.ToString(CultureInfo.InvariantCulture);
            candidate = (seed.Length + tail.Length > MaxIdLength ? seed[..(MaxIdLength - tail.Length)] : seed) + tail;
        }

        return candidate;
    }

    private EnvironmentProfile Candidate(List<RemoteFieldIssue>? parseIssues)
    {
        int? port = null;
        if (NullIfBlank(_portText) is { } portText)
        {
            if (int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                port = parsed;
            }
            else
            {
                parseIssues?.Add(new RemoteFieldIssue(nameof(PortText), "The port must be a number between 1 and 65535."));
            }
        }

        var keepAlive = SshProfileRules.DefaultKeepAliveSeconds;
        if (NullIfBlank(_keepAliveText) is { } keepAliveText)
        {
            if (int.TryParse(keepAliveText, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
            {
                keepAlive = parsed;
            }
            else
            {
                parseIssues?.Add(new RemoteFieldIssue(nameof(KeepAliveText), "The keep-alive interval must be a number of seconds."));
            }
        }

        return new EnvironmentProfile
        {
            Id = NewId(),
            DisplayName = _displayName.Trim(),
            Kind = EnvironmentKind.Ssh,
            Ssh = new SshProfile
            {
                Alias = _alias.Trim(),
                Hostname = NullIfBlank(_hostname),
                Port = port,
                User = NullIfBlank(_user),
                IdentityFile = NullIfBlank(_identityFile),
                ProxyJump = NullIfBlank(_proxyJump),
                Options = _options,
                RemoteWorkspaceRoot = _remoteWorkspaceRoot.Trim(),
                RemoteOmpPath = NullIfBlank(_remoteOmpPath),
                ForwardAgent = _forwardAgent,
                KeepAliveSeconds = keepAlive,
            },
        };
    }

    private void Recompute()
    {
        var found = new List<RemoteFieldIssue>();
        var candidate = Candidate(found);
        var reported = new HashSet<string>(found.Select(issue => issue.Field), StringComparer.Ordinal);
        foreach (var issue in EnvironmentProfileValidator.Validate(candidate))
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
