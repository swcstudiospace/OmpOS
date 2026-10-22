using System.Text;
using System.Text.Json.Nodes;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Verifies run behaviour through outcomes: exit handling, blocking, timeouts, output bounds, quarantine on a runner fault,
/// and run order. Consent is granted for every hook, so each test starts the processes it describes.
/// </summary>
public sealed class HookRunnerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FailingHookDoesNotStopOtherHooks()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, TwoHooks(directory.Path, first: "fail", second: "ok"));
        var runner = new FakeHookProcessRunner((command, _) =>
            Task.FromResult(new HookProcessOutcome(IsArgument(command, "fail") ? 1 : 0, string.Empty, string.Empty)));

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Failed, HookExecutionStatus.Succeeded]);
        result.ActionDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task BlockingExitCodeTwoDeniesTheActionAndSkipsLaterHooks()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, TwoHooks(directory.Path, first: "deny", second: "ok", blockingFirst: true));
        var runner = new FakeHookProcessRunner((command, _) =>
            Task.FromResult(new HookProcessOutcome(IsArgument(command, "deny") ? 2 : 0, string.Empty, string.Empty)));

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Denied, HookExecutionStatus.Skipped]);
        result.ActionDenied.ShouldBeTrue();
        result.DenialReason.ShouldBe(HookDenialReason.ExitCodeTwo);
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task ExitCodeTwoFromANonBlockingHookDoesNotDeny()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, TwoHooks(directory.Path, first: "deny", second: "ok"));
        var runner = new FakeHookProcessRunner((command, _) =>
            Task.FromResult(new HookProcessOutcome(IsArgument(command, "deny") ? 2 : 0, string.Empty, string.Empty)));

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Failed, HookExecutionStatus.Succeeded]);
        result.ActionDenied.ShouldBeFalse();
    }

    [Fact]
    public async Task FailingBlockingHookStopsLaterHooksWithoutDenyingTheAction()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, TwoHooks(directory.Path, first: "fail", second: "ok", blockingFirst: true));
        var runner = new FakeHookProcessRunner((command, _) =>
            Task.FromResult(new HookProcessOutcome(IsArgument(command, "fail") ? 1 : 0, string.Empty, string.Empty)));

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Failed, HookExecutionStatus.Skipped]);
        result.ActionDenied.ShouldBeFalse();
        runner.SpawnCount.ShouldBe(1);
    }

    [Fact]
    public async Task HookThatOutlivesItsTimeoutIsReportedAndLaterHooksStillRun()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, TwoHooks(directory.Path, first: "slow", second: "ok"));
        var runner = new FakeHookProcessRunner((command, cancellationToken) =>
            IsArgument(command, "slow")
                ? WaitUntilCancelledAsync(cancellationToken)
                : Task.FromResult(new HookProcessOutcome(0, string.Empty, string.Empty)));
        var options = new HookRunnerOptions { Timeout = TimeSpan.FromMilliseconds(100) };

        var result = await new HookRunner(host.Hooks, store, runner, options).RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.TimedOut, HookExecutionStatus.Succeeded]);
        result.Executions[0].ExitCode.ShouldBeNull();
    }

    [Fact]
    public async Task HookThatThrowsAsyncIsQuarantinedAndOtherPluginsKeepRunning()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(
            [
                ManifestJson.Source(ManifestJson.Manifest("bad.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/bad"], directory.Path))),
                ManifestJson.Source(ManifestJson.Manifest("good.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/good"], directory.Path))),
            ],
            Token);
        await GrantEverythingAsync(host);
        var runner = new FakeHookProcessRunner(async (command, _) =>
        {
            if (IsArgument(command, "/opt/bad", index: 0))
            {
                await Task.Yield();
                throw new InvalidOperationException("boom in hook");
            }

            return new HookProcessOutcome(0, string.Empty, string.Empty);
        });
        var hookRunner = new HookRunner(host.Hooks, store, runner);

        var first = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);
        var second = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);

        first.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Faulted, HookExecutionStatus.Succeeded]);
        store.FindQuarantine("bad.plugin")!.Reason.ShouldContain("boom in hook");
        second.Executions.Select(execution => execution.Status).ShouldBe([HookExecutionStatus.Skipped, HookExecutionStatus.Succeeded]);
        runner.Spawns.Count(command => IsArgument(command, "/opt/bad", index: 0)).ShouldBe(1);
    }

    [Fact]
    public async Task RunnerThatThrowsSynchronouslyIsQuarantinedInsteadOfCrashingTheHost()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, ManifestJson.Manifest(
            "acme.tools",
            hooks: ManifestJson.Hook("beforeAction", ["/opt/hook"], directory.Path)));

        var result = await new HookRunner(host.Hooks, store, new SynchronouslyThrowingRunner())
            .RunAsync(HookTrigger.BeforeAction, Token);

        result.Executions.Single().Status.ShouldBe(HookExecutionStatus.Faulted);
        store.FindQuarantine("acme.tools").ShouldNotBeNull();
    }

    [Fact]
    public async Task ReorderChangesTheRunOrder()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadTwoPluginsConsentedAsync(store, directory.Path);
        await host.Hooks.MoveAsync("second.plugin#0", 0, Token);
        var runner = new FakeHookProcessRunner();

        await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        runner.Spawns.Select(command => command.Arguments[0]).ShouldBe(["/opt/second", "/opt/first"]);
    }

    [Fact]
    public async Task DisabledHookIsNotStartedUntilItIsEnabledAgain()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadTwoPluginsConsentedAsync(store, directory.Path);
        var runner = new FakeHookProcessRunner();
        var hookRunner = new HookRunner(host.Hooks, store, runner);

        await host.Hooks.SetEnabledAsync("first.plugin#0", enabled: false, Token);
        var whileDisabled = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);
        await host.Hooks.SetEnabledAsync("first.plugin#0", enabled: true, Token);
        var afterEnabling = await hookRunner.RunAsync(HookTrigger.BeforeAction, Token);

        whileDisabled.Executions.Select(execution => execution.HookId).ShouldBe(["second.plugin#0"]);
        afterEnabling.Executions.Select(execution => execution.HookId).ShouldBe(["first.plugin#0", "second.plugin#0"]);
        runner.Spawns.Select(command => command.Arguments[0]).ShouldBe(["/opt/second", "/opt/first", "/opt/second"]);
    }

    [Fact]
    public async Task OutputIsKeptUpToSixteenKibibytesOfUtf8WithoutSplittingACharacter()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadConsentedAsync(store, ManifestJson.Manifest(
            "acme.tools",
            hooks: ManifestJson.Hook("beforeAction", ["/opt/loud"], directory.Path)));
        var runner = new FakeHookProcessRunner((_, _) =>
            Task.FromResult(new HookProcessOutcome(0, new string('\u00e9', 20_000), string.Empty)));

        var result = await new HookRunner(host.Hooks, store, runner).RunAsync(HookTrigger.BeforeAction, Token);

        var execution = result.Executions.Single();
        execution.StandardOutputTruncated.ShouldBeTrue();
        Encoding.UTF8.GetByteCount(execution.StandardOutput).ShouldBeLessThanOrEqualTo(HookOutput.MaxBytes);
        execution.StandardOutput.Length.ShouldBe(HookOutput.MaxBytes / 2);
        execution.StandardOutput.Contains('\uFFFD', StringComparison.Ordinal).ShouldBeFalse();
    }

    [Fact]
    public void DefaultTimeoutIsThirtySeconds()
    {
        new HookRunnerOptions().Timeout.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task NonPositiveTimeoutIsRejected()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);

        Should.Throw<ArgumentOutOfRangeException>(() =>
            _ = new HookRunner(host.Hooks, store, new FakeHookProcessRunner(), new HookRunnerOptions { Timeout = TimeSpan.Zero }));
    }

    private static bool IsArgument(HookCommand command, string value, int index = 1) =>
        string.Equals(command.Arguments[index], value, StringComparison.Ordinal);

    private static JsonObject TwoHooks(string workingDirectory, string first, string second, bool blockingFirst = false) =>
        ManifestJson.Manifest(
            "acme.tools",
            1,
            ManifestJson.Hook("beforeAction", ["/opt/hook", first], workingDirectory, blockingFirst),
            ManifestJson.Hook("beforeAction", ["/opt/hook", second], workingDirectory));

    private static async Task<PluginHost> LoadConsentedAsync(PluginStateStore store, JsonObject manifest)
    {
        var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([ManifestJson.Source(manifest)], Token).ConfigureAwait(false);
        await GrantEverythingAsync(host).ConfigureAwait(false);
        return host;
    }

    private static async Task<PluginHost> LoadTwoPluginsConsentedAsync(PluginStateStore store, string workingDirectory)
    {
        var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(
            [
                ManifestJson.Source(ManifestJson.Manifest("first.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/first"], workingDirectory))),
                ManifestJson.Source(ManifestJson.Manifest("second.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/second"], workingDirectory))),
            ],
            Token).ConfigureAwait(false);
        await GrantEverythingAsync(host).ConfigureAwait(false);
        return host;
    }

    private static async Task GrantEverythingAsync(PluginHost host)
    {
        foreach (var hook in host.Hooks.List())
        {
            await host.Hooks.GrantConsentAsync(hook.Id, Token).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A runner that fails before it returns a task, which a production runner can do too.
    /// </summary>
    private sealed class SynchronouslyThrowingRunner : IHookProcessRunner
    {
        public Task<HookProcessOutcome> RunAsync(HookCommand command, int maxOutputBytes, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the runner cannot start");
    }

    private static async Task<HookProcessOutcome> WaitUntilCancelledAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return new HookProcessOutcome(0, string.Empty, string.Empty);
    }
}
