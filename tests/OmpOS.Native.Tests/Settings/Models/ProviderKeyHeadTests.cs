using System.Runtime.Versioning;
using OmpOS.Harness.OmpLauncher;
using OmpOS.Harness.Terminal;
using OmpOS.Native.Agents;
using OmpOS.Native.Settings.Models;
using OmpOS.Native.Tests.AgentLoop;
using OmpOS.Native.Tests.Terminal;
using Shouldly;
using Xunit;
using static OmpOS.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace OmpOS.Native.Tests.Settings.Models;

/// <summary>
/// The key of a custom provider, end to end through the head: it is saved through the Models source, kept in the OmpOS key store, and
/// reaches the environment of the omp the head starts (the ACP engine of a chat session and the omp of a terminal session), after
/// planning, so the plan itself never holds it. The omp executable is a shell script that records the NAMES of the provider key
/// variables it was started with; no value is ever written or printed, and no real omp, model or network is involved.
/// </summary>
public sealed class ProviderKeyHeadTests
{
    private const string Name = "OMPOS_PROVIDER_GATEWAY_KEY";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AKeySavedAfterTheEngineWasComposedStillReachesTheOmpAcpThePromptStarts()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var tree = new TempTree();
        using var models = fake.Compose();
        (await models.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var head = AgentHead.Create(HeadOptions(tree, fake, models.Keys));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(Token).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, Token).ConfigureAwait(true);
        _ = await slot.EngineAsync(Token).ConfigureAwait(true);

        (await models.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var turn = StartTurnAsync(slot);
        await EventuallyAsync(() => fake.AcpKeyNames() is not null, "the omp acp child to record its environment", Token).ConfigureAwait(true);

        fake.AcpKeyNames().ShouldBe([Name]);
        var plan = await slot.PlanAsync(Token).ConfigureAwait(true);
        plan.Launch.Environment.ContainsKey(Name).ShouldBeFalse("the planner never holds a provider key");
        plan.Launch.ToString().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        var child = fake.AcpProcessId().ShouldNotBeNull();
        await head.DisposeAsync().ConfigureAwait(true);
        await turn.WaitAsync(TimeSpan.FromSeconds(30), Token).ConfigureAwait(true);
        IsProcessRunning(child).ShouldBeFalse("disposing the head leaves no omp acp child");
        AssertNoKeyOutsideTheStore(fake);
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task WithoutTheProviderKeysTheOmpAcpGetsNoProviderKeyVariable()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var tree = new TempTree();
        using var models = fake.Compose();
        (await models.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        (await models.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var head = AgentHead.Create(HeadOptions(tree, fake, keys: null));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(Token).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, Token).ConfigureAwait(true);

        var turn = StartTurnAsync(slot);
        await EventuallyAsync(() => fake.AcpKeyNames() is not null, "the omp acp child to record its environment", Token).ConfigureAwait(true);

        fake.AcpKeyNames().ShouldBeEmpty();
        var child = fake.AcpProcessId().ShouldNotBeNull();
        await head.DisposeAsync().ConfigureAwait(true);
        await turn.WaitAsync(TimeSpan.FromSeconds(30), Token).ConfigureAwait(true);
        IsProcessRunning(child).ShouldBeFalse("disposing the head leaves no omp acp child");
    }

    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task TheTerminalOmpGetsTheKeyAtEveryStartAndNeitherThePlanNorAnyPrintShowsIt()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var tree = new TempTree();
        using var models = fake.Compose();
        (await models.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var head = AgentHead.Create(HeadOptions(tree, fake, models.Keys));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(Token).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, Token).ConfigureAwait(true);
        var backend = new CapturingBackend();
        var starter = new PlannedOmpTerminalStarter(slot, static () => null, new OmpTerminalLauncher(backend, fake.Locator), models.Keys);

        var first = await starter.StartAsync(80, 24, new SilentSink(), Token).ConfigureAwait(true);
        var firstLifetime = (OmpTerminalStarted)first;
        await using var firstScope = firstLifetime.ConfigureAwait(true);
        (await models.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var second = await starter.StartAsync(80, 24, new SilentSink(), Token).ConfigureAwait(true);
        var secondLifetime = (OmpTerminalStarted)second;
        await using var secondScope = secondLifetime.ConfigureAwait(true);

        backend.Started.Count.ShouldBe(2);
        backend.Started[0].Environment.ContainsKey(Name).ShouldBeFalse("no key was saved at the first start");
        backend.Started[1].Environment[Name].ShouldBe(ModelsForms.Canary);
        var plan = await slot.PlanAsync(Token).ConfigureAwait(true);
        plan.Launch.Environment.ContainsKey(Name).ShouldBeFalse();
        plan.Launch.ForTerminal().Environment.ContainsKey(Name).ShouldBeFalse();
        backend.Started[1].ToString().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        backend.Started[1].ToString().ShouldContain(Name, Case.Sensitive);
        first.ToString()!.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        second.ToString()!.ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        new OmpTerminalLaunchRequest("/workspace", [], 80, 24, new SilentSink()) { Environment = backend.Started[1].Environment }
            .ToString().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
    }

    private static AgentHeadOptions HeadOptions(TempTree tree, ModelsFakeOmp fake, ProviderKeyEnvironment? keys) => new()
    {
        PluginsRoot = tree.Plugins,
        StateRoot = tree.State,
        WorkspaceRoot = tree.Workspace,
        SelectMostRecentSession = false,
        MaintenanceInterval = Timeout.InfiniteTimeSpan,
        DetailsRefreshInterval = Timeout.InfiniteTimeSpan,
        OmpPath = fake.ExecutablePath,
        ProviderKeys = keys,
    };

    private static Task StartTurnAsync(SessionSlot slot) => Task.Run(
        async () =>
        {
            try
            {
                await foreach (var unused in slot.Prompts.SubmitAsync("work", CancellationToken.None).ConfigureAwait(false))
                {
                    // The fake never answers: the turn ends when the engine is stopped.
                }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // The engine was stopped under the turn, which ends it with a disconnect.
            }
        },
        CancellationToken.None);

    private static void AssertNoKeyOutsideTheStore(ModelsFakeOmp fake)
    {
        foreach (var (path, text) in fake.ReadEverything(fake.SecretsPath))
        {
            text.ShouldNotContain(ModelsForms.Canary, Case.Sensitive, path);
        }
    }

    private static void SkipUnlessLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Skip("The fake omp is a POSIX shell script.");
        }
    }

    private sealed class SilentSink : IProcessOutputSink
    {
        public void Write(string text)
        {
        }
    }

    private sealed class CapturingBackend : ITerminalBackend
    {
        public List<TerminalSessionOptions> Started { get; } = [];

        public Task<ITerminalSession> StartAsync(TerminalSessionOptions options, CancellationToken cancellationToken)
        {
            Started.Add(options);
            return Task.FromResult<ITerminalSession>(new FakeTerminalSession());
        }
    }
}
