using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Makes the transport factory an <see cref="OmpAcpClient"/> or <c>InProcessEngineHost</c> takes, so a session runs inside a podman container instead of spawning a
/// local omp. Every transport the factory makes runs a new <c>podman exec</c> with the same launch: after a drop (for example the container was restarted), the
/// next transport reconnects and the session is loaded again. <see cref="AcpClientOptions.FsRoot"/> should stay <see langword="null"/> so omp is never served files
/// from outside the container.
/// </summary>
public static class PodmanAcpTransportFactory
{
    /// <summary>Creates the factory.</summary>
    /// <param name="profile">The container.</param>
    /// <param name="launch">What to run there. It carries no reverse forwards.</param>
    /// <param name="builder">The podman command builder.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <returns>A factory that makes one new <see cref="PodmanAcpTransport"/> per call.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into a podman command.</exception>
    public static Func<AcpClientOptions, IAcpTransport> Create(
        ContainerProfile profile,
        RemoteLaunch launch,
        PodmanExecCommandBuilder builder,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(builder);

        // Building once here rejects an unsafe profile or launch before any session exists; every transport reuses the same invocation.
        var invocation = builder.BuildAcp(profile, launch);
        var redactions = launch.GetRedactionValues();
        return _ => new PodmanAcpTransport(invocation, redactions, loggerFactory?.CreateLogger<SshAcpTransport>());
    }
}
