using Autonogrammer.Harness.Plugins;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>The hooks view draws each of its five states explicitly, and draws exactly one of them at a time.</summary>
public sealed class HooksStateTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>While the registry loads, the view says so and draws no list; once loaded, it draws the list.</summary>
    [Fact]
    public async Task LoadingStateIsDrawnWhileTheRegistryLoads()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin("acme.tools", HookManifests.Hook("beforeAction", ["/srv/hooks/guard"], "/srv/hooks"))).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("acme.tools#0", Token).ConfigureAwait(true);
            using var gate = new SemaphoreSlim(0, 1);
            var source = new TestHookSource(workbench.Source, async cancellationToken => await gate.WaitAsync(cancellationToken).ConfigureAwait(true));

            var screen = HooksScreen.Show(source);

            screen.Text("StateHeading").Text.ShouldBe("Loading hooks");
            screen.IsShown("LoadingText").ShouldBeTrue();
            screen.IsShown("ListPanel").ShouldBeFalse();
            screen.IsShown("EmptyText").ShouldBeFalse();
            screen.IsShown("ErrorText").ShouldBeFalse();

            gate.Release();
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            screen.Text("StateHeading").Text.ShouldBe("Hooks");
            screen.IsShown("LoadingText").ShouldBeFalse();
            screen.IsShown("ListPanel").ShouldBeTrue();
            screen.Model.Rows.Select(row => row.Id).ShouldBe(["acme.tools#0"]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A registry with no hooks says so, and draws no list.</summary>
    [Fact]
    public async Task EmptyStateSaysThereAreNoHooks()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(Token).ConfigureAwait(true);

            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            screen.Text("StateHeading").Text.ShouldBe("No hooks");
            screen.IsShown("EmptyText").ShouldBeTrue();
            screen.IsShown("ListPanel").ShouldBeFalse();
            screen.IsShown("ErrorText").ShouldBeFalse();
            screen.IsShown("LoadingText").ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A failed load says so and shows the reason the load gave.</summary>
    [Fact]
    public async Task ErrorStateShowsTheLoadFailureReason()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(Token).ConfigureAwait(true);
            var failing = new TestHookSource(
                workbench.Source,
                _ => Task.FromException(new InvalidOperationException("The plugin state file is corrupt.")));

            var screen = HooksScreen.Show(failing);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            screen.Text("StateHeading").Text.ShouldBe("Hooks could not be loaded");
            screen.IsShown("ErrorText").ShouldBeTrue();
            screen.Text("ErrorText").Text.ShouldBe("Hooks could not be loaded. Reason: The plugin state file is corrupt.");
            screen.IsShown("ListPanel").ShouldBeFalse();
            screen.IsShown("EmptyText").ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>When every hook is consented and no plugin is quarantined, the list is drawn with no degraded marker.</summary>
    [Fact]
    public async Task SuccessStateDrawsEveryHookWithoutDegradedMarkers()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin(
                    "acme.tools",
                    HookManifests.Hook("beforeAction", ["/srv/hooks/guard", "--strict"], "/srv/hooks"),
                    HookManifests.Hook("afterAction", ["/srv/hooks/notify"], "/srv/hooks"))).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("acme.tools#0", Token).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("acme.tools#1", Token).ConfigureAwait(true);

            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            screen.Text("StateHeading").Text.ShouldBe("Hooks");
            screen.Text("SummaryText").Text.ShouldBe(
                "2 hooks in run order. A hook runs only after consent for its exact command.");
            screen.Model.Rows.Select(row => row.Id).ShouldBe(["acme.tools#0", "acme.tools#1"]);
            screen.Model.Rows.Any(row => row.IsDegraded).ShouldBeFalse();
            screen.DrawnDegradedMarkers().ShouldBe(0);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// A hook awaiting consent and a hook whose plugin is quarantined are each marked degraded, with the reason. A consented hook
    /// of a healthy plugin is not marked.
    /// </summary>
    [Fact]
    public async Task DegradedStateMarksHooksAwaitingConsentAndQuarantinedPlugins()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin("good.plugin", HookManifests.Hook("beforeAction", ["/srv/hooks/good"], "/srv/hooks")),
                HookManifests.Plugin("pending.plugin", HookManifests.Hook("beforeAction", ["/srv/hooks/pending"], "/srv/hooks")),
                HookManifests.Plugin("broken.plugin", HookManifests.Hook("afterAction", ["/srv/hooks/broken"], "/srv/hooks"))).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("good.plugin#0", Token).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("broken.plugin#0", Token).ConfigureAwait(true);
            await workbench.State.QuarantineAsync("broken.plugin", "The hook runner faulted.", Token).ConfigureAwait(true);

            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            screen.Text("StateHeading").Text.ShouldBe("Hooks need attention");
            screen.Text("SummaryText").Text.ShouldBe("3 hooks in run order; 2 degraded and will not run.");
            screen.DrawnDegradedMarkers().ShouldBe(2);

            var rows = screen.Model.Rows.ToDictionary(row => row.Id, StringComparer.Ordinal);
            rows["good.plugin#0"].IsDegraded.ShouldBeFalse();
            rows["pending.plugin#0"].BlockedBy.ShouldBe(HookExecutionStatus.AwaitingConsent);
            rows["pending.plugin#0"].DegradedText.ShouldContain("awaiting consent");
            rows["broken.plugin#0"].BlockedBy.ShouldBe(HookExecutionStatus.PluginQuarantined);
            rows["broken.plugin#0"].DegradedText.ShouldContain("broken.plugin is quarantined (The hook runner faulted.)");
        }, Token).ConfigureAwait(true);
    }
}
