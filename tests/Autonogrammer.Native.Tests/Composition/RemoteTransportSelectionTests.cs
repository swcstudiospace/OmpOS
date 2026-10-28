using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Composition;

/// <summary>
/// Which transport a composed engine talks through: a session on an ssh host goes through the ssh transport the remote resources built (the factory of the head is not asked), a local session
/// goes through the head's factory, and a head without services composes only local sessions with no remote part. The ssh client is a script that logs its arguments and exits: no network, no real omp.
/// </summary>
public sealed class RemoteTransportSelectionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static OmpLaunchPlan Plan() => new()
    {
        ExtraArguments = [],
        Environment = new Dictionary<string, string>(StringComparer.Ordinal),
        McpServers = [],
        WorkingDirectory = "/local/ws",
        PlanningEvents = [],
    };

    /// <summary>With remote resources the ssh transport runs the connection: the head's factory is never called, ssh is started for the omp acp command, and the engine can reconnect.</summary>
    [Fact]
    public async Task AnSshSessionConnectsThroughTheSshTransportAndNotTheLocalFactory()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The ssh stand-in is a POSIX shell script.");
        using var tree = new TempTree();
        var script = FakeSsh.Write(tree.Root);
        var localLaunches = 0;
        using var resources = await RemoteSessionResources.CreateAsync(
            new SshProfile { Alias = "build-box", RemoteWorkspaceRoot = "/srv/work" },
            Plan(),
            new RemoteLaunchOptions { SessionId = "session-1", RemoteHomeDirectory = "/", LocalStateRoot = tree.State },
            null,
            new SshClientOptions { SshPath = script.Path },
            null,
            Token).ConfigureAwait(true);
        var engine = EngineComposition.Compose(
            ["read"],
            Plan(),
            _ =>
            {
                localLaunches++;
                return new ScriptedOmp().TransportFactory(new AcpClientOptions());
            },
            options: new EngineCompositionOptions { Remote = resources });
        await using var host = engine.Host.ConfigureAwait(true);

        engine.Remote.ShouldBeSameAs(resources);
        engine.Reconnecting.ShouldNotBeNull();
        _ = await Should.ThrowAsync<Exception>(() => engine.Host.ConnectAsync(Token)).ConfigureAwait(true);

        localLaunches.ShouldBe(0);
        script.Calls().Any(static call => Regex.IsMatch(call, @"\bacp\b", RegexOptions.None, TimeSpan.FromSeconds(1))).ShouldBeTrue("ssh was started for omp acp");
    }

    /// <summary>Without remote resources the head's factory builds the transport and there is nothing to reconnect.</summary>
    [Fact]
    public async Task ALocalSessionConnectsThroughTheHeadSFactory()
    {
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var engine = EngineComposition.Compose(["read"], Plan(), omp.TransportFactory, options: new EngineCompositionOptions());
        await using var host = engine.Host.ConfigureAwait(true);

        await engine.Host.ConnectAsync(Token).ConfigureAwait(true);

        omp.Launches.ShouldHaveSingleItem().WorkingDirectory.ShouldBe("/local/ws");
        engine.Remote.ShouldBeNull();
        engine.Reconnecting.ShouldBeNull();
    }

    /// <summary>A head built without services composes a local engine through its own factory, with no remote resources, no hook gate and only the plan's own launch.</summary>
    [Fact]
    public async Task AHeadWithoutServicesComposesALocalEngineOnly()
    {
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, omp));
        await using var headScope = head.ConfigureAwait(true);
        head.Services.ShouldBeNull();
        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);

        var engine = await slot.EngineAsync(Token).ConfigureAwait(true);
        _ = await AgentLoopFixtures.SendAsync(slot, "hello", Token).ConfigureAwait(true);

        engine.Remote.ShouldBeNull();
        engine.Reconnecting.ShouldBeNull();
        var launch = omp.Launches.ShouldHaveSingleItem();
        launch.HookGate.ShouldBeNull();
        launch.McpServers.ShouldBeEmpty();
        (await slot.RemoteResourcesAsync(Token).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>A head with services still composes a local session locally: no remote resources are made when the session has no ssh target.</summary>
    [Fact]
    public async Task ALocalSessionOfAHeadWithServicesHasNoRemoteResources()
    {
        using var tree = new TempTree();
        var services = await Autonogrammer.Native.Composition.HeadServices.CreateAsync(
            new Autonogrammer.Native.Composition.HeadServicesOptions
            {
                StateRoot = tree.State,
                PluginsRoot = tree.Plugins,
                WorkspaceRoot = tree.Workspace,
                HomeDirectory = Path.Combine(tree.Root, "home"),
                ConfigDirectory = Path.Combine(tree.Root, "config"),
                GetVariable = static _ => null,
            },
            Token).ConfigureAwait(true);
        var omp = new ScriptedOmp();
        var head = AgentHead.Create(AgentLoopFixtures.Options(tree, omp, services: services));
        await using var headScope = head.ConfigureAwait(true);
        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);

        var engine = await slot.EngineAsync(Token).ConfigureAwait(true);
        _ = await AgentLoopFixtures.SendAsync(slot, "hello", Token).ConfigureAwait(true);

        engine.Remote.ShouldBeNull();
        engine.Reconnecting.ShouldBeNull();
        omp.Launches.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(tree.Workspace);
    }

    /// <summary>A stand-in for the ssh client: it logs one line per call, answers the home directory probe, and fails every other command.</summary>
    private sealed class FakeSsh
    {
        private FakeSsh(string path, string log)
        {
            Path = path;
            Log = log;
        }

        public string Path { get; }

        public string Log { get; }

        public static FakeSsh Write(string directory)
        {
            var log = System.IO.Path.Combine(directory, "ssh-calls.log");
            var path = System.IO.Path.Combine(directory, "ssh");
            File.WriteAllText(
                path,
                "#!/bin/sh\n"
                + "printf '%s\\n' \"$*\" >> '" + log + "'\n"
                + "case \"$*\" in\n"
                + "  *'printf %s \"$HOME\"'*) printf %s /home/remote; exit 0 ;;\n"
                + "esac\n"
                + "exit 255\n");
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            return new FakeSsh(path, log);
        }

        public string[] Calls() => File.Exists(Log) ? File.ReadAllLines(Log) : [];
    }
}
