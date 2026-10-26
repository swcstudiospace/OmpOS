using System.Text;
using OmpOS.Harness.Plugins;
using Shouldly;
using Xunit;

namespace OmpOS.Harness.Automations.Tests;

/// <summary>
/// Verifies the automation consent gate against the real plugin host: an automation gated by a hook runs only after the
/// user grants consent for that hook's exact command.
/// </summary>
public sealed class AutomationPluginConsentTests
{
    private const string HookId = "acme.tools#0";

    private const string ManifestJson =
        """
        {"id":"acme.tools","version":"1.0.0","apiVersion":1,"capabilities":[],"permissions":[],"hooks":[{"trigger":"beforeAction","command":["/opt/hooks/check"],"workingDirectory":"/opt/hooks","blocking":false}]}
        """;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task FirstRunIsRefusedUntilTheHookIsConsentedThroughThePluginHost()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([new PluginManifestSource("acme.json", Encoding.UTF8.GetBytes(ManifestJson))], Token);
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = new AutomationDefinition("sync", new ManualTrigger(), HookId, TimeSpan.FromMinutes(1), Kit.Counting(tally));
        var engine = Kit.Engine(clock, new PluginConsentGate(host.Hooks), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("sync", true);

        var refused = await engine.RunManualAsync("sync", Token);
        refused.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        refused.Guard.ShouldBe("consent");
        tally.Count.ShouldBe(0);

        await host.Hooks.GrantConsentAsync(HookId, Token);
        (await engine.RunManualAsync("sync", Token)).Outcome.ShouldBe(AutomationRunOutcome.Succeeded);
        tally.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AnUnknownHookIsRefusedRatherThanAssumedConsented()
    {
        using var directory = new TempDirectory();
        using var store = await PluginStateStore.OpenAsync(directory.StatePath, null, Token);
        using var host = new PluginHost(store, new DeclarativePluginActivator(), PluginApiVersionRange.Current);
        await host.LoadAsync([new PluginManifestSource("acme.json", Encoding.UTF8.GetBytes(ManifestJson))], Token);
        var clock = new FakeTimeProvider(Kit.Start);
        var tally = new Tally();
        var definition = new AutomationDefinition("orphan", new ManualTrigger(), "missing.plugin#0", TimeSpan.FromMinutes(1), Kit.Counting(tally));
        var engine = Kit.Engine(clock, new PluginConsentGate(host.Hooks), new InMemoryAutomationGuardStore(), definition);
        engine.SetEnabled("orphan", true);

        var run = await engine.RunManualAsync("orphan", Token);

        run.Outcome.ShouldBe(AutomationRunOutcome.Refused);
        run.Guard.ShouldBe("consent");
        tally.Count.ShouldBe(0);
    }
}
