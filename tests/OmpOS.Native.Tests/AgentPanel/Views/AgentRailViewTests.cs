using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Views.AgentPanel;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>The Agents rail: grouped rows with status chips, the filter, selection, the states and the keyboard (46-UI-SPEC, Agent Row).</summary>
public sealed class AgentRailViewTests
{
    private static SessionCatalogOptions Options() => new()
    {
        LiveStatus = id => id.StartsWith("wait", StringComparison.Ordinal) ? RuntimeStatus.NeedsAttention : null,
    };

    private static void Add(FakeSessionCatalog fake, ManualTimeProvider clock, string id, SessionLifecycle lifecycle, string title, TimeSpan? age = null, TimeSpan? quiet = null, string? taskId = null)
    {
        var now = clock.GetUtcNow();
        fake.Sessions.Add(PanelFixtures.Summary(id, lifecycle, now - (age ?? TimeSpan.FromMinutes(5)), now - (quiet ?? TimeSpan.FromSeconds(2)), title, taskId));
    }

    private static (AgentRailView Rail, SessionCatalogModel Catalog, FakeSessionCatalog Fake, ManualTimeProvider Clock, Window Window) Open(
        Action<FakeSessionCatalog, ManualTimeProvider>? populate = null,
        bool reducedMotion = false)
    {
        var clock = new ManualTimeProvider();
        var fake = new FakeSessionCatalog();
        if (populate is null)
        {
            Add(fake, clock, "wait-1", SessionLifecycle.Created, "Review the plan");
            Add(fake, clock, "run-1", SessionLifecycle.Running, "Build the rail", taskId: "task-7");
            Add(fake, clock, "lost-1", SessionLifecycle.Lost, "Lost one");
            Add(fake, clock, "idle-1", SessionLifecycle.Created, "Idle one");
        }
        else
        {
            populate(fake, clock);
        }

        var catalog = PanelFixtures.Catalog(fake, clock, Options());
        var rail = new AgentRailView { DataContext = catalog };
        var window = ViewHarness.Show(rail, reducedMotion);
        return (rail, catalog, fake, clock, window);
    }

    private static List<ListBoxItem> Items(Control rail) => [.. rail.GetVisualDescendants().OfType<ListBoxItem>()];

    /// <summary>Every row draws its title, status words, last activity and task tag, and the group header only on the first row of a group.</summary>
    [Fact]
    public async Task RowsDrawTheirCopyAndGroupHeadersOnGroupStartsOnly()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();
            var items = Items(rail);

            items.Count.ShouldBe(catalog.Rows.Count);
            for (var index = 0; index < catalog.Rows.Count; index++)
            {
                var row = catalog.Rows[index];
                var texts = ViewHarness.VisibleTexts(items[index]);
                texts.ShouldContain(row.Title, StringComparer.Ordinal);
                texts.ShouldContain(row.StatusText, StringComparer.Ordinal);
                texts.ShouldContain(row.LastActivityText, StringComparer.Ordinal);
                if (row.TaskTag is not null)
                {
                    texts.ShouldContain(row.TaskTag, StringComparer.Ordinal);
                }

                var expectedHeaders = (row.IsGroupStart ? 1 : 0) + (string.Equals(row.StatusText, row.GroupHeader, StringComparison.Ordinal) ? 1 : 0);
                texts.Count(text => string.Equals(text, row.GroupHeader, StringComparison.Ordinal)).ShouldBe(expectedHeaders, $"the {row.GroupHeader} header is drawn on its first row only");
            }

            var drawnHeaders = catalog.Rows.Where(static row => row.IsGroupStart).Select(static row => row.GroupHeader).ToList();
            drawnHeaders.Count.ShouldBe(drawnHeaders.Distinct(StringComparer.Ordinal).Count());
            drawnHeaders.ShouldContain("Needs you", StringComparer.Ordinal);
            drawnHeaders.ShouldContain("Running", StringComparer.Ordinal);
            drawnHeaders.ShouldContain("Lost", StringComparer.Ordinal);
            drawnHeaders.ShouldContain("Idle", StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A row is named by its full accessible name, never by the control type.</summary>
    [Fact]
    public async Task EachRowIsNamedByItsFullAccessibleName()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();

            Items(rail).Select(static item => AutomationProperties.GetName(item)).ShouldBe(catalog.Rows.Select(static row => row.AccessibleName));
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The group header band and the glyphs are raw to assistive technology.</summary>
    [Fact]
    public async Task GroupHeadersAndGlyphsAreRaw()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, _, _, _, window) = Open();

            var header = rail.GetVisualDescendants().OfType<TextBlock>().First(static block => string.Equals(block.Text, "Needs you", StringComparison.Ordinal));
            AutomationProperties.GetAccessibilityView(header).ShouldBe(AccessibilityView.Raw);
            rail.GetVisualDescendants().OfType<StageIcon>().ShouldAllBe(static icon => AutomationProperties.GetAccessibilityView(icon) == AccessibilityView.Raw);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The long-running flag and the quiet text are words, not colour.</summary>
    [Fact]
    public async Task LongRunningAndQuietFlagsAreWords()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open((fake, clock) =>
            {
                Add(fake, clock, "long-1", SessionLifecycle.Running, "Slow one", age: TimeSpan.FromHours(3), quiet: TimeSpan.FromMinutes(3));
            });

            var row = catalog.Rows.Single();
            row.IsLongRunning.ShouldBeTrue();
            row.IsStale.ShouldBeTrue();
            var texts = ViewHarness.VisibleTexts(Items(rail).Single());
            texts.ShouldContain("Long-running", StringComparer.Ordinal);
            texts.ShouldContain(row.QuietText!, StringComparer.Ordinal);
            texts.ShouldNotContain(row.LastActivityText, StringComparer.Ordinal);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Selecting a session in the catalog selects its row, and choosing a row selects the session.</summary>
    [Fact]
    public async Task SelectionFollowsTheCatalogAndDrivesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();
            var list = ViewHarness.Find<ListBox>(rail, "AgentRows");

            catalog.Select("run-1");
            ViewHarness.Settle(window);
            (list.SelectedItem as AgentRow)?.Id.ShouldBe("run-1");

            list.SelectedItem = catalog.Rows.Single(static row => string.Equals(row.Id, "lost-1", StringComparison.Ordinal));
            ViewHarness.Settle(window);
            catalog.SelectedId.ShouldBe("lost-1");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The filter box narrows the rows, Esc clears it, and a filter that hides everything says so.</summary>
    [Fact]
    public async Task TheFilterNarrowsTheRowsAndEscClearsIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();
            var filter = ViewHarness.Find<TextBox>(rail, "AgentFilter");
            var total = catalog.Rows.Count;

            filter.Text = "build";
            ViewHarness.Settle(window);
            catalog.Filter.ShouldBe("build");
            Items(rail).Count.ShouldBe(1);

            filter.Text = "zzz";
            ViewHarness.Settle(window);
            Items(rail).Count.ShouldBe(0);
            ViewHarness.Find<TextBlock>(rail, "StateHeading").Text.ShouldBe(AgentPanelCopy.FilterNoMatchHeading("zzz"));
            ViewHarness.Find<TextBlock>(rail, "StateBody").Text.ShouldBe(AgentPanelCopy.FilterNoMatchBody(total));

            filter.Focus();
            ViewHarness.Press(window, Key.Escape, PhysicalKey.Escape);
            filter.Text.ShouldBeNullOrEmpty();
            catalog.Filter.ShouldBe(string.Empty);
            Items(rail).Count.ShouldBe(total);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>With no sessions the rail says so; with a Loading or Error panel it draws that copy instead.</summary>
    [Fact]
    public async Task EmptyLoadingAndErrorStatesDrawTheirCopy()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var clock = new ManualTimeProvider();
            var fake = new FakeSessionCatalog();
            using var catalog = PanelFixtures.Catalog(fake, clock);
            var host = new ScriptedPanelHost(fake, clock);
            using var panel = new AgentPanelViewModel(catalog, host);
            var rail = new AgentRailView { DataContext = catalog, Panel = panel };
            var window = ViewHarness.Show(rail);
            var heading = ViewHarness.Find<TextBlock>(rail, "StateHeading");
            var body = ViewHarness.Find<TextBlock>(rail, "StateBody");

            heading.Text.ShouldBe(AgentPanelCopy.EmptyRailHeading);
            body.Text.ShouldBe(AgentPanelCopy.EmptyRailBody);

            var scope = panel.BeginInitialising();
            ViewHarness.Settle(window);
            heading.Text.ShouldBe(AgentPanelCopy.RailLoading);
            body.IsVisible.ShouldBeFalse();
            scope.Dispose();

            panel.ReportLoadFailure("The registry file is unreadable");
            ViewHarness.Settle(window);
            heading.Text.ShouldBe(AgentPanelCopy.RailErrorHeading);
            body.Text.ShouldBe(AgentPanelCopy.RailErrorBody);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The New agent button runs the panel's command and then tells the owner, so focus can move to the new prompt.</summary>
    [Fact]
    public async Task NewAgentRunsTheCommandAndReportsCompletion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var clock = new ManualTimeProvider();
            var fake = new FakeSessionCatalog();
            using var catalog = PanelFixtures.Catalog(fake, clock);
            var host = new ScriptedPanelHost(fake, clock);
            using var panel = new AgentPanelViewModel(catalog, host);
            var rail = new AgentRailView { DataContext = catalog, Panel = panel };
            var window = ViewHarness.Show(rail);
            var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            rail.NewAgentCompleted += (_, _) => completed.TrySetResult();

            ViewHarness.Click(window, ViewHarness.Find<Button>(rail, "NewAgentButton"));
            await completed.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken).ConfigureAwait(true);

            host.Created.Count.ShouldBe(1);
            catalog.SelectedId.ShouldBe(host.Created[0]);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Delete raises a close request for the selected row and Enter reports the row as activated; neither closes anything itself.</summary>
    [Fact]
    public async Task DeleteAsksToCloseAndEnterActivates()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();
            var list = ViewHarness.Find<ListBox>(rail, "AgentRows");
            var closed = new List<string>();
            var activated = 0;
            rail.CloseRequested += (_, args) => closed.Add(args.SessionId);
            rail.RowActivated += (_, _) => activated++;
            catalog.Select("run-1");
            ViewHarness.Settle(window);
            list.Focus();

            ViewHarness.Press(window, Key.Delete, PhysicalKey.Delete);
            ViewHarness.Press(window, Key.Enter, PhysicalKey.Enter);

            closed.ShouldBe(["run-1"]);
            activated.ShouldBe(1);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The row menu offers Resume only for a lost or closed agent, Reopen only for a closed one, and Close otherwise.</summary>
    [Fact]
    public async Task TheRowMenuOffersOnlyWhatFitsTheRow()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, catalog, _, _, window) = Open();
            var list = ViewHarness.Find<ListBox>(rail, "AgentRows");
            var menu = list.ContextMenu!;
            var resume = rail.FindControl<MenuItem>("ResumeItem")!;
            var reopen = rail.FindControl<MenuItem>("ReopenItem")!;
            var close = rail.FindControl<MenuItem>("CloseItem")!;

            AutomationProperties.GetName(menu).ShouldBe("Agent actions");

            catalog.Select("lost-1");
            ViewHarness.Settle(window);
            menu.Open(list);
            resume.IsVisible.ShouldBeTrue();
            reopen.IsVisible.ShouldBeFalse();
            close.IsVisible.ShouldBeTrue();
            menu.Close();
            ViewHarness.Settle(window);

            catalog.Select("run-1");
            ViewHarness.Settle(window);
            menu.Open(list);
            resume.IsVisible.ShouldBeFalse();
            reopen.IsVisible.ShouldBeFalse();
            close.IsVisible.ShouldBeTrue();
            menu.Close();
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>The rail has no unnamed tab stop, and its first stops are New agent, the filter and the list, in that order.</summary>
    [Fact]
    public async Task TheTabOrderIsNewAgentFilterThenTheList()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, _, _, _, window) = Open();

            ViewHarness.Unnamed(rail).ShouldBeEmpty();
            ViewHarness.TabOrder(rail).Take(3).ShouldBe(["New agent", "Filter agents", "Agent sessions"]);
            var newAgent = ViewHarness.Find<Button>(rail, "NewAgentButton");
            newAgent.Classes.Contains("primary").ShouldBeTrue();
            newAgent.MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            ViewHarness.Find<TextBox>(rail, "AgentFilter").MinHeight.ShouldBeGreaterThanOrEqualTo(44);
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A Running row pulses its glyph under full motion and holds it still under reduced motion.</summary>
    [Fact]
    public async Task TheRunningRowGlyphHonoursReducedMotion()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (full, _, _, _, fullWindow) = Open(reducedMotion: false);
            full.GetVisualDescendants().OfType<StageIcon>().Any(static icon => icon.IsPulsing).ShouldBeTrue();
            fullWindow.Close();

            var (reduced, _, _, _, reducedWindow) = Open(reducedMotion: true);
            reduced.GetVisualDescendants().OfType<StageIcon>().Any(static icon => icon.IsPulsing).ShouldBeFalse();
            reducedWindow.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A data context swap redraws the rows; a detached rail stops following its model; a null model draws nothing and does not throw.</summary>
    [Fact]
    public async Task ADataContextSwapRedrawsAndDetachingStopsListening()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var (rail, first, _, clock, window) = Open();
            var secondFake = new FakeSessionCatalog();
            Add(secondFake, clock, "only-1", SessionLifecycle.Created, "The only one");
            using var second = PanelFixtures.Catalog(secondFake, clock);
            var list = ViewHarness.Find<ListBox>(rail, "AgentRows");

            rail.DataContext = second;
            ViewHarness.Settle(window);
            Items(rail).Count.ShouldBe(1);

            second.Select("only-1");
            ViewHarness.Settle(window);
            first.Select("run-1");
            ViewHarness.Settle(window);
            (list.SelectedItem as AgentRow)?.Id.ShouldBe("only-1", "the old model no longer drives the rail");

            rail.DataContext = null;
            ViewHarness.Settle(window);
            Items(rail).Count.ShouldBe(0);

            rail.DataContext = second;
            ViewHarness.Settle(window);
            var drawn = list.ItemsSource;
            window.Content = null;
            secondFake.Put(PanelFixtures.Summary("later-1", SessionLifecycle.Created, clock.GetUtcNow(), clock.GetUtcNow(), "Later"));
            ReferenceEquals(list.ItemsSource, drawn).ShouldBeTrue("a detached rail does not redraw");
            window.Close();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
