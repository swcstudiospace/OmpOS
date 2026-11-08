using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>An Omp provisioner for the view tests: its check answers a fixed report, and its install reports that the lines were applied.</summary>
internal sealed class ScriptedRemoteOmp(RemoteOmpReport check) : IRemoteOmpProvisioner
{
    /// <inheritdoc />
    public Task<RemoteOmpReport> CheckAsync(string environmentId, CancellationToken cancellationToken) => Task.FromResult(check);

    /// <inheritdoc />
    public Task<RemoteOmpReport> InstallAsync(string environmentId, IReadOnlyList<string> checkedActions, CancellationToken cancellationToken) =>
        Task.FromResult(new RemoteOmpReport(RemoteOmpOutcome.Installed, [], [], null, true));
}
