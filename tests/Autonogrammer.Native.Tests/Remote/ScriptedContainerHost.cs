using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>The Containers and WSL host of the Remote page, scripted: it answers a test with the report the test chose and records what it was asked.</summary>
internal sealed class ScriptedContainerHost : IContainerEnvironmentsHost
{
    /// <summary>Gets or sets whether WSL environments are supported.</summary>
    public bool Wsl { get; set; } = true;

    /// <summary>Gets or sets what a connection test returns.</summary>
    public RemotePreflightReport Report { get; set; } = new() { Succeeded = true, Diagnoses = [], OmpVersion = "18.12.6", Platform = "Linux x86_64" };

    /// <summary>Gets the containers tested, by name.</summary>
    public List<string> Tested { get; } = [];

    /// <inheritdoc/>
    public bool SupportsWsl => Wsl;

    /// <inheritdoc/>
    public Task<RemotePreflightReport> TestContainerAsync(ContainerProfile profile, CancellationToken cancellationToken)
    {
        Tested.Add(profile.Container);
        return Task.FromResult(Report);
    }

    /// <inheritdoc/>
    public Task<RemotePreflightReport> TestWslAsync(WslProfile profile, CancellationToken cancellationToken)
    {
        Tested.Add(profile.Distro);
        return Task.FromResult(Report);
    }

    /// <inheritdoc/>
    public Task<RemoteDiagnosis?> StartContainerAsync(ContainerProfile profile, CancellationToken cancellationToken) => Task.FromResult<RemoteDiagnosis?>(null);
}
