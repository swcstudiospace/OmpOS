using System.Text.RegularExpressions;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Environments;
using Autonogrammer.Harness.Abstractions;
using Autonogrammer.Harness.Acp;
using Autonogrammer.Harness.AgentLoop.Launch;
using Autonogrammer.Harness.Remote;
using Autonogrammer.Harness.Remote.Tests;
using Autonogrammer.Harness.Rpc;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Engine;
using Autonogrammer.Native.Tests.AgentLoop;
using Autonogrammer.Presentation.Remote;
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
            options: new EngineCompositionOptions { Transport = EngineTransport.Acp, Remote = resources });
        await using var host = engine.Host.ConfigureAwait(true);

        engine.Remote.ShouldBeSameAs(resources);
        engine.Reconnecting.ShouldNotBeNull();
        _ = await Should.ThrowAsync<Exception>(() => engine.Host.ConnectAsync(Token)).ConfigureAwait(true);

        localLaunches.ShouldBe(0);
        script.Calls().Any(static call => Regex.IsMatch(call, @"\bacp\b", RegexOptions.None, TimeSpan.FromSeconds(1))).ShouldBeTrue("ssh was started for omp acp");
    }
    /// <summary>Remote sessions stay on ACP under <see cref="EngineTransport.Rpc"/> (Phase 83, ADR-0038 D2): composing RPC
    /// over remote resources refuses and names the remote combination, so the RPC flag can never silently downgrade a
    /// remote session to a local RPC session.</summary>
    [Fact]
    public async Task AnRpcSelectionWithRemoteResourcesIsRefusedNamingRemote()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The ssh stand-in is a POSIX shell script.");
        using var tree = new TempTree();
        var script = FakeSsh.Write(tree.Root);
        using var resources = await RemoteSessionResources.CreateAsync(
            new SshProfile { Alias = "build-box", RemoteWorkspaceRoot = "/srv/work" },
            Plan(),
            new RemoteLaunchOptions { SessionId = "session-1", RemoteHomeDirectory = "/", LocalStateRoot = tree.State },
            null,
            new SshClientOptions { SshPath = script.Path },
            null,
            Token).ConfigureAwait(true);

        var error = Should.Throw<ArgumentException>(() => EngineComposition.Compose(
            [],
            Plan(),
            options: new EngineCompositionOptions { Transport = EngineTransport.Rpc, Remote = resources }));

        error.Message.ShouldContain("remote");
    }


    /// <summary>Without remote resources the head's factory builds the transport and there is nothing to reconnect.</summary>
    [Fact]
    public async Task ALocalSessionConnectsThroughTheHeadSFactory()
    {
        var omp = new ScriptedOmp { AdvertiseAuthentication = true };
        var engine = EngineComposition.Compose(["read"], Plan(), omp.TransportFactory, options: new EngineCompositionOptions { Transport = EngineTransport.Acp });
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
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, omp)).ConfigureAwait(true);
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
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The fake omp is a POSIX shell script.");

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
                OmpPath = AgentLoopFixtures.WriteFakeOmp(Path.Combine(tree.Root, "omp"), "18.18.1"),
            },
            Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var omp = new ScriptedOmp();
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, omp, services: services)).ConfigureAwait(true);
        await using var headScope = head.ConfigureAwait(true);
        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);

        var engine = await slot.EngineAsync(Token).ConfigureAwait(true);
        _ = await AgentLoopFixtures.SendAsync(slot, "hello", Token).ConfigureAwait(true);

        engine.Remote.ShouldBeNull();
        engine.Reconnecting.ShouldBeNull();
        omp.Launches.ShouldHaveSingleItem().WorkingDirectory.ShouldBe(tree.Workspace);
    }

    /// <summary>
    /// Production path under the shipped Rpc default (SPE-37896): a head that leaves <see cref="SessionEngineSetOptions.Transport"/>
    /// alone composes a session on a remote (container) host over ACP instead of throwing. The compose site forces ACP when the
    /// plan carries remote resources, so compose succeeds and the engine carries them; the local factory is never asked.
    /// </summary>
    [Fact]
    public async Task AHeadWithTheShippedRpcDefaultComposesARemoteSessionOverAcp()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The podman stand-in is a POSIX shell script.");
        using var tree = new TempTree();
        using var container = FakeContainerHost.Create();
        var services = await Autonogrammer.Native.Composition.HeadServices.CreateAsync(
            new Autonogrammer.Native.Composition.HeadServicesOptions
            {
                StateRoot = tree.State,
                PluginsRoot = tree.Plugins,
                WorkspaceRoot = tree.Workspace,
                HomeDirectory = Path.Combine(tree.Root, "home"),
                ConfigDirectory = Path.Combine(tree.Root, "config"),
                GetVariable = static _ => null,
                Podman = container.ClientOptions,
                OmpAvailable = static () => false,
            },
            Token).ConfigureAwait(true);
        await using var servicesScope = services.ConfigureAwait(true);
        var profile = new EnvironmentProfile { Id = "devbox", DisplayName = "Dev box", Kind = EnvironmentKind.Container, Container = container.Profile };
        await services.RemoteProfiles.SaveAsync(profile, Token).ConfigureAwait(true);
        var local = new ScriptedOmp();
        var head = await AgentHead.CreateAsync(AgentLoopFixtures.Options(tree, local, services: services)).ConfigureAwait(true);
        await using var headScope = head.ConfigureAwait(true);
        services.NewSessionTarget.Set(new SessionTarget(SessionTargetKind.Container, "container: Dev box", profile));

        var (_, slot) = await AgentLoopFixtures.NewSlotAsync(head, Token).ConfigureAwait(true);

        var plan = await slot.PlanAsync(Token).ConfigureAwait(true);
        plan.Extras.IsRemote.ShouldBeTrue();
        var engine = await slot.EngineAsync(Token).ConfigureAwait(true);

        engine.Remote.ShouldNotBeNull();
        engine.Reconnecting.ShouldNotBeNull();
        local.Launches.ShouldBeEmpty("remote omp runs on the host, not through the local factory");
    }
    /// <summary>
    /// Production path under the shipped Rpc default (SPE-37896): a head that leaves <see cref="SessionEngineSetOptions.Transport"/>
    /// alone and admits tool kinds (the production kinds) composes over ACP instead of throwing: the capability fallback in
    /// <c>ComposeEngineAsync</c> routes any <c>ComposeRpc</c> refusal predicate to ACP, so the RPC seam is never asked and the
    /// session connects through the scripted ACP factory.
    /// </summary>
    [Fact]
    public async Task AProductionKindsSessionComposesOverAcpUnderTheShippedRpcDefault()
    {
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        var rpcAsked = false;
        await using var set = EngineSet(
            tree,
            ["read"],
            omp,
            _ => { rpcAsked = true; return new StubRpcSession(); });
        using var plan = MinimalPlan();

        var engine = await set.ComposeEngineAsync(plan, null, Token).ConfigureAwait(true);

        await using (engine.Host.ConfigureAwait(true))
        {
            await engine.Host.ConnectAsync(Token).ConfigureAwait(true);
        }

        rpcAsked.ShouldBeFalse("an approval-capable session composes over ACP");
        omp.Launches.ShouldNotBeEmpty("an approval-capable session composes over ACP");
        engine.Remote.ShouldBeNull();
    }

    /// <summary>
    /// A deny-by-default session (empty allowed kinds) under the shipped Rpc default keeps the configured Rpc transport:
    /// the RPC seam is asked on connect and the ACP factory is never consulted.
    /// </summary>
    [Fact]
    public async Task ADenyByDefaultSessionComposesOverRpcUnderTheShippedRpcDefault()
    {
        using var tree = new TempTree();
        var omp = new ScriptedOmp();
        var rpcAsked = false;
        await using var set = EngineSet(
            tree,
            [],
            omp,
            _ => { rpcAsked = true; return new StubRpcSession(); });
        using var plan = MinimalPlan();

        var engine = await set.ComposeEngineAsync(plan, null, Token).ConfigureAwait(true);

        await using (engine.Host.ConfigureAwait(true))
        {
            await engine.Host.ConnectAsync(Token).ConfigureAwait(true);
        }

        rpcAsked.ShouldBeTrue("a deny-by-default session composes over RPC");
        omp.Launches.ShouldBeEmpty("the RPC path never consults the ACP factory");
        engine.Remote.ShouldBeNull();
    }

    private static SessionEngineSet EngineSet(
        TempTree tree,
        IReadOnlyCollection<string> allowedKinds,
        ScriptedOmp omp,
        Func<OmpRpcClientOptions, IOmpSession> rpcFactory) => new(new SessionEngineSetOptions
        {
            Loop = AgentLoopEnvironment.Resolve(
                name => name switch
                {
                    AgentLoopEnvironment.PluginsRootVariable => tree.Plugins,
                    AgentLoopEnvironment.StateDirectoryVariable => tree.State,
                    _ => null,
                },
                tree.Workspace,
                tree.Workspace,
                tree.Root),
            AllowedKinds = allowedKinds,
            TransportFactory = omp.TransportFactory,
            RpcSessionFactory = rpcFactory,
            OmpPath = "/bin/true",
            MaintenanceInterval = Timeout.InfiniteTimeSpan,
        });

    private static SessionPlan MinimalPlan() => new(Plan(), new EmptyStageSource(), new EmptyTaskContextSource(), null, new RecheckStageSource(), null, false, null);

    private sealed class EmptyStageSource : IStageSource
    {
        public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            _ = scope;
            await Task.CompletedTask.ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            yield break;
        }
    }

    private sealed class EmptyTaskContextSource : ITaskContextSource
    {
        public Task<TaskContext> GetAsync(StageObservationScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(new TaskContext { Title = string.Empty, SharedMemory = [], Lessons = [], Warnings = [] });
    }

    private sealed class StubRpcSession : IOmpSession
    {
        public HarnessState State { get; private set; } = HarnessState.Disconnected;

        public IReadOnlyDictionary<string, string> AdvertisedCapabilities => new Dictionary<string, string>(StringComparer.Ordinal);

        public Task CreateAsync(CancellationToken cancellationToken)
        {
            State = HarnessState.Ready;
            return Task.CompletedTask;
        }

        public Task ResumeAsync(string sessionId, CancellationToken cancellationToken)
        {
            State = HarnessState.Ready;
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<HarnessEvent> SubmitPromptAsync(string prompt, PromptOptions options, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CompletedEvent("stop");
        }

        public Task CancelAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            State = HarnessState.Disposed;
            return ValueTask.CompletedTask;
        }
    }

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
