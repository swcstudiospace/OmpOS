using Avalonia.Input;
using Autonogrammer.Harness.Plugins;
using Autonogrammer.Native.Views.Hooks;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>
/// The consent panel shows the selected hook's exact command, and its Approve button records consent for that command only. The
/// view itself never starts a hook process.
/// </summary>
public sealed class HooksConsentTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Approve records consent for the command the panel shows, and for no other hook. The approved hook then runs with exactly that
    /// command; the other stays awaiting consent.
    /// </summary>
    [Fact]
    public async Task ApproveConsentCallsTheConsentApiForTheShownCommandOnly()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin(
                    "acme.tools",
                    HookManifests.Hook("beforeAction", ["/srv/alpha/guard", "--strict"], "/srv/alpha", blocking: true)),
                HookManifests.Plugin("other.tools", HookManifests.Hook("afterAction", ["/srv/beta/notify"], "/srv/beta"))).ConfigureAwait(true);
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            await screen.ActivateAsync("Review consent for hook other.tools#0", Key.Enter).ConfigureAwait(true);

            screen.Text("ConsentArguments").Text.ShouldBe("0: /srv/beta/notify");
            screen.Text("ConsentDirectory").Text.ShouldBe("Working directory: /srv/beta");
            screen.FindButton("Approve consent for hook other.tools#0").ShouldNotBeNull();

            await screen.ActivateAsync("Approve consent for hook other.tools#0", Key.Enter).ConfigureAwait(true);

            workbench.Registry.IsConsented("other.tools#0").ShouldBeTrue();
            workbench.Registry.IsConsented("acme.tools#0").ShouldBeFalse();
            RowFor(screen, "acme.tools#0").IsConsented.ShouldBeFalse();

            var result = await workbench.RunAsync(HookTrigger.AfterAction, Token).ConfigureAwait(true);
            result.Executions.Select(execution => execution.HookId).ShouldBe(["other.tools#0"]);
            workbench.ProcessRunner.Spawns.Count.ShouldBe(1);
            workbench.ProcessRunner.Spawns[0].Arguments.ShouldBe(["/srv/beta/notify"]);
            workbench.ProcessRunner.Spawns[0].WorkingDirectory.ShouldBe("/srv/beta");

            var beforeResult = await workbench.RunAsync(HookTrigger.BeforeAction, Token).ConfigureAwait(true);
            beforeResult.Executions.Single().Status.ShouldBe(HookExecutionStatus.AwaitingConsent);
            workbench.ProcessRunner.SpawnCount.ShouldBe(1);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>The consent panel lists every argument on its own line with its index, quoted where the text would be ambiguous.</summary>
    [Fact]
    public async Task ConsentPanelShowsTheExactArgumentListAndWorkingDirectory()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var workbench = await HookWorkbench.OpenAsync(
                Token,
                null,
                HookManifests.Plugin(
                    "acme.tools",
                    HookManifests.Hook("sessionStart", ["/srv/check dir/run", "--name", "a\"b"], "/srv/check dir"))).ConfigureAwait(true);
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            await screen.ActivateAsync("Review consent for hook acme.tools#0", Key.Enter).ConfigureAwait(true);

            screen.Text("ConsentArguments").Text.ShouldBe(
                "0: \"/srv/check dir/run\"\n1: --name\n2: \"a\\\"b\"");
            screen.Text("ConsentDirectory").Text.ShouldBe("Working directory: /srv/check dir");
            screen.Text("ConsentTrigger").Text.ShouldStartWith("Trigger: sessionStart.");
            RowFor(screen, "acme.tools#0").CommandText.ShouldBe("Command: \"/srv/check dir/run\" --name \"a\\\"b\"");
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every change the view offers, and the consent step, starts no hook process.</summary>
    [Fact]
    public async Task TheViewNeverStartsAHookProcess()
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
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);

            await screen.ActivateAsync("Review consent for hook acme.tools#1", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Approve consent for hook acme.tools#1", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Enable hook acme.tools#0", Key.Space).ConfigureAwait(true);
            await screen.ActivateAsync("Disable hook acme.tools#0", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Move down hook acme.tools#0", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Move up hook acme.tools#0", Key.Enter).ConfigureAwait(true);
            await screen.ActivateAsync("Review consent for hook acme.tools#0", Key.Enter).ConfigureAwait(true);

            workbench.ProcessRunner.SpawnCount.ShouldBe(0);

            // The counter is live: a run of the registry does start the consented, enabled hook.
            var result = await workbench.RunAsync(HookTrigger.BeforeAction, Token).ConfigureAwait(true);
            result.Executions.Select(execution => execution.HookId).ShouldBe(["acme.tools#1"]);
            workbench.ProcessRunner.SpawnCount.ShouldBe(1);
        }, Token).ConfigureAwait(true);
    }

    private static HookRow RowFor(HooksScreen screen, string id) =>
        screen.Model.Rows.Single(row => string.Equals(row.Id, id, StringComparison.Ordinal));
}
