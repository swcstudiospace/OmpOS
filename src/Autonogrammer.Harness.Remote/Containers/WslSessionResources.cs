using Microsoft.Extensions.Logging;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Everything a head needs to run one session in a WSL distribution, made from the plan it already made for a local session: the wsl.exe transport factory that
/// <c>InProcessEngineHost.ForAcp</c> takes, the <see cref="AcpClientOptions"/> adjusted for omp in a WSL distribution, the distribution file system for the explorer and the
/// editor, and the convention discovery of the distribution workspace. The home directory is read from the distribution once. Disposing it releases the exec layer.
/// A distribution has no ssh connection, so loopback services of this machine are not forwarded: an MCP server on a loopback address stays in the launch, reported as
/// probably unreachable.
/// </summary>
public sealed class WslSessionResources : IEnvironmentSessionResources
{
    private WslSessionResources(
        WslProfile profile,
        WslClientOptions clientOptions,
        RemoteLaunch launch,
        Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        Profile = profile;
        Exec = new WslRemoteExec(profile, clientOptions);
        Launch = launch;
        TransportFactory = transportFactory;
        FileSystem = new RemoteWorkspaceFileSystem(Exec);
    }

    /// <summary>Gets the distribution.</summary>
    public WslProfile Profile { get; }

    /// <summary>Gets the exec layer of the distribution: the transport of the file system and of the convention discovery.</summary>
    public WslRemoteExec Exec { get; }

    /// <summary>Gets how omp starts in the distribution.</summary>
    public RemoteLaunch Launch { get; }

    /// <summary>Gets the factory for <c>InProcessEngineHost.ForAcp(..., transportFactory)</c>: every transport it makes runs a new <c>wsl.exe</c> with this launch.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory { get; }

    /// <summary>Gets the distribution workspace files, for the explorer, the editor and the diff review (use it through <c>WorkspaceTree.OpenFolderAsync</c>).</summary>
    public RemoteWorkspaceFileSystem FileSystem { get; }

    /// <summary>
    /// Makes the resources of a session. It resolves the home directory in the distribution (one short wsl.exe call), rewrites the plan for the distribution and builds the
    /// transport factory. The profile and the launch are validated here, so an unsafe value fails before any session exists.
    /// </summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="plan">The launch plan made for a local session.</param>
    /// <param name="launchOptions">The session identifier, the local state root and the plugin mappings. <see cref="RemoteLaunchOptions.RemoteHomeDirectory"/> is replaced by the home directory read from the distribution and <see cref="RemoteLaunchOptions.CarryLoopbackServices"/> is turned off.</param>
    /// <param name="secretEnvironment">Provider tokens for omp (they travel on the stdin of <c>wsl.exe</c>, never on a command line), or <see langword="null"/> to rely on omp's own login in the distribution.</param>
    /// <param name="clientOptions">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <param name="cancellationToken">Cancels the home directory lookup.</param>
    /// <returns>The resources. Dispose them when the session ends.</returns>
    /// <exception cref="ArgumentException">The profile, the plan or the options are not safe to turn into a wsl.exe command.</exception>
    /// <exception cref="IOException">The distribution did not answer or its home directory could not be read.</exception>
    public static async Task<WslSessionResources> CreateAsync(
        WslProfile profile,
        OmpLaunchPlan plan,
        RemoteLaunchOptions launchOptions,
        IReadOnlyDictionary<string, string>? secretEnvironment,
        WslClientOptions? clientOptions,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(launchOptions);
        ExecScriptRules.RequireValid(profile);

        var effective = clientOptions ?? new WslClientOptions();
        string home;
        using (var probe = new WslRemoteExec(profile, effective))
        {
            home = await probe.ResolveHomeDirectoryAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The home directory of the user in the distribution could not be read: the distribution did not answer or has no usable $HOME.");
        }

        var launch = new RemoteLaunchMaterializer().Materialize(
            plan,
            ToSshShape(profile),
            launchOptions with { RemoteHomeDirectory = home, CarryLoopbackServices = false },
            secretEnvironment);
        var factory = WslAcpTransportFactory.Create(profile, launch, new WslCommandBuilder(effective), loggerFactory);
        return new WslSessionResources(profile, effective, launch, factory);
    }

    /// <summary>Adjusts the ACP options of a session for omp in a WSL distribution (see <see cref="ExecSessionOptions.Apply"/>).</summary>
    /// <param name="options">The options a local session would use.</param>
    /// <returns>The options for the session in the distribution.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AcpClientOptions ApplyTo(AcpClientOptions options) => ExecSessionOptions.Apply(Launch, options);

    /// <summary>Discovers the commands, skills, rules and subagents of the distribution workspace and of the home directory in the distribution.</summary>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>The file trees, the roots and the home directory; call <see cref="RemoteConventionSetup.DiscoverAsync"/> on it.</returns>
    public Task<RemoteConventionSetup> CreateConventionsAsync(CancellationToken cancellationToken) =>
        RemoteConventionSources.CreateAsync(Exec, null, cancellationToken);

    /// <summary>Releases the exec layer.</summary>
    public void Dispose() => Exec.Dispose();

    // The launch materializer reads the working directory, the omp path and a label from a profile; the distribution's values go into the same shape.
    private static SshProfile ToSshShape(WslProfile profile) =>
        new() { Alias = profile.Distro, RemoteWorkspaceRoot = profile.WorkspacePath, RemoteOmpPath = profile.OmpPath };
}
