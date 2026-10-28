using Avalonia.Automation;
using Avalonia.Controls;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Settings;
using Autonogrammer.Native.Views.Workspace;
using Autonogrammer.Presentation.Settings.Workspaces;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Settings;

/// <summary>
/// The Workspaces page view over the real page model and a scripted source: the embedded folder editor without its footer, the recent sets and their
/// buttons, the live regions, the save error, keyboard order, accessible names and target sizes.
/// </summary>
public sealed class WorkspacesPageViewTests
{
    private static async Task<(WorkspacesPageView View, Window Window, WorkspacesSettingsPage Page, FakeWorkspacesSource Source)> OpenAsync(CancellationToken token)
    {
        var source = new FakeWorkspacesSource();
        source.Recents.Add(FakeWorkspacesSource.Row("Backend", "/work/api", "/work/lib"));
        source.Recents.Add(FakeWorkspacesSource.Row("Docs", "/work/docs"));
        var page = new WorkspacesSettingsPage(source);
        await page.LoadAsync(token).ConfigureAwait(true);
        var view = new WorkspacesPageView { DataContext = page };
        var window = ViewHost.Show(view);
        return (view, window, page, source);
    }

    private static string?[] StopNames(WorkspacesPageView view) =>
        [.. SettingsTree.TabStops(view).Select(static stop => AutomationProperties.GetName(stop))];

    /// <summary>The view embeds the folder editor of the page's picker with its Start and Done buttons hidden, and lists each recent set with its two buttons.</summary>
    [Fact]
    public async Task TheViewEmbedsTheEditorWithoutItsFooterAndListsTheRecents()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, _) = await OpenAsync(token).ConfigureAwait(true);
            var texts = SettingsTree.Texts(view);

            view.Editor.DataContext.ShouldBeSameAs(page.Picker);
            view.Editor.ShowFooter.ShouldBeFalse();
            view.Editor.Start.IsEffectivelyVisible.ShouldBeFalse();
            view.Editor.Done.IsEffectivelyVisible.ShouldBeFalse();
            view.Editor.AddFolder.IsEffectivelyVisible.ShouldBeTrue();
            texts.ShouldContain(WorkspacesCopy.Heading, StringComparer.Ordinal);
            texts.ShouldContain(WorkspacesCopy.RecentsHeading, StringComparer.Ordinal);
            texts.ShouldContain("Backend", StringComparer.Ordinal);
            texts.ShouldContain("/work/api", StringComparer.Ordinal);
            texts.ShouldContain("2 folders", StringComparer.Ordinal);
            texts.ShouldContain("Last used 2026-10-28 09:00 UTC.", StringComparer.Ordinal);
            view.Recents.ItemCount.ShouldBe(2);
            view.NoRecents.IsVisible.ShouldBeFalse();
            SettingsTree.Has<Button>(view, WorkspacesCopy.LoadName("Backend")).ShouldBeTrue();
            SettingsTree.Has<Button>(view, WorkspacesCopy.ForgetName("Backend")).ShouldBeTrue();
            SettingsTree.Has<Button>(view, WorkspacesCopy.LoadName("Docs")).ShouldBeTrue();
            SettingsTree.Has<Button>(view, WorkspacesCopy.ForgetName("Docs")).ShouldBeTrue();
            SettingsTree.Descriptions(view).ShouldContain("Backend, 2 folders, primary /work/api. Last used 2026-10-28 09:00 UTC.", StringComparer.Ordinal);
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>With no saved set the page says so and draws no row.</summary>
    [Fact]
    public async Task WithoutRecentsThePageSaysSo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, _, source) = await OpenAsync(token).ConfigureAwait(true);
            source.Recents.Clear();
            await ((WorkspacesSettingsPage)view.Page!).LoadAsync(token).ConfigureAwait(true);
            SettingsTree.SettleSafe(window);

            view.NoRecents.IsVisible.ShouldBeTrue();
            view.Recents.ItemCount.ShouldBe(0);
            SettingsTree.Texts(view).ShouldContain(WorkspacesCopy.NoRecents, StringComparer.Ordinal);
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Load into editor fills the embedded editor with the set's folders as an unsaved change and announces it in a polite live region.</summary>
    [Fact]
    public async Task LoadIntoEditorFillsTheEditorAndAnnouncesIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, _) = await OpenAsync(token).ConfigureAwait(true);
            view.Announcement.IsVisible.ShouldBeFalse();

            SettingsTree.Click(window, SettingsTree.Named<Button>(view, WorkspacesCopy.LoadName("Backend")));

            page.Picker.Name.ShouldBe("Backend");
            page.Picker.Rows.Count.ShouldBe(2);
            page.IsDirty.ShouldBeTrue();
            view.Announcement.IsVisible.ShouldBeTrue();
            view.Announcement.Text.ShouldBe(WorkspacesCopy.Loaded("Backend"));
            AutomationProperties.GetLiveSetting(view.Announcement).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(view.Announcement).ShouldBe(WorkspacesCopy.AnnouncementName);
            SettingsTree.Has<Button>(view, WorkspaceSetPickerCopy.RemoveName("r1")).ShouldBeTrue("the loaded folders have their own row buttons");
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Forget removes the row, announces it, and moves the focus to the Load button that took its place, or to Add folder when no set is left.</summary>
    [Fact]
    public async Task ForgetRemovesTheRowAndKeepsTheFocusInThePage()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, source) = await OpenAsync(token).ConfigureAwait(true);

            SettingsTree.Click(window, SettingsTree.Named<Button>(view, WorkspacesCopy.ForgetName("Backend")));
            SettingsTree.SettleSafe(window);

            source.Recents.Select(row => row.Name).ShouldBe(["Docs"]);
            view.Recents.ItemCount.ShouldBe(1);
            SettingsTree.Has<Button>(view, WorkspacesCopy.ForgetName("Backend")).ShouldBeFalse();
            view.Announcement.Text.ShouldBe(WorkspacesCopy.Forgot("Backend"));
            SettingsTree.FocusedName(window).ShouldBe(WorkspacesCopy.LoadName("Docs"));

            SettingsTree.Click(window, SettingsTree.Named<Button>(view, WorkspacesCopy.ForgetName("Docs")));
            SettingsTree.SettleSafe(window);

            page.HasNoRecents.ShouldBeTrue();
            view.NoRecents.IsVisible.ShouldBeTrue();
            SettingsTree.FocusedName(window).ShouldBe(AutomationProperties.GetName(view.Editor.AddFolder));
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Make primary and Remove on the embedded editor's rows change the page's set and make it dirty; each button is named after its folder.</summary>
    [Fact]
    public async Task TheEmbeddedEditorsRowButtonsChangeTheSet()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, _) = await OpenAsync(token).ConfigureAwait(true);
            page.Picker.AddFolder("/work/api");
            page.Picker.AddFolder("/work/web");
            page.Picker.AddFolder("/work/lib");
            SettingsTree.SettleSafe(window);

            SettingsTree.Click(window, SettingsTree.Named<Button>(view, WorkspaceSetPickerCopy.MakePrimaryName("web")));

            page.Picker.Rows.Select(row => row.Id).ShouldBe(["web", "api", "lib"]);
            page.Picker.Rows[0].IsPrimary.ShouldBeTrue();
            page.IsDirty.ShouldBeTrue();

            SettingsTree.Click(window, SettingsTree.Named<Button>(view, WorkspaceSetPickerCopy.RemoveName("lib")));

            page.Picker.Rows.Select(row => row.Id).ShouldBe(["web", "api"]);
            SettingsTree.Has<Button>(view, WorkspaceSetPickerCopy.RemoveName("lib")).ShouldBeFalse();
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>A refused save shows an assertive heading and the reason as text, and the editor row names its own finding; nothing relies on colour alone.</summary>
    [Fact]
    public async Task ARefusedSaveIsShownAsTextInAnAssertiveRegion()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, _) = await OpenAsync(token).ConfigureAwait(true);
            view.SaveFailed.IsVisible.ShouldBeFalse();
            page.Picker.AddFolder("/work/missing-folder");

            (await page.SaveAsync(token).ConfigureAwait(true)).ShouldBeFalse();
            SettingsTree.SettleSafe(window);

            view.SaveFailed.IsVisible.ShouldBeTrue();
            view.SaveFailed.Text.ShouldBe(WorkspacesCopy.SaveFailedHeading);
            AutomationProperties.GetLiveSetting(view.SaveFailed).ShouldBe(AutomationLiveSetting.Assertive);
            view.SaveFailedDetail.IsVisible.ShouldBeTrue();
            view.SaveFailedDetail.Text.ShouldNotBeNull().ShouldContain("does not exist", Case.Sensitive);
            SettingsTree.Visible<TextBlock>(view.Editor).ShouldContain(
                block => (block.Text ?? string.Empty).Contains("does not exist", StringComparison.Ordinal),
                "the embedded editor words the finding itself");
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Tab walks the editor first and then the recent sets, Load into editor before Forget in each row, in the order of the markup.</summary>
    [Fact]
    public async Task KeyboardOrderIsTheEditorThenTheRecents()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, _, _) = await OpenAsync(token).ConfigureAwait(true);

            var stops = StopNames(view);

            stops.TakeLast(4).ShouldBe(
            [
                WorkspacesCopy.LoadName("Backend"),
                WorkspacesCopy.ForgetName("Backend"),
                WorkspacesCopy.LoadName("Docs"),
                WorkspacesCopy.ForgetName("Docs"),
            ]);
            stops.ShouldContain(AutomationProperties.GetName(view.Editor.AddFolder), StringComparer.Ordinal);
            Array.IndexOf(stops, AutomationProperties.GetName(view.Editor.AddFolder)).ShouldBeLessThan(Array.IndexOf(stops, WorkspacesCopy.LoadName("Backend")));
            window.Close();
        }, token).ConfigureAwait(true);
    }

    /// <summary>Every control that takes focus or announces a change has an accessible name, with a folder in the editor and recents listed, and every pointer target meets the minimum size.</summary>
    [Fact]
    public async Task EveryControlIsNamedAndEveryTargetIsBigEnough()
    {
        var token = TestContext.Current.CancellationToken;
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var (view, window, page, _) = await OpenAsync(token).ConfigureAwait(true);
            page.Picker.AddFolder("/work/api");
            page.Picker.AddFolder("/work/web");
            page.Picker.AddFolder("/work/missing-folder");
            SettingsTree.SettleSafe(window);

            SettingsTree.Unnamed(view).ShouldBeEmpty();
            foreach (var control in SettingsTree.Visible<Button>(view).Where(static button => button.TemplatedParent is null))
            {
                control.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(control));
                control.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(control));
            }

            foreach (var box in SettingsTree.Visible<TextBox>(view).Where(static box => box.TemplatedParent is null))
            {
                box.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget, AutomationProperties.GetName(box));
            }

            window.Close();
        }, token).ConfigureAwait(true);
    }
}
