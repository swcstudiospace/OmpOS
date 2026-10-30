using Autonogrammer.Contracts.Environments;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The desktop implementation of the remote page's host: store, ssh config aliases, the connection test over the fake ssh and the host trust terminal.</summary>
public sealed class RemoteEnvironmentsHostTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static EnvironmentProfile Profile(string id, SshProfile ssh) => new() { Id = id, DisplayName = "Box " + id, Kind = EnvironmentKind.Ssh, Ssh = ssh };

    /// <summary>Profiles saved through the host come back with their last-used time, and a deleted one is gone.</summary>
    [Fact]
    public async Task ProfilesAreSavedListedAndDeleted()
    {
        using var directory = TempDirectory.Create();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"), new ManualTimeProvider());
        var host = new RemoteEnvironmentsHost(store);

        await host.SaveProfileAsync(Profile("a", new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/w" }), Token).ConfigureAwait(true);
        _ = await store.RecordUsedAsync("a", Token).ConfigureAwait(true);
        var list = await host.LoadProfilesAsync(Token).ConfigureAwait(true);
        var deleted = await host.DeleteProfileAsync("a", Token).ConfigureAwait(true);

        list.Warning.ShouldBeNull();
        var saved = list.Profiles.ShouldHaveSingleItem();
        saved.Profile.Id.ShouldBe("a");
        saved.LastUsedUtc.ShouldNotBeNull();
        deleted.ShouldBeTrue();
        (await host.LoadProfilesAsync(Token).ConfigureAwait(true)).Profiles.ShouldBeEmpty();
    }

    /// <summary>The ssh config aliases are read from the configured home; a missing file is an empty list.</summary>
    [Fact]
    public async Task SshConfigAliasesAreListed()
    {
        using var directory = TempDirectory.Create();
        using var home = TempDirectory.Create();
        _ = Directory.CreateDirectory(home.Combine(".ssh"));
        await File.WriteAllTextAsync(home.Combine(".ssh", "config"), "Host alpha beta\n  HostName a.example\nHost *.wild\n", Token).ConfigureAwait(true);
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"));
        var withConfig = new RemoteEnvironmentsHost(store, configReader: new SshConfigReader(home.Path));
        var withoutConfig = new RemoteEnvironmentsHost(store, configReader: new SshConfigReader(directory.Path));

        (await withConfig.ListSshConfigHostsAsync(Token).ConfigureAwait(true)).Aliases.ShouldBe(["alpha", "beta"]);
        (await withoutConfig.ListSshConfigHostsAsync(Token).ConfigureAwait(true)).Aliases.ShouldBeEmpty();
    }

    /// <summary>The connection test runs the preflight through ssh: a healthy fake machine passes and a refused one reports the diagnosis.</summary>
    [Fact]
    public async Task TheConnectionTestRunsThePreflight()
    {
        using var directory = TempDirectory.Create();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"));
        using var healthy = FakeRemoteHost.Create();
        using var refused = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "refused" });

        var good = await new RemoteEnvironmentsHost(store, healthy.ClientOptions).TestConnectionAsync(healthy.Profile, Token).ConfigureAwait(true);
        var bad = await new RemoteEnvironmentsHost(store, refused.ClientOptions).TestConnectionAsync(refused.Profile, Token).ConfigureAwait(true);

        good.Succeeded.ShouldBeTrue();
        good.OmpVersion.ShouldBe("18.12.6");
        bad.Succeeded.ShouldBeFalse();
        bad.Diagnoses.ShouldHaveSingleItem().Kind.ShouldBe(RemoteDiagnosisKind.HostUnreachable);
    }

    /// <summary>The host trust terminal is handed to the head as the interactive true command; without a head terminal the answer is false and nothing starts.</summary>
    [Fact]
    public async Task TheHostTrustTerminalIsHandedToTheHead()
    {
        using var directory = TempDirectory.Create();
        using var store = new EnvironmentProfileStore(directory.Combine("environments.json"));
        var ssh = new SshProfile { Alias = "alpha", RemoteWorkspaceRoot = "/w" };
        RemoteTerminalCommand? seen = null;
        var withTerminal = new RemoteEnvironmentsHost(
            store,
            openTrustTerminal: (command, _) =>
            {
                seen = command;
                return Task.FromResult(true);
            });
        var withoutTerminal = new RemoteEnvironmentsHost(store);

        (await withTerminal.OpenHostTrustTerminalAsync(ssh, Token).ConfigureAwait(true)).ShouldBeTrue();
        (await withoutTerminal.OpenHostTrustTerminalAsync(ssh, Token).ConfigureAwait(true)).ShouldBeFalse();

        var command = seen.ShouldNotBeNull();
        command.Invocation.Arguments.ShouldContain("-tt", StringComparer.Ordinal);
        command.Invocation.Arguments.ShouldNotContain("BatchMode=yes", StringComparer.Ordinal);
        command.Invocation.Arguments[^1].ShouldBe("true");
    }
}
