using OmpOS.Harness.Acp;
using OmpOS.Harness.AgentLoop.Launch;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Remote.Tests;

/// <summary>The one-call composition a head uses for a session on an ssh host: transport factory, adjusted ACP options, remote file system and convention discovery.</summary>
public sealed class RemoteSessionResourcesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OmpLaunchPlan Plan() => new()
    {
        ExtraArguments = ["--no-extensions"],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = [],
        WorkingDirectory = "/local/ws",
        ConfigOverlayPath = null,
        PlanningEvents = [],
    };

    private static RemoteLaunchOptions Options(string stateRoot) =>
        new() { SessionId = "session-7", RemoteHomeDirectory = "/ignored", LocalStateRoot = stateRoot };

    /// <summary>The launch uses the home directory read from the host, the options are adjusted for a remote omp, and the transport factory makes a transport without starting ssh.</summary>
    [Fact]
    public async Task ComposesTheLaunchTheOptionsAndTheTransportFactory()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeRemoteHost.Create();

        using var resources = await RemoteSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        resources.Launch.WorkingDirectory.ShouldBe(host.WorkspaceDirectory);
        resources.Launch.StateDirectory.ShouldStartWith(host.HomeDirectory, Case.Sensitive);
        var local = new AcpClientOptions { FsRoot = "/local/ws", ExtraArguments = ["--x"], WorkingDirectory = "/local/ws", OmpPath = "/usr/bin/omp" };
        var adjusted = resources.ApplyTo(local);
        adjusted.FsRoot.ShouldBeNull();
        adjusted.WorkingDirectory.ShouldBe(host.WorkspaceDirectory);
        adjusted.ExtraArguments.ShouldBeEmpty();
        adjusted.OmpPath.ShouldBeNull();
        adjusted.EnvironmentVariables.ShouldBeEmpty();
        var transport = resources.TransportFactory(adjusted);
        await using var transportScope = transport.ConfigureAwait(true);
        transport.ShouldNotBeNull();
        host.SshCalls().Count.ShouldBe(1);
    }

    /// <summary>The same resources serve the remote file system and the convention discovery of the host.</summary>
    [Fact]
    public async Task ServesTheFilesAndTheConventionsOfTheHost()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeRemoteHost.Create();
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, "a.txt"), "x", TestContext.Current.CancellationToken).ConfigureAwait(true);
        _ = Directory.CreateDirectory(Path.Combine(host.WorkspaceDirectory, ".claude", "commands"));
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, ".claude", "commands", "ship.md"), "Ship it\n", TestContext.Current.CancellationToken).ConfigureAwait(true);
        using var resources = await RemoteSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        var entries = await resources.FileSystem.ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);
        var setup = await resources.CreateConventionsAsync(Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        entries.Select(entry => entry.Name).Order(StringComparer.Ordinal).ToArray().ShouldBe([".claude", "a.txt"]);
        set.Commands.ShouldHaveSingleItem().Name.ShouldBe("ship");
        setup.UserHome.ShouldBe(host.HomeDirectory);
    }

    /// <summary>A host that does not answer fails before any session exists, with an IOException that says the home directory could not be read.</summary>
    [Fact]
    public async Task AnUnreachableHostFailsBeforeASessionExists()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeRemoteHost.Create(new FakeRemoteHostOptions { Mode = "refused" });

        var error = await Should.ThrowAsync<IOException>(() => RemoteSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token)).ConfigureAwait(true);

        error.Message.ShouldContain("home directory", Case.Sensitive);
    }
}
