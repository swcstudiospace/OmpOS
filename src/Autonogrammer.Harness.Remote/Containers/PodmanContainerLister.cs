using System.Text.Json;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// Lists containers with the podman client: the running ones for the "add from a running container" picker (<c>podman ps --format json</c>) and the ones the
/// devcontainer CLI created for a folder (<c>podman ps -a --filter label=devcontainer.local_folder=...</c>). Only the identity of each container is kept (id, name,
/// image, state, the dev container folder label): never its environment. A failure is a listing with a diagnosis, not an exception.
/// </summary>
public sealed class PodmanContainerLister : IContainerLister
{
    private readonly PodmanClientOptions _options;
    private readonly PodmanExecCommandBuilder _builder;
    private readonly SshProcessRunner _runner;

    /// <summary>Initializes a new instance of the <see cref="PodmanContainerLister"/> class.</summary>
    /// <param name="options">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limit uses, or <see langword="null"/> for the system clock.</param>
    public PodmanContainerLister(PodmanClientOptions? options = null, TimeProvider? clock = null)
    {
        _options = options ?? new PodmanClientOptions();
        _builder = new PodmanExecCommandBuilder(_options);
        _runner = new SshProcessRunner(clock);
    }

    /// <inheritdoc />
    public Task<ContainerListing> ListRunningAsync(CancellationToken cancellationToken) =>
        RunAsync(_builder.BuildList(), cancellationToken);

    /// <summary>Lists the containers (running or not) the devcontainer CLI labelled with a local project folder.</summary>
    /// <param name="localFolder">The absolute path of the project folder on this machine.</param>
    /// <param name="cancellationToken">Cancels the listing and stops podman.</param>
    /// <returns>The containers, or the diagnosis of why they could not be listed.</returns>
    /// <exception cref="ArgumentException">The folder is not an absolute path without control characters.</exception>
    public Task<ContainerListing> FindForFolderAsync(string localFolder, CancellationToken cancellationToken) =>
        RunAsync(_builder.BuildFindDevContainers(localFolder), cancellationToken);

    private async Task<ContainerListing> RunAsync(SshInvocation invocation, CancellationToken cancellationToken)
    {
        var result = await _runner.RunAsync(invocation, _options.CommandTimeout, cancellationToken).ConfigureAwait(false);
        if (result.StartFailure is not null)
        {
            return Problem(RemoteDiagnosisKind.ContainerRuntimeMissing, result.StartFailure);
        }

        if (result.TimedOut)
        {
            return Problem(RemoteDiagnosisKind.ContainerRuntimeUnavailable, "podman did not answer in time.");
        }

        if (result.ExitCode != 0)
        {
            var diagnosis = ContainerDiagnosisCatalog.FromPodmanFailure(result.ExitCode, result.StandardError)
                ?? ContainerDiagnosisCatalog.Create(
                    RemoteDiagnosisKind.ContainerRuntimeUnavailable,
                    EnvironmentKind.Container,
                    RemoteDiagnosisCatalog.Excerpt(ContainerDiagnosisCatalog.Normalise(result.StandardError)));
            return new ContainerListing([], diagnosis);
        }

        try
        {
            return new ContainerListing(PodmanContainerListParser.Parse(result.StandardOutput), null);
        }
        catch (JsonException)
        {
            return Problem(RemoteDiagnosisKind.Unknown, "podman printed a container list that is not JSON.");
        }
    }

    private static ContainerListing Problem(RemoteDiagnosisKind kind, string? detail) =>
        new([], ContainerDiagnosisCatalog.Create(kind, EnvironmentKind.Container, RemoteDiagnosisCatalog.Excerpt(detail ?? string.Empty)));
}
