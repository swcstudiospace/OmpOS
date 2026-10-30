using Autonogrammer.Contracts.Environments;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Builds the podman argument lists of Autonogrammer. A session in a container runs <c>podman exec -i [-u user] -- &lt;container&gt; sh -c '&lt;script&gt;'</c>: the script is the
/// same POSIX bootstrap that the ssh builder sends (<c>PATH</c>, environment and files read from stdin, <c>cd</c>, <c>exec omp acp</c>) with the same quoting, so a
/// container behaves like a remote machine. The arguments are a list (no local shell), a hostile container name cannot become an option (the profile rules refuse a
/// leading <c>-</c> and <c>--</c> ends the options), and secrets never appear in the list: they travel in <see cref="SshInvocation.Preamble"/> on the stdin of
/// <c>podman exec -i</c>. The working directory is entered by the script (<c>cd</c> with exit code 71), not by <c>-w</c>, so a missing folder is reported by the same
/// diagnosis as on ssh. The container runtime is podman only.
/// </summary>
public sealed class PodmanExecCommandBuilder
{
    private const string FormatTemplate = "{{.State.Status}}";

    private readonly PodmanClientOptions _options;
    private readonly SshCommandBuilder _bootstrap;

    /// <summary>Initializes a new instance of the <see cref="PodmanExecCommandBuilder"/> class.</summary>
    /// <param name="options">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <exception cref="ArgumentException">The podman path is not usable.</exception>
    public PodmanExecCommandBuilder(PodmanClientOptions? options = null)
        : this(options, markerFactory: null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PodmanExecCommandBuilder"/> class with a fixed way of choosing the end-of-section marker.</summary>
    /// <param name="options">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="markerFactory">Creates the marker line that ends a section of the stdin preamble. Null uses a random value.</param>
    internal PodmanExecCommandBuilder(PodmanClientOptions? options, Func<string>? markerFactory)
    {
        _options = options ?? new PodmanClientOptions();
        if (!ContainerProfileRules.IsValidPodmanPath(_options.PodmanPath))
        {
            throw new ArgumentException("The podman path must be a plain command name or a full path.", nameof(options));
        }

        _bootstrap = new SshCommandBuilder(options: null, markerFactory);
    }

    /// <summary>Builds the podman invocation that runs <c>omp acp</c> inside the container with its stdio carried by <c>podman exec -i</c>.</summary>
    /// <param name="profile">The container.</param>
    /// <param name="launch">What to run there. It carries no reverse forwards.</param>
    /// <returns>The invocation: the arguments never hold a secret, the preamble holds the environment and files.</returns>
    /// <exception cref="ArgumentException">The profile or the launch holds a value that is not safe to pass to podman or to a shell, or the launch has reverse forwards.</exception>
    public SshInvocation BuildAcp(ContainerProfile profile, RemoteLaunch launch)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ExecScriptRules.RequireValid(profile);
        ExecScriptRules.RequireNoForwards(launch);
        var (script, preamble) = _bootstrap.BuildBootstrap(launch);
        return new SshInvocation { FileName = ExecutableOf(profile), Arguments = ExecArguments(profile, interactive: false, script), Preamble = preamble };
    }

    /// <summary>Builds the podman invocation that runs a short POSIX script inside the container without a terminal (the preflight and the file system).</summary>
    /// <param name="profile">The container.</param>
    /// <param name="script">One line of POSIX shell. It is passed to <c>sh -c</c> inside the container.</param>
    /// <returns>The invocation, without a preamble.</returns>
    /// <exception cref="ArgumentException">The profile is not valid or the script is blank or spans several lines.</exception>
    public SshInvocation BuildProbe(ContainerProfile profile, string script)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireOneLine(script);
        ExecScriptRules.RequireValid(profile);
        return new SshInvocation { FileName = ExecutableOf(profile), Arguments = ExecArguments(profile, interactive: false, script) };
    }

    /// <summary>
    /// Builds the podman invocation of an interactive terminal inside the container: <c>podman exec -it</c> and a short POSIX script that runs through <c>sh -c</c>.
    /// Nothing secret may be in the script: stdin is the user's keyboard, so no preamble can travel.
    /// </summary>
    /// <param name="profile">The container.</param>
    /// <param name="script">One line of POSIX shell, for example <c>cd '/work' &amp;&amp; exec "${SHELL:-/bin/sh}" -l</c>.</param>
    /// <returns>The invocation, without a preamble.</returns>
    /// <exception cref="ArgumentException">The profile is not valid, or the script is blank or spans several lines.</exception>
    public SshInvocation BuildTerminal(ContainerProfile profile, string script)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireOneLine(script);
        ExecScriptRules.RequireValid(profile);
        return new SshInvocation { FileName = ExecutableOf(profile), Arguments = ExecArguments(profile, interactive: true, script) };
    }

    /// <summary>Builds <c>podman --version</c>: the check that podman is installed and can be started.</summary>
    /// <param name="podmanPath">The podman path of the profile, or <see langword="null"/> for the configured one.</param>
    /// <returns>The invocation.</returns>
    /// <exception cref="ArgumentException">The path is not usable.</exception>
    public SshInvocation BuildVersion(string? podmanPath = null) =>
        new() { FileName = ExecutableOf(podmanPath), Arguments = ["--version"] };

    /// <summary>Builds <c>podman container inspect --format '{{.State.Status}}' -- &lt;container&gt;</c>: it prints <c>running</c>, <c>exited</c>, <c>created</c> or <c>paused</c>, or fails when the container does not exist.</summary>
    /// <param name="profile">The container.</param>
    /// <returns>The invocation.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public SshInvocation BuildInspect(ContainerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);
        return new SshInvocation { FileName = ExecutableOf(profile), Arguments = ["container", "inspect", "--format", FormatTemplate, "--", profile.Container] };
    }

    /// <summary>Builds <c>podman start -- &lt;container&gt;</c>: the action the user chooses to start a stopped container.</summary>
    /// <param name="profile">The container.</param>
    /// <returns>The invocation.</returns>
    /// <exception cref="ArgumentException">The profile is not valid.</exception>
    public SshInvocation BuildStart(ContainerProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ExecScriptRules.RequireValid(profile);
        return new SshInvocation { FileName = ExecutableOf(profile), Arguments = ["start", "--", profile.Container] };
    }

    /// <summary>Builds <c>podman ps --format json</c>: the running containers, for the "add from a running container" picker.</summary>
    /// <param name="podmanPath">The podman path of the profile, or <see langword="null"/> for the configured one.</param>
    /// <returns>The invocation.</returns>
    /// <exception cref="ArgumentException">The path is not usable.</exception>
    public SshInvocation BuildList(string? podmanPath = null) =>
        new() { FileName = ExecutableOf(podmanPath), Arguments = ["ps", "--format", "json"] };

    /// <summary>
    /// Builds <c>podman ps -a --filter label=devcontainer.local_folder=&lt;folder&gt; --format json</c>: the containers the devcontainer CLI created for a local
    /// folder (it labels them with the folder), running or not.
    /// </summary>
    /// <param name="localFolder">The absolute path of the project folder on this machine.</param>
    /// <param name="podmanPath">The podman path to use, or <see langword="null"/> for the configured one.</param>
    /// <returns>The invocation.</returns>
    /// <exception cref="ArgumentException">The folder is not an absolute path without control characters, or the podman path is not usable.</exception>
    public SshInvocation BuildFindDevContainers(string localFolder, string? podmanPath = null)
    {
        if (!SshProfileRules.IsValidLocalPath(localFolder) || !Path.IsPathFullyQualified(localFolder))
        {
            throw new ArgumentException("The folder must be an absolute path without control characters.", nameof(localFolder));
        }

        return new SshInvocation
        {
            FileName = ExecutableOf(podmanPath),
            Arguments = ["ps", "-a", "--filter", "label=" + DevContainerResolver.LocalFolderLabel + "=" + localFolder, "--format", "json"],
        };
    }

    private static List<string> ExecArguments(ContainerProfile profile, bool interactive, string script)
    {
        var arguments = new List<string> { "exec", interactive ? "-it" : "-i" };
        if (profile.RemoteUser is { } user)
        {
            arguments.Add("-u");
            arguments.Add(user);
        }

        arguments.Add("--");
        arguments.Add(profile.Container);
        arguments.Add("sh");
        arguments.Add("-c");
        arguments.Add(script);
        return arguments;
    }

    private string ExecutableOf(ContainerProfile profile) => ExecutableOf(profile.PodmanPath);

    private string ExecutableOf(string? podmanPath)
    {
        if (podmanPath is null)
        {
            return _options.PodmanPath;
        }

        return ContainerProfileRules.IsValidPodmanPath(podmanPath)
            ? podmanPath
            : throw new ArgumentException("The podman path must be a plain command name or a full path.", nameof(podmanPath));
    }
}
