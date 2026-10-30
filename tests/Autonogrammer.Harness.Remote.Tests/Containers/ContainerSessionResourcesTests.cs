using Autonogrammer.Contracts.Agents;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Remote.Containers;
using Shouldly;
using Xunit;

namespace Autonogrammer.Harness.Remote.Tests;

/// <summary>The one-call composition a head uses for a session in a container or a WSL distribution: transport factory, adjusted ACP options, file system and conventions.</summary>
public sealed class ContainerSessionResourcesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OmpLaunchPlan Plan(IReadOnlyList<AgentMcpServerSpec>? servers = null, IReadOnlyDictionary<string, string>? environment = null) => new()
    {
        ExtraArguments = ["--no-extensions"],
        Environment = environment ?? new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = servers ?? [],
        WorkingDirectory = "/local/ws",
        ConfigOverlayPath = null,
        PlanningEvents = [],
    };

    private static RemoteLaunchOptions Options(string stateRoot) =>
        new() { SessionId = "session-7", RemoteHomeDirectory = "/ignored", LocalStateRoot = stateRoot };

    /// <summary>
    /// The launch uses the home directory read from the container, carries no reverse forward, the options are adjusted for omp in a container, and the transport factory
    /// makes a transport without starting podman again.
    /// </summary>
    [Fact]
    public async Task ComposesTheLaunchTheOptionsAndTheTransportFactory()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();

        using var resources = await ContainerSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        resources.Launch.WorkingDirectory.ShouldBe(host.WorkspaceDirectory);
        resources.Launch.StateDirectory.ShouldStartWith(host.HomeDirectory, Case.Sensitive);
        resources.Launch.ReverseForwards.ShouldBeEmpty();
        resources.Launch.OmpPath.ShouldBe("omp");
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
        host.PodmanCalls().Count.ShouldBe(1);
    }

    /// <summary>The omp path of the profile is the omp the launch runs.</summary>
    [Fact]
    public async Task TheProfileOmpPathIsTheLaunchOmp()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();

        using var resources = await ContainerSessionResources.CreateAsync(host.Profile with { OmpPath = "/opt/omp/bin/omp" }, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        resources.Launch.OmpPath.ShouldBe("/opt/omp/bin/omp");
    }

    /// <summary>
    /// A container cannot carry a reverse forward, so a local MCP server on a loopback address is not rewritten to a forwarded port: it stays as it is, the launch has no
    /// forward, and a degraded step tells the user that it is unlikely to be reachable. A server on a reachable address is untouched.
    /// </summary>
    [Fact]
    public async Task ALoopbackServerIsKeptAndReportedUnreachable()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        var servers = new[]
        {
            new AgentMcpServerSpec { Name = "gateway", Url = new Uri("http://127.0.0.1:8123/mcp") },
            new AgentMcpServerSpec { Name = "docs", Url = new Uri("https://docs.example.com/mcp") },
        };

        using var resources = await ContainerSessionResources.CreateAsync(host.Profile, Plan(servers), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        resources.Launch.ReverseForwards.ShouldBeEmpty();
        resources.Launch.McpServers.Select(server => server.Url.ToString()).ShouldBe(["http://127.0.0.1:8123/mcp", "https://docs.example.com/mcp"]);
        var degraded = resources.Launch.PlanningEvents.Where(planned => planned.Status == StageStatus.Degraded).ToList();
        degraded.ShouldHaveSingleItem().Detail.ShouldNotBeNull().ShouldContain("gateway", Case.Sensitive);
    }

    /// <summary>The same resources serve the container's files and the conventions of the container.</summary>
    [Fact]
    public async Task ServesTheFilesAndTheConventionsOfTheContainer()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, "a.txt"), "x", Token).ConfigureAwait(true);
        _ = Directory.CreateDirectory(Path.Combine(host.WorkspaceDirectory, ".claude", "commands"));
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, ".claude", "commands", "ship.md"), "Ship it\n", Token).ConfigureAwait(true);
        using var resources = await ContainerSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        var entries = await resources.FileSystem.ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true);
        var setup = await resources.CreateConventionsAsync(Token).ConfigureAwait(true);
        var set = await setup.DiscoverAsync(Token).ConfigureAwait(true);

        entries.Select(entry => entry.Name).Order(StringComparer.Ordinal).ToArray().ShouldBe([".claude", "a.txt"]);
        set.Commands.ShouldHaveSingleItem().Name.ShouldBe("ship");
        setup.UserHome.ShouldBe(host.HomeDirectory);
    }

    /// <summary>A stopped container fails before any session exists, with an IOException that says the home directory could not be read.</summary>
    [Fact]
    public async Task AStoppedContainerFailsBeforeASessionExists()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create(new FakeContainerHostOptions { State = "exited" });

        var error = await Should.ThrowAsync<IOException>(() => ContainerSessionResources.CreateAsync(host.Profile, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token)).ConfigureAwait(true);

        error.Message.ShouldContain("home directory", Case.Sensitive);
    }

    /// <summary>An unsafe profile is refused before podman runs.</summary>
    [Fact]
    public async Task AnUnsafeProfileIsRefused()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeContainerHost.Create();

        await Should.ThrowAsync<ArgumentException>(() => ContainerSessionResources.CreateAsync(host.Profile with { Container = "--rm" }, Plan(), Options(directory.Path), null, host.ClientOptions, null, Token)).ConfigureAwait(true);

        host.PodmanCalls().ShouldBeEmpty();
    }

    /// <summary>The WSL resources compose the same way: home read from the distribution, no forward, options adjusted, files served.</summary>
    [Fact]
    public async Task TheWslResourcesComposeTheSameWay()
    {
        using var directory = TempDirectory.Create();
        using var host = FakeWslHost.Create();
        await File.WriteAllTextAsync(Path.Combine(host.WorkspaceDirectory, "a.txt"), "x", Token).ConfigureAwait(true);

        using var resources = await WslSessionResources.CreateAsync(host.Profile, Plan([new AgentMcpServerSpec { Name = "gateway", Url = new Uri("http://localhost:8123/mcp") }]), Options(directory.Path), null, host.ClientOptions, null, Token).ConfigureAwait(true);

        resources.Launch.WorkingDirectory.ShouldBe(host.WorkspaceDirectory);
        resources.Launch.StateDirectory.ShouldStartWith(host.HomeDirectory, Case.Sensitive);
        resources.Launch.ReverseForwards.ShouldBeEmpty();
        resources.Launch.McpServers.ShouldHaveSingleItem().Url.ToString().ShouldBe("http://localhost:8123/mcp");
        resources.ApplyTo(new AcpClientOptions { FsRoot = "/x", WorkingDirectory = "/x" }).FsRoot.ShouldBeNull();
        (await resources.FileSystem.ListDirectoryAsync(string.Empty, Token).ConfigureAwait(true)).ShouldHaveSingleItem().Name.ShouldBe("a.txt");
    }
}
