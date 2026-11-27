using System.Runtime.Versioning;
using Autonogrammer.Native.Agents;
using Autonogrammer.Native.Settings.Models;
using Autonogrammer.Native.Tests.AgentLoop;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.Settings.Models;

/// <summary>
/// The key of a custom provider, end to end through the head: it is saved through the Models source, kept in the Autonogrammer key store, and
/// reaches the environment of the omp the head starts (the ACP engine of a chat session), after planning, so the plan itself never holds it.
/// A persisted terminal record is retired (Phase 81): no terminal omp is started for it any more. The omp executable is a shell script that records the NAMES of the provider key
/// variables it was started with; no value is ever written or printed, and no real omp, model or network is involved.
/// </summary>
public sealed class ProviderKeyHeadTests
{
    private const string Name = "AUTONOGRAMMER_PROVIDER_GATEWAY_KEY";

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
        var head = await AgentHead.CreateAsync(HeadOptions(tree, fake, models.Keys)).ConfigureAwait(true);
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
        var head = await AgentHead.CreateAsync(HeadOptions(tree, fake, keys: null)).ConfigureAwait(true);
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

    /// <summary>A persisted terminal record is retired (Phase 81): no starter exists any more, so no terminal omp is started for the key journey. The plan and its terminal derivation never hold the key, no rendering shows it, and the merged ACP overlay still carries a key saved after the engine was composed.</summary>
    [Fact]
    [SupportedOSPlatform("linux")]
    public async Task AKeySavedAfterTheEngineWasComposedStaysOutOfThePlanAndNoRenderShowsIt()
    {
        SkipUnlessLinux();
        using var fake = new ModelsFakeOmp();
        using var tree = new TempTree();
        using var models = fake.Compose();
        (await models.Source.SaveCustomProviderAsync(ModelsForms.Gateway("gateway"), Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();
        var head = await AgentHead.CreateAsync(HeadOptions(tree, fake, models.Keys)).ConfigureAwait(true);
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(Token).ConfigureAwait(true);
        var (_, slot) = await NewSlotAsync(head, Token).ConfigureAwait(true);
        (await models.Source.SaveApiKeyAsync("gateway", ModelsForms.Canary, Token).ConfigureAwait(true)).Succeeded.ShouldBeTrue();

        var plan = await slot.PlanAsync(Token).ConfigureAwait(true);
        plan.Launch.Environment.ContainsKey(Name).ShouldBeFalse("the planner never holds a provider key");
        plan.Launch.ToString().ShouldNotContain(ModelsForms.Canary, Case.Sensitive);
        fake.AcpKeyNames().ShouldBeNull("no omp child is started without a prompt");
        await models.Keys.RefreshAsync(Token).ConfigureAwait(true);
        models.Keys.Merge(plan.Launch.Environment)[Name].ShouldBe(ModelsForms.Canary, "the merged ACP overlay still carries the key");
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

}
