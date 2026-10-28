using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.Settings;
using OmpOS.Native.Views.Hooks;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Tests.Hooks;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Hooks;

/// <summary>The Hooks page view: which block of the main area shows for each state, the list by scope, the consent card, and that every control is named and large enough.</summary>
public sealed class HooksPageViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<(Window Window, HooksPageView View, HooksSettingsPage Page, ScriptedHooksPageSource Source)> ShowAsync(HooksSnapshot snapshot)
    {
        var source = new ScriptedHooksPageSource { Snapshot = snapshot };
        var page = new HooksSettingsPage(SettingsSection.Plugins, source, TimeZoneInfo.Utc);
        await page.LoadAsync(Token).ConfigureAwait(true);
        var view = new HooksPageView { DataContext = page };
        var window = new Window { Width = 900, Height = 1800, Content = view };
        window.Show();
        Settle(window);
        return (window, view, page, source);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.GetLayoutManager()?.ExecuteLayoutPass();
        Dispatcher.UIThread.RunJobs();
    }

    private static async Task ClickAsync(Window window, Button button)
    {
        SettingsTree.Click(window, button);
        await Task.Yield();
        Settle(window);
    }

    /// <summary>A listed set of hooks shows the list block only, one group per scope and file, named rows, and a named Review button and switch for each hook.</summary>
    [Fact]
    public async Task TheListShowsOnlyTheListBlockAndNamesEveryControl()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (window, view, _, _) = await ShowAsync(HookItems.Snapshot(
                HookItems.Hook("guard", file: "/work/.cursor/hooks.json"),
                HookItems.Hook("audit", "afterAgentResponse", HookScopeKind.User, "/home/u/.ompos/hooks.json") with { IsConsented = false })).ConfigureAwait(true);

            view.List.IsVisible.ShouldBeTrue();
            view.Off.IsVisible.ShouldBeFalse();
            view.Untrusted.IsVisible.ShouldBeFalse();
            view.Invalid.IsVisible.ShouldBeFalse();
            view.Empty.IsVisible.ShouldBeFalse();
            view.Groups.ItemCount.ShouldBe(2);
            view.RunHooks.IsChecked.ShouldBe(true);
            var texts = SettingsTree.Texts(view);
            texts.ShouldContain(HooksCopy.ScopeTitle(HookScopeKind.Project), StringComparer.Ordinal);
            texts.ShouldContain(HooksCopy.ScopeTitle(HookScopeKind.User), StringComparer.Ordinal);
            texts.ShouldContain("guard", StringComparer.Ordinal);
            texts.ShouldContain(HooksCopy.StateText(HookRowState.NeedsConsent), StringComparer.Ordinal);
            SettingsTree.Has<Button>(view, "Review guard").ShouldBeTrue();
            SettingsTree.Has<CheckBox>(view, HooksCopy.EnabledLabel + ": audit").ShouldBeTrue();
            SettingsTree.Has<Button>(view, HooksCopy.TrustAllLabel).ShouldBeTrue();
            SettingsTree.Unnamed(view).ShouldBeEmpty();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>With no hook file the empty block shows the explanation and the example file, whose button is named.</summary>
    [Fact]
    public async Task TheEmptyStateOffersTheExampleFile()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (window, view, _, _) = await ShowAsync(HookItems.Snapshot()).ConfigureAwait(true);

            view.Empty.IsVisible.ShouldBeTrue();
            view.List.IsVisible.ShouldBeFalse();
            view.Example.Text.ShouldBe(HooksCopy.ExampleJson);
            AutomationProperties.GetName(view.CopyExample).ShouldBe(HooksCopy.CopyExampleLabel);
            SettingsTree.Unnamed(view).ShouldBeEmpty();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>An untrusted folder shows the trust block; pressing Trust this folder calls the source and then lists the hooks.</summary>
    [Fact]
    public async Task TrustingTheFolderListsItsHooks()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (window, view, _, source) = await ShowAsync(HookItems.Snapshot() with { WithheldFiles = ["/work/.cursor/hooks.json"] }).ConfigureAwait(true);
            view.Untrusted.IsVisible.ShouldBeTrue();
            source.Snapshot = source.Snapshot with { Hooks = [HookItems.Hook("guard") with { IsConsented = false }] };

            await ClickAsync(window, view.TrustFolder).ConfigureAwait(true);

            source.Calls.ShouldContain("TrustWorkspace", StringComparer.Ordinal);
            view.Untrusted.IsVisible.ShouldBeFalse();
            view.List.IsVisible.ShouldBeTrue();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Unchecking Run hooks calls the source and shows the switched-off block in place of the list.</summary>
    [Fact]
    public async Task TheRunHooksSwitchShowsTheOffState()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (window, view, _, source) = await ShowAsync(HookItems.Snapshot(HookItems.Hook("guard"))).ConfigureAwait(true);

            view.RunHooks.IsChecked = false;
            await Task.Yield();
            Settle(window);

            source.Calls.ShouldContain("SetRunHooks(False)", StringComparer.Ordinal);
            view.Off.IsVisible.ShouldBeTrue();
            view.List.IsVisible.ShouldBeFalse();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Review opens the consent card with the exact command, Approve records consent through the source, and the card then offers to withdraw it.</summary>
    [Fact]
    public async Task ReviewingAHookShowsItsCommandAndApproveRecordsConsent()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (window, view, _, source) = await ShowAsync(HookItems.Snapshot(HookItems.Hook("guard", command: "./block-force-push.sh") with { IsConsented = false })).ConfigureAwait(true);
            view.Consent.IsVisible.ShouldBeFalse();

            await ClickAsync(window, SettingsTree.Named<Button>(view, "Review guard")).ConfigureAwait(true);

            view.Consent.IsVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain(HooksCopy.ConsentHeading, StringComparer.Ordinal);
            SettingsTree.Everything(view).ShouldContain("./block-force-push.sh", StringComparer.Ordinal);
            SettingsTree.Unnamed(view).ShouldBeEmpty();

            await ClickAsync(window, SettingsTree.Named<Button>(view, HooksCopy.ApproveLabel)).ConfigureAwait(true);

            source.Calls.ShouldContain("Approve(guard)", StringComparer.Ordinal);
            SettingsTree.Named<Button>(view, HooksCopy.RevokeLabel).IsEnabled.ShouldBeTrue();
            SettingsTree.Named<Button>(view, HooksCopy.ApproveLabel).IsEnabled.ShouldBeFalse();
            window.Close();
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Every visible button, switch and field of the page is at least the minimum target size and the diagnostics list names its rows.</summary>
    [Fact]
    public async Task EveryInteractiveControlMeetsTheTargetSize()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var diagnostic = new HookDiagnosticItem("/work/.cursor/hooks.json", "$.hooks.stop[0]", "loop_limit is only for stop hooks", IsError: false);
            var (window, view, _, _) = await ShowAsync(HookItems.Snapshot(HookItems.Hook("guard") with { IsConsented = false }) with { Diagnostics = [diagnostic] }).ConfigureAwait(true);
            await ClickAsync(window, SettingsTree.Named<Button>(view, "Review guard")).ConfigureAwait(true);

            var targets = SettingsTree.Visible<Button>(view).Cast<Control>().Concat(SettingsTree.Visible<CheckBox>(view)).Where(static c => c.TemplatedParent is null).ToList();
            targets.ShouldNotBeEmpty();
            foreach (var target in targets)
            {
                target.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(target));
                target.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(target));
            }

            SettingsTree.Descriptions(view).ShouldContain(diagnostic.Summary, StringComparer.Ordinal);
            view.Diagnostics.ItemCount.ShouldBe(1);
            window.Close();
        }, Token).ConfigureAwait(true);
    }
}
