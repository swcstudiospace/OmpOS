using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>The Network and Keyboard page views: what they draw, that nothing runs by itself, the live region, focus, fonts and keyboard use.</summary>
public sealed class NetworkAndKeyboardPageViewTests
{
    private static string FamilyOfToken(string key) => ((FontFamily)Application.Current!.FindResource(key)!).Name;

    private static TextBlock Block(Control root, string text) =>
        SettingsTree.Visible<TextBlock>(root).First(block => string.Equals(block.Text, text, StringComparison.Ordinal));

    /// <summary>The Keyboard page lists the shortcuts under one heading per scope, in the fixed scope order, each row named with its action and keys.</summary>
    [Fact]
    public async Task TheKeyboardPageListsTheShortcutsByScope()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (KeyboardPageView)fixture.Show(SettingsSection.Keyboard);
            var texts = SettingsTree.Texts(view);

            texts.ShouldContain(KeyboardCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain(KeyboardCopy.Description, StringComparer.Ordinal);
            view.Empty.IsVisible.ShouldBeFalse();
            var scopes = new[] { KeyboardCopy.ScopeApplication, KeyboardCopy.ScopeAgents, KeyboardCopy.ScopeComposer };
            scopes.Select(scope => texts.ToList().IndexOf(scope)).ShouldBeInOrder(SortDirection.Ascending);
            scopes.ShouldAllBe(scope => texts.Contains(scope, StringComparer.Ordinal));
            texts.ShouldContain("Ctrl+N", StringComparer.Ordinal);
            var names = SettingsTree.Descriptions(view);
            names.ShouldContain("Start a new agent: Ctrl+N", StringComparer.Ordinal);
            names.ShouldContain("Open Settings: Ctrl+,", StringComparer.Ordinal);
            AutomationProperties.GetName(fixture.Window.NavigationEntry(SettingsSection.Keyboard)!).ShouldBe(KeyboardCopy.Title);
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty();
            SettingsTree.TabStops(view).ShouldBeEmpty("a read-only list has no control to tab to");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A source that lists no shortcut leaves the heading and says so.</summary>
    [Fact]
    public async Task AnEmptyKeyboardListSaysSo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.KeyboardSource.Values = new KeyboardShortcutsSnapshot([])).ConfigureAwait(true);
            var view = (KeyboardPageView)fixture.Show(SettingsSection.Keyboard);

            view.Empty.IsVisible.ShouldBeTrue();
            SettingsTree.Texts(view).ShouldContain(KeyboardCopy.Empty, StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Keys are drawn in Space Mono, the heading in Anybody and the words in DM Sans, each through its token.</summary>
    [Fact]
    public async Task TheKeyboardPageUsesTheThreeFamilies()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (KeyboardPageView)fixture.Show(SettingsSection.Keyboard);

            Block(view, "Ctrl+N").FontFamily.Name.ShouldBe(FamilyOfToken("Autonogrammer.Font.Mono"));
            Block(view, KeyboardCopy.Heading).FontFamily.Name.ShouldBe(FamilyOfToken("Autonogrammer.Font.Display"));
            Block(view, "Start a new agent").FontFamily.Name.ShouldBe(FamilyOfToken("Autonogrammer.Font.Family"));
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Opening the Network page runs nothing: the explanation shows, the button is on, and no result is announced.</summary>
    [Fact]
    public async Task TheNetworkPageRunsNothingByItself()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (NetworkPageView)fixture.Show(SettingsSection.Network);
            var texts = SettingsTree.Texts(view);

            fixture.NetworkSource.RunCount.ShouldBe(0);
            texts.ShouldContain(NetworkCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain(NetworkCopy.Explanation, StringComparer.Ordinal);
            texts.ShouldContain(NetworkCopy.NotRun, StringComparer.Ordinal);
            view.Run.IsEnabled.ShouldBeTrue();
            view.CancelRun.IsVisible.ShouldBeFalse();
            view.Announcement.IsVisible.ShouldBeFalse();
            AutomationProperties.GetName(view.Run).ShouldBe(NetworkCopy.RunLabel);
            SettingsTree.TabStops(view).ShouldBe([view.Run]);
            fixture.Window.Save.IsVisible.ShouldBeFalse("the page has no Save");
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Run diagnostics draws one named row per endpoint, announces the result in a polite live region and shows when it was made.</summary>
    [Fact]
    public async Task RunningTheDiagnosticsDrawsTheResultInALiveRegion()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (NetworkPageView)fixture.Show(SettingsSection.Network);

            SettingsTree.Click(fixture.Window, view.Run);
            fixture.Settle();
            var texts = SettingsTree.Texts(view);

            fixture.NetworkSource.RunCount.ShouldBe(1);
            view.NotRun.IsVisible.ShouldBeFalse();
            view.Announcement.IsVisible.ShouldBeTrue();
            view.Announcement.Text.ShouldBe("Diagnostics finished. 1 of 2 endpoints reachable.");
            AutomationProperties.GetLiveSetting(view.Announcement).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(view.Announcement).ShouldBe(NetworkCopy.AnnouncementName);
            texts.ShouldContain("openrouter", StringComparer.Ordinal);
            texts.ShouldContain("openrouter.ai", StringComparer.Ordinal);
            texts.ShouldContain(NetworkCopy.StatusReachable, StringComparer.Ordinal);
            texts.ShouldContain(NetworkCopy.StatusUnreachable, StringComparer.Ordinal);
            texts.ShouldContain("Checked 2026-10-26 09:30:00 UTC.", StringComparer.Ordinal);
            SettingsTree.Descriptions(view).ShouldContain("openrouter, openrouter.ai: Reachable. The endpoint answered with HTTP 401. 120 ms.", StringComparer.Ordinal);
            Block(view, "localhost:11434").FontFamily.Name.ShouldBe(FamilyOfToken("Autonogrammer.Font.Mono"));
            SettingsTree.Unnamed(fixture.Window).ShouldBeEmpty();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>While a run is in progress Run is off and Cancel is on with the focus; Cancel ends the run, announces it and gives the focus back to Run.</summary>
    [Fact]
    public async Task ARunInProgressCanBeCancelled()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(
                ct,
                static f => f.NetworkSource.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ConfigureAwait(true);
            var view = (NetworkPageView)fixture.Show(SettingsSection.Network);

            SettingsTree.Click(fixture.Window, view.Run);
            fixture.Settle();

            view.Run.IsEnabled.ShouldBeFalse("a second run cannot start");
            view.CancelRun.IsVisible.ShouldBeTrue();
            view.Announcement.Text.ShouldBe(NetworkCopy.Running);
            SettingsTree.FocusedName(fixture.Window).ShouldBe(NetworkCopy.CancelLabel);

            SettingsTree.Click(fixture.Window, view.CancelRun);
            fixture.Settle();

            view.Announcement.Text.ShouldBe(NetworkCopy.Cancelled);
            view.Run.IsEnabled.ShouldBeTrue();
            view.CancelRun.IsVisible.ShouldBeFalse();
            SettingsTree.FocusedName(fixture.Window).ShouldBe(NetworkCopy.RunLabel);
            fixture.NetworkSource.RunCount.ShouldBe(1);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A run that fails shows the reason in the live region and leaves the button on; nothing is thrown.</summary>
    [Fact]
    public async Task AFailedRunIsAnnouncedAndTheButtonStaysOn()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.NetworkSource.Failure = "The provider list could not be read.").ConfigureAwait(true);
            var view = (NetworkPageView)fixture.Show(SettingsSection.Network);

            SettingsTree.Click(fixture.Window, view.Run);
            fixture.Settle();

            view.Announcement.Text.ShouldBe("The provider list could not be read.");
            view.Run.IsEnabled.ShouldBeTrue();
            view.Results.ItemCount.ShouldBe(0);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A report with no endpoint says so and draws no row.</summary>
    [Fact]
    public async Task AReportWithNoEndpointDrawsNoRow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(
                ct,
                static f => f.NetworkSource.Report = new NetworkDiagnosticsReport([], DateTimeOffset.UnixEpoch)).ConfigureAwait(true);
            var view = (NetworkPageView)fixture.Show(SettingsSection.Network);

            SettingsTree.Click(fixture.Window, view.Run);
            fixture.Settle();

            view.Announcement.Text.ShouldBe(NetworkCopy.NoEndpoints);
            view.Results.ItemCount.ShouldBe(0);
        }, ct).ConfigureAwait(true);
    }
}
