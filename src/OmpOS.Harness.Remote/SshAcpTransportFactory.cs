using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;

namespace OmpOS.Harness.Remote;

/// <summary>
/// Makes the transport factory an <see cref="OmpAcpClient"/> or <c>InProcessEngineHost</c> takes, so a session runs on a remote machine
/// instead of spawning a local omp. Every transport the factory makes opens a new ssh connection with the same launch: after a drop, the
/// next transport reconnects and the session is loaded again. The <see cref="AcpClientOptions"/> the session passes in are not used
/// to build the command: the remote launch already holds the arguments and environment, and <see cref="AcpClientOptions.FsRoot"/> should stay
/// <see langword="null"/> so the remote omp is never served files from this machine.
/// </summary>
public static class SshAcpTransportFactory
{
    /// <summary>Creates the factory.</summary>
    /// <param name="profile">The remote machine.</param>
    /// <param name="launch">What to run there, from <see cref="RemoteLaunchMaterializer.Materialize"/>.</param>
    /// <param name="builder">The ssh command builder.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <returns>A factory that makes one new <see cref="SshAcpTransport"/> per call.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into an ssh command.</exception>
    public static Func<AcpClientOptions, IAcpTransport> Create(
        SshProfile profile,
        RemoteLaunch launch,
        SshCommandBuilder builder,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(builder);

        // Building once here rejects an unsafe profile or launch before any session exists; every transport reuses the same invocation.
        var invocation = builder.BuildAcp(profile, launch);
        var redactions = launch.GetRedactionValues();
        return _ => new SshAcpTransport(invocation, redactions, loggerFactory?.CreateLogger<SshAcpTransport>());
    }
}
