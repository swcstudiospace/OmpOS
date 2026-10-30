using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Harness.Remote;

/// <summary>
/// The desktop implementation of <see cref="IRemoteEnvironmentsHost"/> over the ssh harness: the profile store, the host list of the user's ssh
/// configuration, the connection test (<see cref="RemotePreflight"/>) and the host trust terminal. The terminal itself belongs to the head, which
/// supplies <c>openTrustTerminal</c>; without it the page tells the user to run ssh once by hand.
/// </summary>
public sealed class RemoteEnvironmentsHost : IRemoteEnvironmentsHost
{
    private readonly EnvironmentProfileStore _store;
    private readonly SshClientOptions _clientOptions;
    private readonly SshConfigReader _configReader;
    private readonly RemoteTerminalLauncher _launcher;
    private readonly Func<RemoteTerminalCommand, CancellationToken, Task<bool>>? _openTrustTerminal;
    private readonly TimeProvider? _clock;

    /// <summary>Initializes a new instance of the <see cref="RemoteEnvironmentsHost"/> class.</summary>
    /// <param name="store">The saved environments.</param>
    /// <param name="clientOptions">How ssh is run, or <see langword="null"/> for the defaults.</param>
    /// <param name="configReader">Reads the host aliases of the ssh configuration, or <see langword="null"/> for the user's <c>~/.ssh/config</c>.</param>
    /// <param name="openTrustTerminal">Opens a terminal tab that runs the given command, for example through <see cref="RemoteTerminalLauncher.StartAsync"/>; <see langword="null"/> when the head has no terminal.</param>
    /// <param name="clock">The clock the connection test time limit uses, or <see langword="null"/> for the system clock.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is null.</exception>
    public RemoteEnvironmentsHost(
        EnvironmentProfileStore store,
        SshClientOptions? clientOptions = null,
        SshConfigReader? configReader = null,
        Func<RemoteTerminalCommand, CancellationToken, Task<bool>>? openTrustTerminal = null,
        TimeProvider? clock = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _clientOptions = clientOptions ?? new SshClientOptions();
        _configReader = configReader ?? new SshConfigReader();
        _launcher = new RemoteTerminalLauncher(_clientOptions);
        _openTrustTerminal = openTrustTerminal;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<RemoteProfileList> LoadProfilesAsync(CancellationToken cancellationToken)
    {
        var listing = await _store.ListAsync(cancellationToken).ConfigureAwait(false);
        var profiles = listing.Profiles.Select(stored => new RemoteStoredProfile(stored.Profile, stored.LastUsedUtc)).ToList();
        return new RemoteProfileList(profiles, listing.Warning);
    }

    /// <inheritdoc/>
    public Task<RemoteHostCandidates> ListSshConfigHostsAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var listing = _configReader.ListHosts();
        return Task.FromResult(new RemoteHostCandidates(listing.Hosts.Select(host => host.Alias).ToList(), listing.Warnings));
    }

    /// <inheritdoc/>
    public Task SaveProfileAsync(EnvironmentProfile profile, CancellationToken cancellationToken) => _store.SaveAsync(profile, cancellationToken);

    /// <inheritdoc/>
    public Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken) => _store.DeleteAsync(profileId, cancellationToken);

    /// <inheritdoc/>
    public Task<RemotePreflightReport> TestConnectionAsync(SshProfile profile, CancellationToken cancellationToken) =>
        new RemotePreflight(_clientOptions, options: null, _clock).RunAsync(profile, cancellationToken);

    /// <inheritdoc/>
    public async Task<bool> OpenHostTrustTerminalAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        if (_openTrustTerminal is null)
        {
            return false;
        }

        var command = _launcher.BuildHostTrust(profile);
        return await _openTrustTerminal(command, cancellationToken).ConfigureAwait(false);
    }
}
