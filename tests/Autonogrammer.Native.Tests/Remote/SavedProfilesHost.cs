using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Native.Tests.Remote;

/// <summary>An environment host that serves a fixed list of saved profiles. The Omp provisioner reads nothing else, so the other members refuse.</summary>
internal sealed class SavedProfilesHost(params RemoteStoredProfile[] profiles) : IRemoteEnvironmentsHost
{
    /// <inheritdoc />
    public Task<RemoteProfileList> LoadProfilesAsync(CancellationToken cancellationToken) => Task.FromResult(new RemoteProfileList(profiles, null));

    /// <inheritdoc />
    public Task<RemoteHostCandidates> ListSshConfigHostsAsync(CancellationToken cancellationToken) => throw new NotSupportedException("The Omp provisioner reads the saved profiles only.");

    /// <inheritdoc />
    public Task SaveProfileAsync(EnvironmentProfile profile, CancellationToken cancellationToken) => throw new NotSupportedException("The Omp provisioner reads the saved profiles only.");

    /// <inheritdoc />
    public Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken) => throw new NotSupportedException("The Omp provisioner reads the saved profiles only.");

    /// <inheritdoc />
    public Task<RemotePreflightReport> TestConnectionAsync(SshProfile profile, CancellationToken cancellationToken) => throw new NotSupportedException("The Omp provisioner reads the saved profiles only.");

    /// <inheritdoc />
    public Task<bool> OpenHostTrustTerminalAsync(SshProfile profile, CancellationToken cancellationToken) => throw new NotSupportedException("The Omp provisioner reads the saved profiles only.");
}
