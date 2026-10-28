using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Everything a head needs to run one session inside a podman container, made from the plan it already made for a local session: the podman transport factory that
/// <c>InProcessEngineHost.ForAcp</c> takes, the <see cref="AcpClientOptions"/> adjusted for omp in a container, the container file system for the explorer and the
/// editor, and the convention discovery of the container workspace. The home directory is read from the container once. Disposing it releases the exec layer.
/// A container has no ssh connection, so loopback services of this machine are not forwarded: an MCP server on a loopback address stays in the launch, reported as
/// probably unreachable.
/// </summary>
public sealed class ContainerSessionResources : IEnvironmentSessionResources
{
    private ContainerSessionResources(
        ContainerProfile profile,
        PodmanClientOptions clientOptions,
        RemoteLaunch launch,
        Func<AcpClientOptions, IAcpTransport> transportFactory)
    {
        Profile = profile;
        Exec = new PodmanRemoteExec(profile, clientOptions);
        Launch = launch;
        TransportFactory = transportFactory;
        FileSystem = new RemoteWorkspaceFileSystem(Exec);
    }

    /// <summary>Gets the container.</summary>
    public ContainerProfile Profile { get; }

    /// <summary>Gets the exec layer of the container: the transport of the file system and of the convention discovery.</summary>
    public PodmanRemoteExec Exec { get; }

    /// <summary>Gets how omp starts in the container.</summary>
    public RemoteLaunch Launch { get; }

    /// <summary>Gets the factory for <c>InProcessEngineHost.ForAcp(..., transportFactory)</c>: every transport it makes runs a new <c>podman exec</c> with this launch.</summary>
    public Func<AcpClientOptions, IAcpTransport> TransportFactory { get; }

    /// <summary>Gets the container workspace files, for the explorer, the editor and the diff review (use it through <c>WorkspaceTree.OpenFolderAsync</c>).</summary>
    public RemoteWorkspaceFileSystem FileSystem { get; }

    /// <summary>
    /// Makes the resources of a session. It resolves the home directory in the container (one short podman call), rewrites the plan for the container and builds the
    /// transport factory. The profile and the launch are validated here, so an unsafe value fails before any session exists.
    /// </summary>
    /// <param name="profile">The container.</param>
    /// <param name="plan">The launch plan made for a local session.</param>
    /// <param name="launchOptions">The session identifier, the local state root and the plugin mappings. <see cref="RemoteLaunchOptions.RemoteHomeDirectory"/> is replaced by the home directory read from the container and <see cref="RemoteLaunchOptions.CarryLoopbackServices"/> is turned off.</param>
    /// <param name="secretEnvironment">Provider tokens for omp (they travel on the stdin of <c>podman exec -i</c>, never on a command line), or <see langword="null"/> to rely on omp's own login in the container.</param>
    /// <param name="clientOptions">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <param name="cancellationToken">Cancels the home directory lookup.</param>
    /// <returns>The resources. Dispose them when the session ends.</returns>
    /// <exception cref="ArgumentException">The profile, the plan or the options are not safe to turn into a podman command.</exception>
    /// <exception cref="IOException">The container did not answer or its home directory could not be read.</exception>
    public static async Task<ContainerSessionResources> CreateAsync(
        ContainerProfile profile,
        OmpLaunchPlan plan,
        RemoteLaunchOptions launchOptions,
        IReadOnlyDictionary<string, string>? secretEnvironment,
        PodmanClientOptions? clientOptions,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(launchOptions);
        ExecScriptRules.RequireValid(profile);

        var effective = clientOptions ?? new PodmanClientOptions();
        string home;
        using (var probe = new PodmanRemoteExec(profile, effective))
        {
            home = await probe.ResolveHomeDirectoryAsync(cancellationToken).ConfigureAwait(false)
                ?? throw new IOException("The home directory of the user in the container could not be read: the container did not answer or has no usable $HOME.");
        }

        var launch = new RemoteLaunchMaterializer().Materialize(
            plan,
            ToSshShape(profile),
            launchOptions with { RemoteHomeDirectory = home, CarryLoopbackServices = false },
            secretEnvironment);
        var factory = PodmanAcpTransportFactory.Create(profile, launch, new PodmanExecCommandBuilder(effective), loggerFactory);
        return new ContainerSessionResources(profile, effective, launch, factory);
    }

    /// <summary>Adjusts the ACP options of a session for omp in a container (see <see cref="ExecSessionOptions.Apply"/>).</summary>
    /// <param name="options">The options a local session would use.</param>
    /// <returns>The options for the session in the container.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    public AcpClientOptions ApplyTo(AcpClientOptions options) => ExecSessionOptions.Apply(Launch, options);

    /// <summary>Discovers the commands, skills, rules and subagents of the container workspace and of the home directory in the container.</summary>
    /// <param name="cancellationToken">Cancels the discovery.</param>
    /// <returns>The file trees, the roots and the home directory; call <see cref="RemoteConventionSetup.DiscoverAsync"/> on it.</returns>
    public Task<RemoteConventionSetup> CreateConventionsAsync(CancellationToken cancellationToken) =>
        RemoteConventionSources.CreateAsync(Exec, null, cancellationToken);

    /// <summary>Releases the exec layer.</summary>
    public void Dispose() => Exec.Dispose();

    // The launch materializer reads the working directory, the omp path and a label from a profile; the container's values go into the same shape.
    private static SshProfile ToSshShape(ContainerProfile profile) =>
        new() { Alias = profile.Container, RemoteWorkspaceRoot = profile.WorkspaceFolder, RemoteOmpPath = profile.OmpPath };
}
