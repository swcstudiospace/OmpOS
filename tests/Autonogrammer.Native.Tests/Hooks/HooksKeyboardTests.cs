using Avalonia.Input;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Hooks;

/// <summary>Every hooks control is reached by Tab, and Enter and Space both activate its buttons.</summary>
public sealed class HooksKeyboardTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Tab visits each row's five controls in order, then the consent panel's Approve button.</summary>
    [Fact]
    public async Task TabVisitsEveryRowControlThenApprove()
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
                    HookManifests.Hook("afterAction", ["/srv/hooks/second"], "/srv/hooks"))).ConfigureAwait(true);
            var screen = HooksScreen.Show(workbench.Source);
            await screen.WaitForIdleAsync().ConfigureAwait(true);
            screen.Focus("Enable hook acme.tools#0");

            var visited = new List<string?> { screen.FocusedButtonName() };
            for (var press = 0; press < 10; press++)
            {
                screen.PressTab();
                visited.Add(screen.FocusedButtonName());
            }

            visited.ShouldBe(
            [
                "Enable hook acme.tools#0",
                "Disable hook acme.tools#0",
                "Move up hook acme.tools#0",
                "Move down hook acme.tools#0",
                "Review consent for hook acme.tools#0",
                "Enable hook acme.tools#1",
                "Disable hook acme.tools#1",
                "Move up hook acme.tools#1",
                "Move down hook acme.tools#1",
                "Review consent for hook acme.tools#1",
                "Approve consent",
            ]);
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Enter and Space each activate a button; focus stays on the activated control so Tab continues from it.</summary>
    [Fact]
    public async Task EnterAndSpaceBothActivateButtons()
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

            await screen.ActivateAsync("Disable hook acme.tools#0", Key.Enter).ConfigureAwait(true);
            screen.Model.Rows.Single(row => string.Equals(row.Id, "acme.tools#0", StringComparison.Ordinal)).IsEnabled.ShouldBeFalse();
            screen.FocusedButtonName().ShouldBe("Disable hook acme.tools#0");

            await screen.ActivateAsync("Enable hook acme.tools#0", Key.Space).ConfigureAwait(true);
            screen.Model.Rows.Single(row => string.Equals(row.Id, "acme.tools#0", StringComparison.Ordinal)).IsEnabled.ShouldBeTrue();

            await screen.ActivateAsync("Move down hook acme.tools#0", Key.Space).ConfigureAwait(true);
            screen.Model.Rows.Select(row => row.Id).ShouldBe(["acme.tools#1", "acme.tools#0"]);

            await screen.ActivateAsync("Review consent for hook acme.tools#1", Key.Enter).ConfigureAwait(true);
            screen.Model.Selected.ShouldNotBeNull();
            screen.Model.Selected.Id.ShouldBe("acme.tools#1");

            await screen.ActivateAsync("Approve consent for hook acme.tools#1", Key.Space).ConfigureAwait(true);
            workbench.Registry.IsConsented("acme.tools#1").ShouldBeTrue();
            workbench.Registry.IsConsented("acme.tools#0").ShouldBeFalse();
        }, Token).ConfigureAwait(true);
    }
}
