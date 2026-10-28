using OmpOS.Contracts.Environments;
using OmpOS.Presentation.Remote;

namespace OmpOS.Native.Tests.Remote;

/// <summary>A scripted <see cref="IRemoteEnvironmentsHost"/> for the view tests: it keeps the profiles in memory and answers every call at once.</summary>
internal sealed class ScriptedRemoteHost : IRemoteEnvironmentsHost
{
    private readonly List<RemoteStoredProfile> _profiles = [];

    /// <summary>Gets or sets the aliases of the fake ssh config.</summary>
    public List<string> ConfigAliases { get; set; } = [];

    /// <summary>Gets or sets what the connection test returns.</summary>
    public RemotePreflightReport Report { get; set; } = new() { Succeeded = true, Diagnoses = [], OmpVersion = "18.12.6", Platform = "Linux x86_64" };

    /// <summary>Gets the number of connection tests run.</summary>
    public int TestCalls { get; private set; }

    /// <summary>Gets the hosts a trust terminal was opened for.</summary>
    public List<SshProfile> TrustRequests { get; } = [];

    /// <summary>Gets the saved profiles.</summary>
    public IReadOnlyList<RemoteStoredProfile> Profiles => _profiles;

    /// <summary>Adds a saved ssh environment.</summary>
    /// <param name="id">The identifier.</param>
    /// <param name="alias">The ssh alias.</param>
    /// <returns>This host, for chaining.</returns>
    public ScriptedRemoteHost With(string id, string alias)
    {
        _profiles.Add(new RemoteStoredProfile(
            new EnvironmentProfile
            {
                Id = id,
                DisplayName = "Box " + id,
                Kind = EnvironmentKind.Ssh,
                Ssh = new SshProfile { Alias = alias, RemoteWorkspaceRoot = "/srv/app" },
            },
            LastUsedUtc: null));
        return this;
    }

    /// <inheritdoc/>
    public Task<RemoteProfileList> LoadProfilesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RemoteProfileList([.. _profiles], null));

    /// <inheritdoc/>
    public Task<RemoteHostCandidates> ListSshConfigHostsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RemoteHostCandidates([.. ConfigAliases], []));

    /// <inheritdoc/>
    public Task SaveProfileAsync(EnvironmentProfile profile, CancellationToken cancellationToken)
    {
        _profiles.RemoveAll(existing => string.Equals(existing.Profile.Id, profile.Id, StringComparison.Ordinal));
        _profiles.Add(new RemoteStoredProfile(profile, null));
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken) =>
        Task.FromResult(_profiles.RemoveAll(existing => string.Equals(existing.Profile.Id, profileId, StringComparison.Ordinal)) > 0);

    /// <inheritdoc/>
    public Task<RemotePreflightReport> TestConnectionAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        TestCalls++;
        return Task.FromResult(Report);
    }

    /// <inheritdoc/>
    public Task<bool> OpenHostTrustTerminalAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        TrustRequests.Add(profile);
        return Task.FromResult(true);
    }
}
