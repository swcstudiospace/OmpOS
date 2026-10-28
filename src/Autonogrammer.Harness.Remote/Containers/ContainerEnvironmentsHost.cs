using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Harness.Remote.Containers;

/// <summary>
/// The desktop implementation of <see cref="IContainerEnvironmentsHost"/> over the podman and WSL harness: the container test (<see cref="ContainerPreflight"/>), the
/// WSL test (<see cref="WslPreflight"/>) and the start of a stopped container. Profiles are stored by <see cref="RemoteEnvironmentsHost"/> (the same store holds every
/// kind); the running-container picker uses <see cref="PodmanContainerLister"/> as its <see cref="IContainerLister"/>.
/// </summary>
public sealed class ContainerEnvironmentsHost : IContainerEnvironmentsHost
{
    private readonly ContainerPreflight _containers;
    private readonly WslPreflight _wsl;

    /// <summary>Initializes a new instance of the <see cref="ContainerEnvironmentsHost"/> class.</summary>
    /// <param name="podmanOptions">How podman is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="wslOptions">How wsl.exe is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="clock">The clock the time limits use, or <see langword="null"/> for the system clock.</param>
    public ContainerEnvironmentsHost(PodmanClientOptions? podmanOptions = null, WslClientOptions? wslOptions = null, TimeProvider? clock = null)
    {
        _containers = new ContainerPreflight(podmanOptions, options: null, clock);
        _wsl = new WslPreflight(wslOptions, options: null, clock);
    }

    /// <inheritdoc />
    public bool SupportsWsl => OperatingSystem.IsWindows();

    /// <inheritdoc />
    public Task<RemotePreflightReport> TestContainerAsync(ContainerProfile profile, CancellationToken cancellationToken) =>
        _containers.RunAsync(profile, cancellationToken);

    /// <inheritdoc />
    public Task<RemotePreflightReport> TestWslAsync(WslProfile profile, CancellationToken cancellationToken) =>
        SupportsWsl
            ? _wsl.RunAsync(profile, cancellationToken)
            : Task.FromResult(new RemotePreflightReport
            {
                Succeeded = false,
                Diagnoses = [ContainerDiagnosisCatalog.Create(RemoteDiagnosisKind.WslMissing, EnvironmentKind.Wsl)],
            });

    /// <inheritdoc />
    public async Task<RemoteDiagnosis?> StartContainerAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        var result = await _containers.StartAsync(profile, cancellationToken).ConfigureAwait(false);
        return result.Started ? null : result.Diagnosis;
    }
}
