using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// What a session needs from the environment it runs in, whatever that environment is: the launch, the transport that starts omp there, the file system the
/// explorer and the editor read, and the convention discovery. <see cref="RemoteSessionResources"/> (ssh), <see cref="Containers.ContainerSessionResources"/>
/// (podman) and <see cref="Containers.WslSessionResources"/> (WSL) implement it, so the engine of a session is composed the same way for each. Disposing it
/// releases the exec layer; the owner disposes it after the engine host.
/// </summary>
public interface IEnvironmentSessionResources : IDisposable
{
    /// <summary>Gets the launch that was materialized for the environment.</summary>
    RemoteLaunch Launch { get; }

    /// <summary>Gets the factory <c>InProcessEngineHost.ForAcp</c> takes: it starts omp in the environment.</summary>
    Func<AcpClientOptions, IAcpTransport> TransportFactory { get; }

    /// <summary>Gets the file system of the environment's workspace, for the explorer and the editor.</summary>
    RemoteWorkspaceFileSystem FileSystem { get; }

    /// <summary>Adjusts the client options for an omp that runs in the environment.</summary>
    /// <param name="options">The options of a local omp.</param>
    /// <returns>The options for the environment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is null.</exception>
    AcpClientOptions ApplyTo(AcpClientOptions options);

    /// <summary>Creates the discovery of the custom commands, skills and subagents of the environment's workspace.</summary>
    /// <param name="cancellationToken">Cancels the setup.</param>
    /// <returns>The discovery.</returns>
    Task<RemoteConventionSetup> CreateConventionsAsync(CancellationToken cancellationToken);
}
