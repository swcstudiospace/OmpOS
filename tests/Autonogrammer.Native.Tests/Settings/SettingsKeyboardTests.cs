using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Presentation.Settings;
using Autonogrammer.Presentation.Settings.Models;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>Keyboard use of the Settings window: the navigation keys, the Tab order, and Esc closing a popup before the window.</summary>
public sealed class SettingsKeyboardTests
{
    private static Control? Focused(SettingsFixture fixture) => fixture.Window.FocusManager?.GetFocusedElement() as Control;

    /// <summary>Up, Down, Home and End move through the pages in model order, and each move shows that page.</summary>
    [Fact]
    public async Task TheNavigationKeysMoveThroughThePagesInOrder()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            _ = window.NavigationEntry(SettingsSection.Runtime)!.Focus(NavigationMethod.Tab);
            fixture.Settle();

            SettingsTree.Press(window, Key.Down, PhysicalKey.ArrowDown);
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.General);
            window.CurrentView.ShouldBeOfType<GeneralPageView>();

            SettingsTree.Press(window, Key.Down, PhysicalKey.ArrowDown);
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.Display);
            window.CurrentView.ShouldBeOfType<DisplayPageView>();

            SettingsTree.Press(window, Key.Up, PhysicalKey.ArrowUp);
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.General);

            SettingsTree.Press(window, Key.End, PhysicalKey.End);
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.Keyboard);
            window.CurrentView.ShouldBeOfType<KeyboardPageView>();

            SettingsTree.Press(window, Key.Home, PhysicalKey.Home);
            fixture.Model.SelectedSection.ShouldBe(SettingsSection.Runtime);
            window.CurrentView.ShouldBeOfType<RuntimePageView>();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>The navigation is one tab stop: Tab leaves it for the page, then reaches the footer buttons that are on, in order.</summary>
    [Fact]
    public async Task TabLeavesTheNavigationForThePageThenTheFooter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            _ = window.NavigationEntry(SettingsSection.Runtime)!.Focus(NavigationMethod.Tab);
            fixture.Settle();

            var names = new List<string?>();
            for (var step = 0; step < 4; step++)
            {
                SettingsTree.PressTab(window);
                names.Add(SettingsTree.FocusedName(window));
            }

            names.ShouldBe([SettingsCopy.IdleStopLabel, SettingsCopy.RetentionLabel, SettingsCopy.ResetLabel, SettingsViewCopy.CloseLabel], "Discard and Save are off while the page is clean");
        }, ct).ConfigureAwait(true);
    }

    private static HashSet<object> Walk(SettingsFixture fixture, Control start, int steps)
    {
        _ = start.Focus(NavigationMethod.Tab);
        fixture.Settle();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (Focused(fixture) is { } first)
        {
            seen.Add(first);
        }

        for (var step = 0; step < steps; step++)
        {
            SettingsTree.PressTab(fixture.Window);
            if (Focused(fixture) is { } focused)
            {
                seen.Add(focused);
            }
        }

        return seen;
    }

    private static void EveryStopIsReached(SettingsFixture fixture, Control page)
    {
        var stops = SettingsTree.TabStops(page).Where(static control => control is not Expander).ToList();
        stops.Count.ShouldBeGreaterThan(5);

        var seen = Walk(fixture, stops[0], stops.Count + 12);

        stops.Where(stop => !seen.Contains(stop)).Select(static stop => stop.GetType().Name + ":" + AutomationProperties.GetName(stop))
            .ShouldBeEmpty("Tab must reach every actionable control of the page");
    }

    /// <summary>Tab reaches every actionable control of the Models page: search, Add provider, every card action and the selectable plan text.</summary>
    [Fact]
    public async Task TabReachesEveryActionableControlOnTheModelsPage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);

            EveryStopIsReached(fixture, view);
        }, ct).ConfigureAwait(true);
    }

    /// <summary>With a key entry and the custom provider form open, Tab still reaches every control, in the order they are drawn.</summary>
    [Fact]
    public async Task TabReachesTheKeyEntryAndTheFormToo()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            SettingsTree.Click(fixture.Window, ModelsPageViewTests.Card(view, "openrouter").ApiKey);
            SettingsTree.Click(fixture.Window, view.AddProvider);

            EveryStopIsReached(fixture, view);

            var names = SettingsTree.TabStops(view).Select(static control => AutomationProperties.GetName(control)).ToList();
            names.IndexOf(ModelsCopy.FieldDisplayName).ShouldBeLessThan(names.IndexOf(ModelsCopy.FieldId));
            names.IndexOf(ModelsCopy.FieldId).ShouldBeLessThan(names.IndexOf(ModelsCopy.FieldEndpoint));
            names.IndexOf(ModelsCopy.FieldEndpoint).ShouldBeLessThan(names.IndexOf(ModelsCopy.FieldModels));
            names.IndexOf(ModelsCopy.FieldModels).ShouldBeLessThan(names.IndexOf(SettingsCopy.SaveLabel));
            names.IndexOf(SettingsCopy.SaveLabel).ShouldBeLessThan(names.IndexOf(ModelsCopy.CancelLabel));
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Tab reaches every control of the editable pages and of the Plugins page.</summary>
    [Fact]
    public async Task TabReachesEveryControlOfTheOtherPages()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            foreach (var section in new[] { SettingsSection.Runtime, SettingsSection.General, SettingsSection.Agents, SettingsSection.Tab, SettingsSection.Plugins })
            {
                var page = fixture.Show(section);
                var stops = SettingsTree.TabStops(page);
                if (stops.Count == 0)
                {
                    continue;
                }

                var seen = Walk(fixture, stops[0], stops.Count + 8);
                stops.Where(stop => !seen.Contains(stop)).ShouldBeEmpty(section.ToString());
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Esc closes an open drop-down first; only the next Esc closes the window.</summary>
    [Fact]
    public async Task EscClosesAnOpenPopupBeforeTheWindow()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var view = (ModelsPageView)fixture.Show(SettingsSection.Models);
            SettingsTree.Click(fixture.Window, view.AddProvider);
            var protocol = view.Form.Protocol;
            _ = protocol.Focus(NavigationMethod.Tab);
            protocol.IsDropDownOpen = true;
            fixture.Settle();
            protocol.IsDropDownOpen.ShouldBeTrue();

            SettingsTree.Press(fixture.Window, Key.Escape, PhysicalKey.Escape);

            protocol.IsDropDownOpen.ShouldBeFalse("the first Esc closes the popup");
            fixture.Model.IsClosed.ShouldBeFalse();
            fixture.Model.IsClosePromptVisible.ShouldBeFalse();

            SettingsTree.Press(fixture.Window, Key.Escape, PhysicalKey.Escape);

            fixture.Model.IsClosed.ShouldBeTrue("the second Esc closes the window");
        }, ct).ConfigureAwait(true);
    }
}
