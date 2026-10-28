using Avalonia.Automation;
using Avalonia.Controls;
using OmpOS.Native.Views.Cloud;
using OmpOS.Native.Views.Hooks;
using OmpOS.Native.Views.Mcp;
using OmpOS.Native.Views.Remote;
using OmpOS.Native.Views.Safety;
using OmpOS.Native.Views.Settings;
using OmpOS.Presentation;
using OmpOS.Presentation.Cloud;
using OmpOS.Presentation.Hooks;
using OmpOS.Presentation.Mcp;
using OmpOS.Presentation.Remote;
using OmpOS.Presentation.Safety;
using OmpOS.Presentation.Settings;
using OmpOS.Presentation.Settings.Customize;
using OmpOS.Presentation.Settings.Ignore;
using OmpOS.Presentation.Settings.Models;
using OmpOS.Presentation.Settings.Privacy;
using OmpOS.Presentation.Settings.Workspaces;
using OmpOS.Presentation.Settings.Worktrees;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Settings;

/// <summary>The Settings window: its navigation, the page it shows, the footer buttons and what they do to a page.</summary>
public sealed class SettingsWindowTests
{
    private static readonly (SettingsSection Section, string Title, Type View)[] Pages =
    [
        (SettingsSection.Runtime, SettingsCopy.RuntimeTitle, typeof(RuntimePageView)),
        (SettingsSection.General, SettingsCopy.GeneralTitle, typeof(GeneralPageView)),
        (SettingsSection.Display, DisplayCopy.Title, typeof(DisplayPageView)),
        (SettingsSection.Agents, SettingsCopy.AgentsTitle, typeof(AgentsPageView)),
        (SettingsSection.RunMode, SafetyCopy.Title, typeof(RunModePageView)),
        (SettingsSection.Models, ModelsCopy.PageTitle, typeof(ModelsPageView)),
        (SettingsSection.Tab, SettingsCopy.TabTitle, typeof(TabPageView)),
        (SettingsSection.Rules, CustomizeCopy.RulesTitle, typeof(CustomizeCatalogView)),
        (SettingsSection.Skills, CustomizeCopy.SkillsTitle, typeof(CustomizeCatalogView)),
        (SettingsSection.Commands, CustomizeCopy.CommandsTitle, typeof(CustomizeCatalogView)),
        (SettingsSection.Hooks, HooksCopy.Title, typeof(HooksPageView)),
        (SettingsSection.Mcp, McpCopy.Title, typeof(McpPageView)),
        (SettingsSection.Plugins, SettingsCopy.PluginsTitle, typeof(PluginsPageView)),
        (SettingsSection.Workspaces, WorkspacesCopy.Title, typeof(WorkspacesPageView)),
        (SettingsSection.Worktrees, WorktreesCopy.Title, typeof(WorktreesPageView)),
        (SettingsSection.Ignore, IgnoreCopy.Title, typeof(IgnorePageView)),
        (SettingsSection.Remote, RemoteCopy.Title, typeof(RemoteEnvironmentsPageView)),
        (SettingsSection.Cloud, CloudCopy.Title, typeof(CloudAgentsPageView)),
        (SettingsSection.Privacy, PrivacyCopy.Title, typeof(PrivacyPageView)),
        (SettingsSection.Network, NetworkCopy.Title, typeof(NetworkPageView)),
        (SettingsSection.Keyboard, KeyboardCopy.Title, typeof(KeyboardPageView)),
    ];

    // The MCP and Cloud views draw the model their page holds; every other view draws the page.
    private static object ContextOf(ISettingsPage page) => page switch
    {
        McpSettingsPage mcp => mcp.Model,
        CloudSettingsPage cloud => cloud.Agents,
        _ => page,
    };

    /// <summary>The navigation lists the pages in model order under their titles, the first is selected, and choosing an entry shows that page's view.</summary>
    [Fact]
    public async Task ThePagesAreListedInModelOrderAndChoosingOneShowsItsView()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;

            window.NavigationList.Items.OfType<ListBoxItem>().Select(static item => AutomationProperties.GetName(item)).ShouldBe(Pages.Select(static page => page.Title));
            window.NavigationList.SelectedItem.ShouldBeSameAs(window.NavigationEntry(SettingsSection.Runtime));
            window.CurrentView.ShouldBeOfType<RuntimePageView>();
            AutomationProperties.GetName(window.NavigationList).ShouldBe(SettingsCopy.NavigationName);

            foreach (var (section, title, view) in Pages)
            {
                window.NavigationList.SelectedItem = window.NavigationEntry(section);
                fixture.Settle();

                fixture.Model.SelectedSection.ShouldBe(section, title);
                window.CurrentView.ShouldNotBeNull().GetType().ShouldBe(view, title);
                window.CurrentView.DataContext.ShouldBeSameAs(ContextOf(fixture.Model.SelectedPage));
            }
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Selecting a section through the model moves the navigation, so the list never disagrees with the page.</summary>
    [Fact]
    public async Task ASectionSelectedThroughTheModelMovesTheNavigation()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);

            fixture.Model.SelectSection(SettingsSection.Agents);
            fixture.Settle();

            fixture.Window.NavigationList.SelectedItem.ShouldBeSameAs(fixture.Window.NavigationEntry(SettingsSection.Agents));
            fixture.Window.CurrentView.ShouldBeOfType<AgentsPageView>();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A page view is kept when another page is shown, so what the user typed is still there on the way back.</summary>
    [Fact]
    public async Task APageViewIsKeptWhenAnotherPageIsShown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var runtime = (RuntimePageView)fixture.Show(SettingsSection.Runtime);
            runtime.IdleStop.Text = "36";

            _ = fixture.Show(SettingsSection.Tab);
            var back = fixture.Show(SettingsSection.Runtime);

            back.ShouldBeSameAs(runtime);
            runtime.IdleStop.Text.ShouldBe("36");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>An edit marks the page's navigation entry, in its accessible name as well as by the mark, and Discard takes the mark away.</summary>
    [Fact]
    public async Task AnEditMarksTheNavigationEntryInItsNameAndDiscardClearsIt()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var entry = fixture.Window.NavigationEntry(SettingsSection.Runtime).ShouldNotBeNull();
            AutomationProperties.GetName(entry).ShouldBe(SettingsCopy.RuntimeTitle);

            var view = (RuntimePageView)fixture.Show(SettingsSection.Runtime);
            view.IdleStop.Text = "48";
            fixture.Settle();

            AutomationProperties.GetName(entry).ShouldBe(SettingsCopy.NavigationEntryName(SettingsCopy.RuntimeTitle, isDirty: true));
            fixture.Window.Footer.Text.ShouldBe(SettingsCopy.UnsavedBadge);

            SettingsTree.Click(fixture.Window, fixture.Window.Discard);

            AutomationProperties.GetName(entry).ShouldBe(SettingsCopy.RuntimeTitle);
            fixture.Window.Footer.Text.ShouldBeNullOrEmpty();
            view.IdleStop.Text.ShouldBe("24");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Save is off while a page is clean or its entry is rejected, on for a valid edit, and saving announces the values the head persists.</summary>
    [Fact]
    public async Task SaveFollowsTheDirtyAndValidStateOfThePage()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;
            var view = (RuntimePageView)fixture.Show(SettingsSection.Runtime);

            window.Save.IsVisible.ShouldBeTrue();
            window.Save.IsEnabled.ShouldBeFalse("a clean page has nothing to save");
            window.Discard.IsEnabled.ShouldBeFalse();

            view.IdleStop.Text = "abc";
            fixture.Settle();
            window.Save.IsEnabled.ShouldBeFalse("a rejected entry can never be saved");
            view.IdleStopMessage.IsVisible.ShouldBeTrue();
            window.Discard.IsEnabled.ShouldBeTrue();

            view.IdleStop.Text = "48";
            fixture.Settle();
            window.Save.IsEnabled.ShouldBeTrue();
            view.IdleStopMessage.IsVisible.ShouldBeFalse();

            view.Retention.Text = "0";
            fixture.Settle();
            window.Save.IsEnabled.ShouldBeFalse("a retention outside the allowed range is rejected too");
            view.RetentionMessage.IsVisible.ShouldBeTrue();
            view.RetentionMessage.Text.ShouldBe(fixture.Runtime.Model.RetentionError);

            view.Retention.Text = "14";
            fixture.Settle();
            window.Save.IsEnabled.ShouldBeTrue();
            view.RetentionMessage.IsVisible.ShouldBeFalse();

            SettingsTree.Click(window, window.Save);

            fixture.RuntimeSaved.ShouldHaveSingleItem().ShouldBe(new RuntimeSettingsValues(48, 14));
            window.Save.IsEnabled.ShouldBeFalse();
            fixture.Model.HasUnsavedChanges.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Saving the General page writes the chosen motion through its source, and Reset returns the draft to the defaults without saving it.</summary>
    [Fact]
    public async Task SaveWritesThePageAndResetReturnsTheDraftToTheDefaults()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.GeneralSource.Values = new GeneralSettingsValues(MotionPreference.Full, ThemePreference.Dark)).ConfigureAwait(true);
            var window = fixture.Window;
            var view = (GeneralPageView)fixture.Show(SettingsSection.General);
            ((ListBoxItem)view.Motion.SelectedItem!).Tag.ShouldBeOfType<MotionOption>().Value.ShouldBe(MotionPreference.Full);

            window.Reset.IsVisible.ShouldBeTrue();
            SettingsTree.Click(window, window.Reset);

            fixture.General.Motion.ShouldBe(MotionPreference.System);
            fixture.General.IsDirty.ShouldBeTrue();
            fixture.GeneralSource.Saved.ShouldBeEmpty("Reset changes the draft only");
            ((ListBoxItem)view.Motion.SelectedItem!).Tag.ShouldBeOfType<MotionOption>().Value.ShouldBe(MotionPreference.System);

            SettingsTree.Click(window, window.Save);

            fixture.GeneralSource.Saved.ShouldHaveSingleItem().Motion.ShouldBe(MotionPreference.System);
            fixture.General.IsDirty.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A rejected save keeps the draft, shows the reason on the page and says so in the footer's live region.</summary>
    [Fact]
    public async Task ARejectedSaveShowsItsReasonOnThePageAndInTheFooter()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct, static f => f.AgentsSource.SaveFailure = "The disk is full.").ConfigureAwait(true);
            var window = fixture.Window;
            var view = (AgentsPageView)fixture.Show(SettingsSection.Agents);

            view.AutoRun.IsChecked = true;
            fixture.Settle();
            view.Warning.IsVisible.ShouldBeTrue("turning auto-run on shows its warning");
            SettingsTree.Click(window, window.Save);

            fixture.Agents.IsDirty.ShouldBeTrue();
            fixture.Agents.SaveError.ShouldBe("The disk is full.");
            SettingsTree.Texts(view).ShouldContain("The disk is full.", StringComparer.Ordinal);
            SettingsTree.Texts(view).ShouldContain(SettingsCopy.SaveFailedHeading, StringComparer.Ordinal);
            window.Footer.Text.ShouldBe(SettingsCopy.UnsavedBadge, "the page is still dirty");
            window.Save.IsEnabled.ShouldBeFalse("the rejected draft is not offered again until it changes");
        }, ct).ConfigureAwait(true);
    }

    /// <summary>A read-only page has no Save, Reset or Discard; the Models page has Save and Discard but no Reset to defaults.</summary>
    [Fact]
    public async Task ReadOnlyPagesShowNoSaveAndTheModelsPageHasNoReset()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;

            foreach (var section in new[] { SettingsSection.Tab, SettingsSection.Plugins, SettingsSection.Rules, SettingsSection.Skills, SettingsSection.Commands, SettingsSection.Network, SettingsSection.Keyboard })
            {
                _ = fixture.Show(section);
                window.Save.IsVisible.ShouldBeFalse(section.ToString());
                window.Reset.IsVisible.ShouldBeFalse(section.ToString());
                window.Discard.IsVisible.ShouldBeFalse(section.ToString());
                window.CloseWindow.IsVisible.ShouldBeTrue(section.ToString());
            }

            _ = fixture.Show(SettingsSection.Models);
            window.Save.IsVisible.ShouldBeTrue();
            window.Save.IsEnabled.ShouldBeFalse("no custom provider form is open");
            window.Discard.IsVisible.ShouldBeTrue();
            window.Reset.IsVisible.ShouldBeFalse();
        }, ct).ConfigureAwait(true);
    }

    /// <summary>Every footer button has the accessible name of its label and a target of at least the WCAG 2.2 minimum (24 by 24).</summary>
    [Fact]
    public async Task TheFooterButtonsAreNamedAndLargeEnough()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            using var fixture = await SettingsFixture.OpenAsync(ct).ConfigureAwait(true);
            var window = fixture.Window;

            AutomationProperties.GetName(window.Save).ShouldBe(SettingsCopy.SaveLabel);
            AutomationProperties.GetName(window.Reset).ShouldBe(SettingsCopy.ResetLabel);
            AutomationProperties.GetName(window.Discard).ShouldBe(SettingsCopy.DiscardLabel);
            AutomationProperties.GetName(window.CloseWindow).ShouldBe(SettingsViewCopy.CloseLabel);
            foreach (var button in new[] { window.Save, window.Reset, window.Discard, window.CloseWindow })
            {
                button.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            window.Save.Classes.ShouldContain("primary", StringComparer.Ordinal);
        }, ct).ConfigureAwait(true);
    }
}
