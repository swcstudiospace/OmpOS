using Microsoft.Extensions.Logging;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// Everything a head needs to run one session on an ssh host, made from the plan it already made for a local session: the ssh transport factory that
/// <c>InProcessEngineHost.ForAcp</c> takes, the <see cref="AcpClientOptions"/> adjusted for a remote omp, the remote file system for the explorer and the
/// editor, and the convention discovery of the remote workspace. The remote home directory is read from the host once. Disposing it releases the exec layer.
/// </summary>
public sealed class RemoteSessionResources : IEnvironmentSessionResources
{
    private RemoteSessionResources(
        SshProfile profile,
        SshClientOptions clientOptions,
        RemoteLaunch launch,
        Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        Profile = profile;
        Exec = new RemoteExec(profile, clientOptions);
        Launch = launch;
        TransportFactory = transportFactory;
        FileSystem = new RemoteWorkspaceFileSystem(Exec);
    }

    /// <summary>Gets the ssh host.</summary>
    public SshProfile Profile { get; }

    /// <summary>Gets the exec layer of the host: the transport of the file system and of the convention discovery.</summary>
    public RemoteExec Exec { get; }

    /// <summary>Gets how omp starts on the host.</summary>
    public RemoteLaunch Launch { get; }

    /// <summary>Gets the factory for <c>InProcessEngineHost.ForAcp(..., transportFactory)</c>: every transport it makes opens a new ssh connection with this launch.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory { get; }

    /// <summary>Gets the remote workspace files, for the explorer, the editor and the diff review (use it through <c>WorkspaceTree.OpenFolderAsync</c>).</summary>
    public RemoteWorkspaceFileSystem FileSystem { get; }

    /// <summary>
    /// Makes the resources of a session. It resolves the remote home directory (one short ssh call), rewrites the plan for the host and builds the transport
    /// factory. The profile and the launch are validated here, so an unsafe value fails before any session exists.
    /// </summary>
    /// <param name="profile">The ssh host.</param>
    /// <param name="plan">The launch plan made for a local session.</param>
    /// <param name="launchOptions">The session identifier, the local state root and the plugin mappings. <see cref="RemoteLaunchOptions.RemoteHomeDirectory"/> is replaced by the home directory read from the host.</param>
    /// <param name="secretEnvironment">Provider tokens for the remote omp (they travel on the ssh stdin, never on a command line), or <see langword="null"/> to rely on the remote omp's own login.</param>
    /// <param name="clientOptions">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <param name="cancellationToken">Cancels the home directory lookup.</param>
    /// <returns>The resources. Dispose them when the session ends.</returns>
    /// <exception cref="ArgumentException">The profile, the plan or the options are not safe to turn into an ssh command.</exception>
    /// <exception cref="IOException">The host did not answer or its home directory could not be read.</exception>
    public static async Task<RemoteSessionResources> CreateAsync(
        SshProfile profile,
        OmpLaunchPlan plan,
        RemoteLaunchOptions launchOptions,
        IReadOnlyDictionary<string, string>? secretEnvironment,
        SshClientOptions? clientOptions,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(launchOptions);
        var effective = clientOptions ?? new SshClientOptions();
        string home;
        using (var probe = new RemoteExec(profile, effective))
        {
            home = await probe.ResolveHomeDirectoryAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The home directory of the remote user could not be read: the host did not answer or has no usable $HOME.");
        }

        var launch = new RemoteLaunchMaterializer().Materialize(plan, profile, launchOptions with { RemoteHomeDirectory = home }, secretEnvironment);
        var factory = SshAcpTransportFactory.Create(profile, launch, new SshCommandBuilder(effective), loggerFactory);
        return new RemoteSessionResources(profile, effective, launch, factory);
    }

    /// <summary>
    /// Adjusts the ACP options of a session for a remote omp: the working directory is the remote workspace, the MCP servers are the ones the launch rewrote for
    /// the remote loopback, <c>FsRoot</c> is empty so the remote omp is never served files from this machine, and the arguments and environment are empty
    /// because the launch carries them (the ssh transport does not use them).
    /// </summary>
    /// <param name="options">The options a local session would use (approval policy, handshake timeout, auth method).</param>
    /// <returns>The options for the remote session.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AcpClientOptions ApplyTo(AcpClientOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return options with
        {
            OmpPath = null,
            WorkingDirectory = Launch.WorkingDirectory,
            FsRoot = null,
            ExtraArguments = [],
            EnvironmentVariables = new Dictionary<string, string>(StringComparer.Ordinal),
            McpServers = [.. Launch.McpServers],
        };
    }

    /// <summary>Discovers the commands, skills, rules and subagents of the remote workspace and of the remote home directory.</summary>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>The file trees, the roots and the home directory; call <see cref="RemoteConventionSetup.DiscoverAsync"/> on it.</returns>
    public Task<RemoteConventionSetup> CreateConventionsAsync(CancellationToken cancellationToken) =>
        RemoteConventionSources.CreateAsync(Exec, null, cancellationToken);

    /// <summary>Releases the exec layer.</summary>
    public void Dispose() => Exec.Dispose();
}
