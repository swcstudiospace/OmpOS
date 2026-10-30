using Autonogrammer.Contracts.Environments;
using Autonogrammer.Presentation.Remote;

namespace Autonogrammer.Presentation.Tests;

/// <summary>An in-memory <see cref="IRemoteEnvironmentsHost"/> that counts what the page asked of it and returns what a test scripted.</summary>
internal sealed class FakeRemoteEnvironmentsHost : IRemoteEnvironmentsHost
{
    private readonly List<RemoteStoredProfile> _profiles = [];

    /// <summary>Gets or sets the aliases of the fake ssh config.</summary>
    public List<string> ConfigAliases { get; set; } = [];

    /// <summary>Gets or sets the warnings of the fake ssh config.</summary>
    public List<string> ConfigWarnings { get; set; } = [];

    /// <summary>Gets or sets the warning the profile list carries.</summary>
    public string? ProfileWarning { get; set; }

    /// <summary>Gets or sets the exception <see cref="LoadProfilesAsync"/> throws, or <see langword="null"/>.</summary>
    public Exception? LoadFailure { get; set; }

    /// <summary>Gets or sets the exception save, delete and the connection test throw, or <see langword="null"/>.</summary>
    public Exception? OperationFailure { get; set; }

    /// <summary>Gets or sets what the connection test returns.</summary>
    public RemotePreflightReport Report { get; set; } = new() { Succeeded = true, Diagnoses = [], OmpVersion = "18.12.6", Platform = "Linux x86_64" };

    /// <summary>Gets or sets a task the connection test waits for before it answers, or <see langword="null"/> to answer at once.</summary>
    public Task? TestGate { get; set; }

    /// <summary>Gets or sets whether a terminal is available for the host trust step.</summary>
    public bool HasTerminal { get; set; } = true;

    /// <summary>Gets the number of connection tests started.</summary>
    public int TestCalls { get; private set; }

    /// <summary>Gets the number of times the profile list was read.</summary>
    public int LoadCalls { get; private set; }

    /// <summary>Gets the hosts a trust terminal was opened for.</summary>
    public List<SshProfile> TrustRequests { get; } = [];

    /// <summary>Gets the saved profiles.</summary>
    public IReadOnlyList<RemoteStoredProfile> Profiles => _profiles;

    /// <summary>Adds a saved profile.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="lastUsed">When it was last used, or <see langword="null"/>.</param>
    /// <returns>This host, for chaining.</returns>
    public FakeRemoteEnvironmentsHost With(EnvironmentProfile profile, DateTimeOffset? lastUsed = null)
    {
        _profiles.Add(new RemoteStoredProfile(profile, lastUsed));
        return this;
    }

    /// <inheritdoc/>
    public Task<RemoteProfileList> LoadProfilesAsync(CancellationToken cancellationToken)
    {
        LoadCalls++;
        return LoadFailure is { } failure
            ? Task.FromException<RemoteProfileList>(failure)
            : Task.FromResult(new RemoteProfileList([.. _profiles], ProfileWarning));
    }

    /// <inheritdoc/>
    public Task<RemoteHostCandidates> ListSshConfigHostsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new RemoteHostCandidates([.. ConfigAliases], [.. ConfigWarnings]));

    /// <inheritdoc/>
    public Task SaveProfileAsync(EnvironmentProfile profile, CancellationToken cancellationToken)
    {
        if (OperationFailure is { } failure)
        {
            return Task.FromException(failure);
        }

        var index = _profiles.FindIndex(existing => string.Equals(existing.Profile.Id, profile.Id, StringComparison.Ordinal));
        var stored = new RemoteStoredProfile(profile, index >= 0 ? _profiles[index].LastUsedUtc : null);
        if (index >= 0)
        {
            _profiles[index] = stored;
        }
        else
        {
            _profiles.Add(stored);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> DeleteProfileAsync(string profileId, CancellationToken cancellationToken)
    {
        if (OperationFailure is { } failure)
        {
            return Task.FromException<bool>(failure);
        }

        return Task.FromResult(_profiles.RemoveAll(existing => string.Equals(existing.Profile.Id, profileId, StringComparison.Ordinal)) > 0);
    }

    /// <inheritdoc/>
    public async Task<RemotePreflightReport> TestConnectionAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        TestCalls++;
        if (TestGate is { } gate)
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return OperationFailure is { } failure ? throw failure : Report;
    }

    /// <inheritdoc/>
    public Task<bool> OpenHostTrustTerminalAsync(SshProfile profile, CancellationToken cancellationToken)
    {
        TrustRequests.Add(profile);
        return Task.FromResult(HasTerminal);
    }
}
