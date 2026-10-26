using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Verifies quarantine through run outcomes and spawn counts, and the operator lift through its result, its persisted
/// history, and the hooks that run after it. Consent is granted for every hook, so a hook that does not run is held back
/// by quarantine alone.
/// </summary>
public sealed class PluginQuarantineTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string PluginId = "acme.tools";

    [Fact]
    public async Task BlockingHookOfAQuarantinedPluginDeniesWithPluginQuarantinedAndSpawnsNothing()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: true, "/opt/guard", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);
        var runner = new FakeHookProcessRunner();

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.ActionDenied.ShouldBeTrue();
        result.DenialReason.ShouldBe(HookDenialReason.PluginQuarantined);
        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.PluginQuarantined]);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task BlockingQuarantinedHookStopsLaterHooksOfOtherPlugins()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(
            store,
            Manifest(PluginId, blocking: true, "/opt/guard", directory.Path),
            Manifest("other.plugin", blocking: false, "/opt/other", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);
        var runner = new FakeHookProcessRunner();

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.DenialReason.ShouldBe(HookDenialReason.PluginQuarantined);
        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.PluginQuarantined, HookExecutionStatus.Skipped]);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task NonBlockingHookOfAQuarantinedPluginIsSkippedAndSpawnsNothing()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: false, "/opt/note", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);
        var runner = new FakeHookProcessRunner();

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.ActionDenied.ShouldBeFalse();
        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Skipped]);
        runner.SpawnCount.ShouldBe(0);
    }

    [Fact]
    public async Task LiftWithAReasonReEnablesTheBlockingHook()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: true, "/opt/guard", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);
        var runner = new FakeHookProcessRunner();
        var hookRunner = new HookRunner(host.Hooks, store, runner);
        var whileQuarantined = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);

        var lifted = await host.LiftQuarantineAsync(PluginId, "reviewed by operator", Token);
        var afterLift = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);

        whileQuarantined.DenialReason.ShouldBe(HookDenialReason.PluginQuarantined);
        lifted.ShouldBeTrue();
        afterLift.ActionDenied.ShouldBeFalse();
        afterLift.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Succeeded]);
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task LiftIsPersistedWithItsReasonAndSurvivesARestart()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: true, "/opt/guard", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);

        await host.LiftQuarantineAsync(PluginId, "reviewed by operator", Token);

        store.FindQuarantine(PluginId).ShouldBeNull();
        var lift = store.Lifts.Single();
        lift.PluginId.ShouldBe(PluginId);
        lift.Reason.ShouldBe("reviewed by operator");

        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        reopened.FindQuarantine(PluginId).ShouldBeNull();
        reopened.Lifts.Single().Reason.ShouldBe("reviewed by operator");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public async Task LiftWithAnEmptyOrWhitespaceReasonThrowsAndKeepsTheQuarantine(string reason)
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: true, "/opt/guard", directory.Path));
        await store.QuarantineAsync(PluginId, "operator test", Token);

        await Should.ThrowAsync<ArgumentException>(async () => await host.LiftQuarantineAsync(PluginId, reason, Token).ConfigureAwait(false));

        store.FindQuarantine(PluginId).ShouldNotBeNull();
        store.Lifts.ShouldBeEmpty();
    }

    [Fact]
    public async Task LiftOfAPluginThatIsNotQuarantinedReturnsFalseAndChangesNothing()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, Manifest(PluginId, blocking: true, "/opt/guard", directory.Path));
        var runner = new FakeHookProcessRunner();

        var lifted = await host.LiftQuarantineAsync(PluginId, "no quarantine to lift", Token);
        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        lifted.ShouldBeFalse();
        store.Lifts.ShouldBeEmpty();
        result.ActionDenied.ShouldBeFalse();
        runner.SpawnCount.ShouldBe(1);
    }

    private static JsonObject Manifest(string id, bool blocking, string command, string workingDirectory) =>
        ManifestJson.Manifest(id, hooks: ManifestJson.Hook("beforeAction", [command], workingDirectory, blocking));

    private static async Task<PluginHost> LoadConsentedAsync(PluginStateStore store, params JsonObject[] manifests)
    {
        var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(manifests.Select(ManifestJson.Source).ToList(), Token).ConfigureAwait(false);
        foreach (var hook in host.Hooks.List())
        {
            await host.Hooks.GrantConsentAsync(hook.Id, Token).ConfigureAwait(false);
        }

        return host;
    }
}
