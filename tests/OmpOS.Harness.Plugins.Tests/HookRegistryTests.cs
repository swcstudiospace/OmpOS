using Shouldly;
using Xunit;

namespace OmpOS.Harness.Plugins.Tests;

/// <summary>
/// Verifies the hook registry's listing, enablement and ordering through consumer-visible state, including that
/// enablement and order survive a restart.
/// </summary>
public sealed class HookRegistryTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ListShowsTriggerCommandAndEnabledStateInRunOrder()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(
        [
            ManifestJson.Source(ManifestJson.Manifest(
                "acme.tools",
                1,
                ManifestJson.Hook("beforeAction", ["/opt/guard", "--mode", "strict"], directory.Path, blocking: true),
                ManifestJson.Hook("afterAction", ["/opt/notify"], directory.Path))),
        ], Token);

        var hooks = host.Hooks.List();

        hooks.Select(hook => hook.Id).ShouldBe(["acme.tools#0", "acme.tools#1"]);
        hooks[0].Trigger.ShouldBe(HookTrigger.BeforeAction);
        hooks[0].Command.Arguments.ShouldBe(["/opt/guard", "--mode", "strict"]);
        hooks[0].Blocking.ShouldBeTrue();
        hooks[0].Enabled.ShouldBeTrue();
        hooks[1].Trigger.ShouldBe(HookTrigger.AfterAction);
        hooks[1].Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task EnablingAndDisablingChangesTheListedState()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadOneHookAsync(store, directory.Path);

        await host.Hooks.SetEnabledAsync("acme.tools#0", enabled: false, Token);
        host.Hooks.List().Single().Enabled.ShouldBeFalse();

        await host.Hooks.SetEnabledAsync("acme.tools#0", enabled: true, Token);
        host.Hooks.List().Single().Enabled.ShouldBeTrue();
    }

    [Fact]
    public async Task DisabledStateSurvivesARestart()
    {
        using var directory = new TempDirectory();
        using (var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token))
        using (var host = await LoadOneHookAsync(store, directory.Path))
        {
            await host.Hooks.SetEnabledAsync("acme.tools#0", enabled: false, Token);
        }

        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var secondHost = await LoadOneHookAsync(reopened, directory.Path);

        secondHost.Hooks.List().Single().Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task ReorderedOrderSurvivesARestart()
    {
        using var directory = new TempDirectory();
        var manifests = new[]
        {
            ManifestJson.Source(ManifestJson.Manifest("first.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/first"], directory.Path))),
            ManifestJson.Source(ManifestJson.Manifest("second.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/second"], directory.Path))),
            ManifestJson.Source(ManifestJson.Manifest("third.plugin", hooks: ManifestJson.Hook("beforeAction", ["/opt/third"], directory.Path))),
        };
        using (var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token))
        using (var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current))
        {
            await host.LoadAsync(manifests, Token);
            await host.Hooks.MoveAsync("third.plugin#0", 0, Token);
            host.Hooks.List().Select(hook => hook.Id).ShouldBe(["third.plugin#0", "first.plugin#0", "second.plugin#0"]);
        }

        using var reopened = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var secondHost = new PluginHost(reopened, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await secondHost.LoadAsync(manifests, Token);

        secondHost.Hooks.List().Select(hook => hook.Id).ShouldBe(["third.plugin#0", "first.plugin#0", "second.plugin#0"]);
    }

    [Fact]
    public async Task UnknownHookIdentifierIsRejected()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadOneHookAsync(store, directory.Path);

        await Should.ThrowAsync<ArgumentException>(() => host.Hooks.SetEnabledAsync("missing#0", enabled: false, Token));
    }

    [Fact]
    public async Task MoveRejectsPositionOutsideTheList()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadOneHookAsync(store, directory.Path);

        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => host.Hooks.MoveAsync("acme.tools#0", 1, Token));
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => host.Hooks.MoveAsync("acme.tools#0", -1, Token));
    }

    [Fact]
    public async Task ConsentShownByTheRegistryFollowsTheExactCommand()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = await LoadOneHookAsync(store, directory.Path);
        host.Hooks.IsConsented("acme.tools#0").ShouldBeFalse();

        await host.Hooks.GrantConsentAsync("acme.tools#0", Token);
        host.Hooks.IsConsented("acme.tools#0").ShouldBeTrue();

        // A plugin that now declares a different command is a new command: the old consent does not carry over.
        using var edited = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await edited.LoadAsync(
            [ManifestJson.Source(ManifestJson.Manifest("acme.tools", hooks: ManifestJson.Hook("beforeAction", ["/opt/other"], directory.Path)))],
            Token);
        edited.Hooks.IsConsented("acme.tools#0").ShouldBeFalse();
    }

    private static async Task<PluginHost> LoadOneHookAsync(PluginStateStore store, string workingDirectory)
    {
        var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync(
            [ManifestJson.Source(ManifestJson.Manifest("acme.tools", hooks: ManifestJson.Hook("beforeAction", ["/opt/hook"], workingDirectory)))],
            Token).ConfigureAwait(false);
        return host;
    }
}
