using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Verifies consent through spawn counts and run outcomes: no process starts before consent for the exact command, and
/// any edit to the command needs new consent.
/// </summary>
public sealed class HookConsentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private const string HookId = "acme.tools#0";

    [Fact]
    public async Task NoProcessIsSpawnedBeforeConsent()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var manifest = ManifestWith(["/opt/hooks/check", "--strict"], directory.Path);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([ManifestJson.Source(manifest)], Token);
        var runner = new FakeHookProcessRunner();

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        runner.SpawnCount.ShouldBe(0);
        var execution = result.Executions.Single();
        execution.Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
        execution.ExitCode.ShouldBeNull();
    }

    [Fact]
    public async Task ConsentedHookRunsOnceAndRecordsExitStatusDurationAndOutput()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var manifest = ManifestWith(["/opt/hooks/check", "--strict"], directory.Path);
        await GrantAsync(store, manifest);
        var runner = new FakeHookProcessRunner((_, _) =>
            Task.FromResult(new HookProcessOutcome(0, "checked\n", "note\n")));

        var result = await RunAsync(store, manifest, runner);

        runner.SpawnCount.ShouldBe(1);
        var execution = result.Executions.Single();
        execution.Status.ShouldBe(HookExecutionStatus.Succeeded);
        execution.ExitCode.ShouldBe(0);
        execution.Duration.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
        execution.StandardOutput.ShouldBe("checked\n");
        execution.StandardError.ShouldBe("note\n");
        execution.StandardOutputTruncated.ShouldBeFalse();
        execution.StandardErrorTruncated.ShouldBeFalse();
        execution.CommandHash.ShouldBe(HookCommandIdentity.ComputeHash(HookTrigger.BeforeAction, false, runner.Spawns.Single()));
    }

    [Fact]
    public async Task EditingTheCommandInvalidatesConsent()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var original = ManifestWith(["/opt/hooks/check", "--strict"], directory.Path);
        await GrantAsync(store, original);

        var editedArgument = ManifestWith(["/opt/hooks/check", "--lenient"], directory.Path);
        var argumentRunner = new FakeHookProcessRunner();
        var afterArgumentEdit = await RunAsync(store, editedArgument, argumentRunner);

        afterArgumentEdit.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
        argumentRunner.SpawnCount.ShouldBe(0);

        var editedDirectory = ManifestWith(["/opt/hooks/check", "--strict"], directory.PathOf("elsewhere"));
        var directoryRunner = new FakeHookProcessRunner();
        var afterDirectoryEdit = await RunAsync(store, editedDirectory, directoryRunner);

        afterDirectoryEdit.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
        directoryRunner.SpawnCount.ShouldBe(0);

        var restoredRunner = new FakeHookProcessRunner();
        var restored = await RunAsync(store, original, restoredRunner);

        restored.Executions.Single().Status.ShouldBe(HookExecutionStatus.Succeeded);
        restoredRunner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task ConsentSurvivesReopeningTheStateFile()
    {
        using var directory = new TempDirectory();
        var manifest = ManifestWith(["/opt/hooks/check"], directory.Path);
        using (var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token))
        {
            await GrantAsync(store, manifest);
        }

        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var runner = new FakeHookProcessRunner();
        var result = await RunAsync(reopened, manifest, runner);

        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.Succeeded);
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task BlockingHookWithoutConsentDeniesTheActionAndSpawnsNothing()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var manifest = ManifestWith(["/opt/hooks/guard"], directory.Path, blocking: true);
        var runner = new FakeHookProcessRunner();

        var result = await RunAsync(store, manifest, runner);

        runner.SpawnCount.ShouldBe(0);
        result.ActionDenied.ShouldBeTrue();
        result.DenialReason.ShouldBe(HookDenialReason.ConsentMissing);
        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
    }

    [Fact]
    public async Task BlockingHookWithoutConsentStopsTheHooksAfterIt()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var manifest = ManifestJson.Manifest(
            "acme.tools",
            1,
            ManifestJson.Hook("beforeAction", ["/opt/hooks/guard"], directory.Path, blocking: true),
            ManifestJson.Hook("beforeAction", ["/opt/hooks/after"], directory.Path));
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([ManifestJson.Source(manifest)], Token);
        await host.Hooks.GrantConsentAsync("acme.tools#1", Token);
        var runner = new FakeHookProcessRunner();

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        runner.SpawnCount.ShouldBe(0);
        result.DenialReason.ShouldBe(HookDenialReason.ConsentMissing);
        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.AwaitingConsent, HookExecutionStatus.Skipped]);
    }

    [Fact]
    public async Task NonBlockingHookWithoutConsentDoesNotDenyTheAction()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var manifest = ManifestWith(["/opt/hooks/check"], directory.Path);
        var runner = new FakeHookProcessRunner();

        var result = await RunAsync(store, manifest, runner);

        runner.SpawnCount.ShouldBe(0);
        result.ActionDenied.ShouldBeFalse();
        result.DenialReason.ShouldBeNull();
        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
    }

    [Fact]
    public async Task TurningBlockingOnForAConsentedHookRequiresNewConsent()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var nonBlocking = ManifestWith(["/opt/hooks/check"], directory.Path);
        await GrantAsync(store, nonBlocking);
        var blocking = ManifestWith(["/opt/hooks/check"], directory.Path, blocking: true);
        var runner = new FakeHookProcessRunner();

        var result = await RunAsync(store, blocking, runner);

        runner.SpawnCount.ShouldBe(0);
        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
        result.DenialReason.ShouldBe(HookDenialReason.ConsentMissing);
    }

    [Fact]
    public async Task ChangingTheTriggerRequiresNewConsent()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        var beforeAction = ManifestWith(["/opt/hooks/check"], directory.Path);
        await GrantAsync(store, beforeAction);
        var afterAction = ManifestJson.Manifest("acme.tools", hooks: ManifestJson.Hook("afterAction", ["/opt/hooks/check"], directory.Path));
        var runner = new FakeHookProcessRunner();

        var result = await RunAsync(store, afterAction, runner, HookTrigger.AfterAction);

        runner.SpawnCount.ShouldBe(0);
        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
    }

    private static JsonObject ManifestWith(string[] command, string workingDirectory, bool blocking = false) =>
        ManifestJson.Manifest("acme.tools", hooks: ManifestJson.Hook("beforeAction", command, workingDirectory, blocking));

    private static async Task GrantAsync(PluginStateStore store, JsonObject manifest)
    {
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([ManifestJson.Source(manifest)], Token).ConfigureAwait(false);
        await host.Hooks.GrantConsentAsync(HookId, Token).ConfigureAwait(false);
    }

    private static async Task<HookRunResult> RunAsync(
        PluginStateStore store,
        JsonObject manifest,
        IHookProcessRunner runner,
        HookTrigger trigger = HookTrigger.BeforeAction)
    {
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([ManifestJson.Source(manifest)], Token).ConfigureAwait(false);
        return await new HookRunner(host.Hooks, store, runner).RunAsync(trigger, Token).ConfigureAwait(false);
    }
}
