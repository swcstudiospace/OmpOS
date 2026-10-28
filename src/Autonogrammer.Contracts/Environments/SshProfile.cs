namespace Autonogrammer.Contracts.Environments;

/// <summary>
/// How to reach one remote machine over ssh. The profile holds references only: a host alias or address, a path to an identity
/// file, a jump host. It has no property that could carry a password, a passphrase, a private key or a token: authentication is
/// whatever OpenSSH provides (ssh-agent, identity files, certificates) and Autonogrammer never prompts for or stores a secret.
/// Use <see cref="EnvironmentProfileValidator"/> before the profile is stored or used to start a process.
/// </summary>
public sealed record SshProfile
{
    private static readonly IReadOnlyDictionary<string, string> NoOptions = new Dictionary<string, string>(StringComparer.Ordinal);

    // The source-generated deserializer passes default (null, 0) for a member a hand-edited document omits and skips property
    // initializers, so the defaults live in the getters.
    private IReadOnlyDictionary<string, string>? _options;
    private int _keepAliveSeconds;

    /// <summary>
    /// Gets the host alias: a <c>Host</c> name from <c>~/.ssh/config</c> or a label for a custom profile. It is passed to ssh as the
    /// destination when <see cref="Hostname"/> is <see langword="null"/>. It matches <see cref="SshProfileRules.IsValidAlias"/>.
    /// </summary>
    public required string Alias { get; init; }

    /// <summary>
    /// Gets the host name or address of a custom profile, or <see langword="null"/> when the alias is resolved by the user's
    /// ssh configuration. When it is set it is the destination and <see cref="Alias"/> is only a label.
    /// </summary>
    public string? Hostname { get; init; }

    /// <summary>Gets the ssh port of a custom profile, or <see langword="null"/> for the configured or default port.</summary>
    public int? Port { get; init; }

    /// <summary>Gets the remote user name of a custom profile, or <see langword="null"/> for the configured or default user.</summary>
    public string? User { get; init; }

    /// <summary>Gets the path of the identity (private key) FILE on this machine, or <see langword="null"/>. The file is never read by Autonogrammer; ssh does.</summary>
    public string? IdentityFile { get; init; }

    /// <summary>Gets the jump hosts (<c>[user@]host[:port]</c>, comma separated) the connection goes through, or <see langword="null"/>.</summary>
    public string? ProxyJump { get; init; }

    /// <summary>
    /// Gets extra ssh options by name. Only names in <see cref="SshProfileRules.AllowedOptionNames"/> with a value that passes the
    /// rule of the name are accepted: nothing that runs a command, loosens host key checking or forwards a port can be set here.
    /// </summary>
    public IReadOnlyDictionary<string, string> Options
    {
        get => _options ?? NoOptions;
        init => _options = value;
    }

    /// <summary>Gets the absolute POSIX path of the workspace directory on the remote machine: the working directory of <c>omp acp</c>.</summary>
    public required string RemoteWorkspaceRoot { get; init; }

    /// <summary>
    /// Gets the omp executable on the remote machine: an absolute path or a bare command name. <see langword="null"/> means
    /// <c>omp</c> resolved from the remote <c>PATH</c> (extended by <c>~/.bun/bin</c>, <c>~/.local/bin</c> and <c>/usr/local/bin</c>).
    /// </summary>
    public string? RemoteOmpPath { get; init; }

    /// <summary>
    /// Gets whether the ssh agent connection is forwarded to the remote machine. Defaults to <see langword="false"/>: root on the
    /// remote machine could use a forwarded agent as the user.
    /// </summary>
    public bool ForwardAgent { get; init; }

    /// <summary>
    /// Gets the ssh <c>ServerAliveInterval</c> in seconds. Defaults to <see cref="SshProfileRules.DefaultKeepAliveSeconds"/>, which is also
    /// what 0 means: a source-generated JSON deserializer passes 0 for a member the document omits, and Autonogrammer never switches keep-alive off.
    /// </summary>
    public int KeepAliveSeconds
    {
        get => _keepAliveSeconds == 0 ? SshProfileRules.DefaultKeepAliveSeconds : _keepAliveSeconds;
        init => _keepAliveSeconds = value;
    }
}
