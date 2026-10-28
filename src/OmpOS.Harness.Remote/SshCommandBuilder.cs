using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using OmpOS.Contracts.Environments;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Builds the ssh argument lists of OmpOS. Every list has the same safety shape: non-interactive (<c>BatchMode=yes</c>, so a password,
/// passphrase or host-key prompt fails at once instead of waiting), host key checking left at the user's default and never switched
/// off, no agent forwarding unless the profile asks, no X11, no local command, no remote command from the user's configuration, and
/// <c>--</c> before the destination so a destination can never be read as an option. The remote command is one POSIX shell line in
/// which every dynamic value is single-quoted. Secrets never appear in the list: they travel in <see cref="SshInvocation.Preamble"/>.
/// </summary>
public sealed partial class SshCommandBuilder
{
    /// <summary>
    /// The shell lines that make a non-interactive remote shell find omp and bun: a non-interactive ssh session often has a shorter
    /// <c>PATH</c> than a login shell. The directories are appended, so nothing earlier on the path is shadowed.
    /// </summary>
    internal const string PathSetup = "PATH=\"$PATH:$HOME/.bun/bin:$HOME/.local/bin:/usr/local/bin\"; export PATH";

    private const int MaxControlPathDirectoryLength = 80;
    private const int LowestForwardPort = 1024;
    private const int MarkerRetries = 8;

    private static readonly HashSet<string> ReservedEnvironmentNames = new(StringComparer.Ordinal)
    {
        "PATH", "IFS", "ENV", "BASH_ENV", "PS4", "SHELLOPTS", "BASHOPTS", "PROMPT_COMMAND", "CDPATH",
    };

    private readonly SshClientOptions _options;
    private readonly Func<string> _markerFactory;

    /// <summary>Initializes a new instance of the <see cref="SshCommandBuilder"/> class.</summary>
    /// <param name="options">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The connection-sharing directory is not usable.</exception>
    public SshCommandBuilder(SshClientOptions? options = null)
        : this(options, markerFactory: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SshCommandBuilder"/> class with a fixed way of choosing the end-of-section marker.</summary>
    /// <param name="options">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="markerFactory">Creates the marker line that ends a section of the stdin preamble. Null uses a random value.</param>
    internal SshCommandBuilder(SshClientOptions? options, Func<string>? markerFactory)
    {
        _options = options ?? new SshClientOptions();
        ArgumentException.ThrowIfNullOrWhiteSpace(_options.SshPath, nameof(options));
        if (_options.ControlPathDirectory is { } directory
            && (!Path.IsPathFullyQualified(directory)
                || directory.Length > MaxControlPathDirectoryLength
                || directory.Contains('%', StringComparison.Ordinal)
                || directory.Any(char.IsControl)))
        {
            throw new ArgumentException("The connection-sharing directory must be a short absolute path without '%' or control characters.", nameof(options));
        }

        _markerFactory = markerFactory ?? NewMarker;
    }

    /// <summary>Builds the ssh invocation that runs <c>omp acp</c> on the remote machine with its stdio tunnelled by ssh.</summary>
    /// <param name="profile">The remote machine.</param>
    /// <param name="launch">What to run there.</param>
    /// <returns>The invocation: the arguments never hold a secret, the preamble holds the environment and files.</returns>
    /// <exception cref="ArgumentException">The profile or the launch holds a value that is not safe to pass to ssh or to a remote shell.</exception>
    public SshInvocation BuildAcp(SshProfile profile, RemoteLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ValidateProfile(profile);
        ValidateLaunch(launch);

        var arguments = BaseArguments(profile, interactive: false, launch.ReverseForwards);
        var (script, preamble) = BuildLaunchScript(launch);
        arguments.Add("--");
        arguments.Add(Destination(profile));
        arguments.Add(WrapForRemoteShell(script));
        return new SshInvocation { FileName = _options.SshPath, Arguments = arguments, Preamble = preamble };
    }

    /// <summary>Builds the ssh invocation that runs a short POSIX script on the remote machine without a terminal (the preflight and the remote tools).</summary>
    /// <param name="profile">The remote machine.</param>
    /// <param name="script">One line of POSIX shell. It is passed to <c>sh -c</c> on the remote machine.</param>
    /// <returns>The invocation, without a preamble.</returns>
    /// <exception cref="ArgumentException">The profile is not valid or the script is empty or spans several lines.</exception>
    public SshInvocation BuildProbe(SshProfile profile, string script)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(script);
        ValidateProfile(profile);

        var arguments = BaseArguments(profile, interactive: false, []);
        arguments.Add("--");
        arguments.Add(Destination(profile));
        arguments.Add(WrapForRemoteShell(script));
        return new SshInvocation { FileName = _options.SshPath, Arguments = arguments };
    }

    /// <summary>
    /// Builds the ssh invocation for the first connection to a host: an interactive terminal (<c>-tt</c>) in which ssh may ask the user
    /// to confirm the host key. OmpOS never answers for the user and never edits <c>known_hosts</c>; run it in a terminal tab.
    /// </summary>
    /// <param name="profile">The remote machine.</param>
    /// <returns>The invocation: ssh connects, runs <c>true</c> and exits once the user has decided.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public SshInvocation BuildHostTrustTerminal(SshProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateProfile(profile);

        var arguments = BaseArguments(profile, interactive: true, []);
        arguments.Add("--");
        arguments.Add(Destination(profile));
        arguments.Add("true");
        return new SshInvocation { FileName = _options.SshPath, Arguments = arguments };
    }

    private static string Destination(SshProfile profile) => profile.Hostname ?? profile.Alias;

    private static string WrapForRemoteShell(string script) => "exec sh -c " + PosixQuoting.Quote(script);

    private static string NewMarker() => "OMPOS_EOF_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(8));

    private static void ValidateProfile(SshProfile profile)
    {
        var issues = EnvironmentProfileValidator.Validate(profile);
        if (issues.Count > 0)
        {
            throw new ArgumentException("The ssh profile is not valid: " + string.Join(" ", issues.Select(issue => issue.Property + ": " + issue.Message)), nameof(profile));
        }
    }

    private static void ValidateLaunch(RemoteLaunch launch)
    {
        if (!SshProfileRules.IsValidAbsoluteRemotePath(launch.WorkingDirectory)
            || !SshProfileRules.IsValidAbsoluteRemotePath(launch.StateDirectory)
            || !SshProfileRules.IsValidRemoteExecutable(launch.OmpPath))
        {
            throw new ArgumentException("The remote working directory, state directory and omp path must be valid remote paths.", nameof(launch));
        }

        foreach (var argument in launch.Arguments)
        {
            if (argument is null || argument.Contains('\0', StringComparison.Ordinal) || argument.Contains('\n', StringComparison.Ordinal) || argument.Contains('\r', StringComparison.Ordinal))
            {
                throw new ArgumentException("A remote omp argument must not contain NUL or a line break.", nameof(launch));
            }
        }

        foreach (var (name, value) in launch.Environment)
        {
            if (!EnvironmentNamePattern().IsMatch(name)
                || ReservedEnvironmentNames.Contains(name)
                || name.StartsWith("LD_", StringComparison.Ordinal)
                || name.StartsWith("DYLD_", StringComparison.Ordinal)
                || value is null
                || value.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("A remote environment variable has a name that is not allowed or a value with NUL.", nameof(launch));
            }
        }

        var statePrefix = launch.StateDirectory.TrimEnd('/') + "/";
        foreach (var file in launch.Files)
        {
            if (!SshProfileRules.IsValidAbsoluteRemotePath(file.Path)
                || !file.Path.StartsWith(statePrefix, StringComparison.Ordinal)
                || file.Content is null
                || file.Content.Contains('\0', StringComparison.Ordinal))
            {
                throw new ArgumentException("A remote file must lie inside the session state directory and must not contain NUL.", nameof(launch));
            }
        }

        foreach (var forward in launch.ReverseForwards)
        {
            if (forward.RemotePort is < LowestForwardPort or > 65535
                || !SshProfileRules.IsValidPort(forward.LocalPort)
                || !LoopbackHostPattern().IsMatch(forward.LocalHost))
            {
                throw new ArgumentException("A reverse forward needs a remote port of 1024 or more and a loopback local address.", nameof(launch));
            }
        }
    }

    private List<string> BaseArguments(SshProfile profile, bool interactive, IReadOnlyList<ReverseForward> forwards)
    {
        var arguments = new List<string>();
        if (_options.ConfigFile is { } configFile)
        {
            arguments.Add("-F");
            arguments.Add(configFile);
        }

        arguments.Add(interactive ? "-tt" : "-T");

        foreach (var (name, value) in EffectiveOptions(profile, interactive, forwards.Count > 0))
        {
            arguments.Add("-o");
            arguments.Add(name + "=" + value);
        }

        if (profile.Port is { } port)
        {
            arguments.Add("-p");
            arguments.Add(port.ToString(CultureInfo.InvariantCulture));
        }

        if (profile.User is { } user)
        {
            arguments.Add("-l");
            arguments.Add(user);
        }

        if (profile.IdentityFile is { } identityFile)
        {
            arguments.Add("-i");
            arguments.Add(identityFile);
        }

        if (profile.ProxyJump is { } proxyJump)
        {
            arguments.Add("-J");
            arguments.Add(proxyJump);
        }

        foreach (var forward in forwards)
        {
            arguments.Add("-R");
            arguments.Add(string.Create(CultureInfo.InvariantCulture, $"127.0.0.1:{forward.RemotePort}:{forward.LocalHost}:{forward.LocalPort}"));
        }

        return arguments;
    }

    private SortedDictionary<string, string> EffectiveOptions(SshProfile profile, bool interactive, bool hasForwards)
    {
        var seconds = Math.Clamp((int)Math.Ceiling(_options.ConnectTimeout.TotalSeconds), 1, 600);
        var effective = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["ConnectTimeout"] = seconds.ToString(CultureInfo.InvariantCulture),
            ["ServerAliveInterval"] = profile.KeepAliveSeconds.ToString(CultureInfo.InvariantCulture),
            ["ServerAliveCountMax"] = "3",
            ["ForwardAgent"] = profile.ForwardAgent ? "yes" : "no",
            ["ForwardX11"] = "no",
            ["PermitLocalCommand"] = "no",
            ["RemoteCommand"] = "none",
            ["ControlMaster"] = _options.ControlPathDirectory is null ? "no" : "auto",
        };

        if (_options.ControlPathDirectory is { } directory)
        {
            effective["ControlPersist"] = "10m";
            effective["ControlPath"] = directory.TrimEnd('/') + "/ompos-%C";
        }

        if (!interactive)
        {
            effective["BatchMode"] = "yes";
            effective["LogLevel"] = "ERROR";
        }

        if (hasForwards)
        {
            effective["ExitOnForwardFailure"] = "yes";
        }

        if (profile.IdentityFile is not null)
        {
            effective["IdentitiesOnly"] = "yes";
        }

        foreach (var (name, value) in profile.Options)
        {
            if (SshProfileRules.TryGetCanonicalOptionName(name, out var canonical))
            {
                effective[canonical] = value;
            }
        }

        return effective;
    }

    private (string Script, SshStdinPreamble? Preamble) BuildLaunchScript(RemoteLaunch launch)
    {
        var environmentText = BuildEnvironmentText(launch.Environment);
        var sections = new List<string>(launch.Files.Count + 1);
        if (environmentText is not null)
        {
            sections.Add(environmentText);
        }

        foreach (var file in launch.Files)
        {
            sections.Add(file.Content);
        }

        var marker = ChooseMarker(sections);
        var statements = new List<string> { PathSetup, "umask 077" };
        var preamble = new StringBuilder();
        if (sections.Count > 0)
        {
            statements.Add("mkdir -p " + PosixQuoting.Quote(launch.StateDirectory) + " || exit 70");
        }

        if (environmentText is not null)
        {
            statements.Add("ompos_env=$(mktemp \"${TMPDIR:-/tmp}/ompos-env." + new string('X', 6) + "\") || exit 70");
            statements.Add(ReadSection(marker, "\"$ompos_env\""));
            statements.Add(". \"$ompos_env\" || exit 70");
            statements.Add("rm -f \"$ompos_env\"");
            AppendSection(preamble, environmentText, marker);
        }

        foreach (var file in launch.Files)
        {
            statements.Add("mkdir -p " + PosixQuoting.Quote(DirectoryOf(file.Path)) + " || exit 70");
            statements.Add(ReadSection(marker, PosixQuoting.Quote(file.Path)));
            AppendSection(preamble, file.Content, marker);
        }

        statements.Add("cd " + PosixQuoting.Quote(launch.WorkingDirectory) + " || exit 71");
        var exec = new StringBuilder("exec ").Append(PosixQuoting.Quote(launch.OmpPath)).Append(" acp");
        foreach (var argument in launch.Arguments)
        {
            exec.Append(' ').Append(PosixQuoting.Quote(argument));
        }

        statements.Add(exec.ToString());
        var bytes = preamble.Length == 0 ? null : new SshStdinPreamble(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(preamble.ToString()));
        return (string.Join("; ", statements), bytes);
    }

    private static string? BuildEnvironmentText(IReadOnlyDictionary<string, string> environment)
    {
        if (environment.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var name in environment.Keys.Order(StringComparer.Ordinal))
        {
            builder.Append("export ").Append(name).Append('=').Append(PosixQuoting.QuoteValue(environment[name])).Append('\n');
        }

        return builder.ToString();
    }

    private static string ReadSection(string marker, string target) =>
        "while IFS= read -r ompos_l; do [ \"$ompos_l\" = " + PosixQuoting.Quote(marker) + " ] && break; printf '%s\\n' \"$ompos_l\"; done > " + target;

    private static void AppendSection(StringBuilder preamble, string content, string marker)
    {
        preamble.Append(content);
        if (content.Length > 0 && content[^1] != '\n')
        {
            preamble.Append('\n');
        }

        preamble.Append(marker).Append('\n');
    }

    private static string DirectoryOf(string path)
    {
        var index = path.LastIndexOf('/');
        return index <= 0 ? "/" : path[..index];
    }

    private string ChooseMarker(List<string> sections)
    {
        for (var attempt = 0; attempt < MarkerRetries; attempt++)
        {
            var candidate = _markerFactory();
            if (candidate.Length > 0 && !sections.Any(section => section.Split('\n').Contains(candidate, StringComparer.Ordinal)))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("No end-of-section marker that is absent from the content could be found.");
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnvironmentNamePattern();

    [GeneratedRegex("^(?:127\\.0\\.0\\.1|localhost|\\[::1\\])\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LoopbackHostPattern();
}
