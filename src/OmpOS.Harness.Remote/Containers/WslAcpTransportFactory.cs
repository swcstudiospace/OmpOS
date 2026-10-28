using Microsoft.Extensions.Logging;
using OmpOS.Contracts.Environments;
using OmpOS.Harness.Abstractions;
using OmpOS.Harness.Acp;

namespace OmpOS.Harness.Remote.Containers;

/// <summary>
/// Makes the transport factory an <see cref="OmpAcpClient"/> or <c>InProcessEngineHost</c> takes, so a session runs in a WSL distribution instead of spawning a
/// local omp. Every transport the factory makes runs a new <c>wsl.exe</c> with the same launch: after a drop (for example <c>wsl --shutdown</c>), the next
/// transport reconnects and the session is loaded again. <see cref="AcpClientOptions.FsRoot"/> should stay <see langword="null"/> so omp is never served files
/// from outside the distribution.
/// </summary>
public static class WslAcpTransportFactory
{
    /// <summary>Creates the factory.</summary>
    /// <param name="profile">The distribution.</param>
    /// <param name="launch">What to run there. It carries no reverse forwards.</param>
    /// <param name="builder">The wsl.exe command builder.</param>
    /// <param name="loggerFactory">Creates the transport loggers, or <see langword="null"/> to discard their output.</param>
    /// <returns>A factory that makes one new <see cref="WslAcpTransport"/> per call.</returns>
    /// <exception cref="ArgumentException">The profile or the launch is not safe to turn into a wsl.exe command.</exception>
    public static Func<AcpClientOptions, IAcpTransport> Create(
        WslProfile profile,
        RemoteLaunch launch,
        WslCommandBuilder builder,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(launch);
        ArgumentNullException.ThrowIfNull(builder);

        var invocation = builder.BuildAcp(profile, launch);
        var redactions = launch.GetRedactionValues();
        return _ => new WslAcpTransport(invocation, redactions, loggerFactory?.CreateLogger<SshAcpTransport>());
    }
}
