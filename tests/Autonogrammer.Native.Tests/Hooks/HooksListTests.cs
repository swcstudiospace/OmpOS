using Avalonia.Input;
using Autonogrammer.Harness.Plugins;
using Autonogrammer.Native.Views.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>Move up, Move down, Enable and Disable change the registry and what the list draws, and a disabled hook never runs.</summary>
public sealed class HooksListTests
{
    private const string EnablePrefix = "Enable hook ";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Move down and Move up reorder the registry and the drawn rows; the edges of the list do nothing.</summary>
    [Fact]
    public async Task MoveDownAndMoveUpReorderTheRegistryAndTheRows()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin(
                    "acme.tools",
                    HookManifests.Hook("beforeAction", ["/srv/hooks/one"], "/srv/hooks"),
                    HookManifests.Hook("beforeAction", ["/srv/hooks/two"], "/srv/hooks"),
                    HookManifests.Hook("beforeAction", ["/srv/hooks/three"], "/srv/hooks"))).ConfigureAwait(true);
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);
            RowOrder(screen).ShouldBe(["acme.tools#0", "acme.tools#1", "acme.tools#2"]);

            await screen.ActivateAsync("Move down hook acme.tools#0", Key.Enter).ConfigureAwait(true);

            RowOrder(screen).ShouldBe(["acme.tools#1", "acme.tools#0", "acme.tools#2"]);
            workbench.Registry.List().Select(hook => hook.Id).ShouldBe(["acme.tools#1", "acme.tools#0", "acme.tools#2"]);

            await screen.ActivateAsync("Move up hook acme.tools#0", Key.Space).ConfigureAwait(true);

            RowOrder(screen).ShouldBe(["acme.tools#0", "acme.tools#1", "acme.tools#2"]);
            workbench.Registry.List().Select(hook => hook.Id).ShouldBe(["acme.tools#0", "acme.tools#1", "acme.tools#2"]);

            await screen.ActivateAsync("Move up hook acme.tools#0", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Move down hook acme.tools#2", Key.Enter).ConfigureAwait(true);

            RowOrder(screen).ShouldBe(["acme.tools#0", "acme.tools#1", "acme.tools#2"]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A disabled hook is not run by a later trigger; enabling it again runs it again.</summary>
    [Fact]
    public async Task DisableStopsTheHookFromRunningLater()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin(
                    "acme.tools",
                    HookManifests.Hook("beforeAction", ["/srv/hooks/first"], "/srv/hooks"),
                    HookManifests.Hook("beforeAction", ["/srv/hooks/second"], "/srv/hooks"))).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("acme.tools#0", Token).ConfigureAwait(true);
            await workbench.Registry.GrantConsentAsync("acme.tools#1", Token).ConfigureAwait(true);
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            await screen.ActivateAsync("Disable hook acme.tools#0", Key.Enter).ConfigureAwait(true);

            RowFor(screen, "acme.tools#0").IsEnabled.ShouldBeFalse();
            var afterDisable = await workbench.RunAsync(HookTrigger.BeforeAction, Token).ConfigureAwait(true);
            afterDisable.Executions.Select(execution => execution.HookId).ShouldBe(["acme.tools#1"]);
            workbench.ProcessRunner.Spawns.Select(command => command.Arguments.Single()).ShouldBe(["/srv/hooks/second"]);

            await screen.ActivateAsync("Enable hook acme.tools#0", Key.Space).ConfigureAwait(true);

            var afterEnable = await workbench.RunAsync(HookTrigger.BeforeAction, Token).ConfigureAwait(true);
            afterEnable.Executions.Select(execution => execution.HookId).ShouldBe(["acme.tools#0", "acme.tools#1"]);
            workbench.ProcessRunner.SpawnCount.ShouldBe(3);
        }, Token).ConfigureAwait(true);
    }

    private static string[] RowOrder(HooksScreen screen) => screen.ButtonNames()
        .Where(name => name.StartsWith(EnablePrefix, StringComparison.Ordinal))
        .Select(name => name[EnablePrefix.Length..])
        .ToArray();

    private static HookRow RowFor(HooksScreen screen, string id) =>
        screen.Model.Rows.Single(row => string.Equals(row.Id, id, StringComparison.Ordinal));
}
