using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using OmpOS.Native.Tests.Workspace;
using OmpOS.Native.Views.Search;
using OmpOS.Workspace;
using Shouldly;
using Xunit;

namespace OmpOS.Native.Tests.Search;

/// <summary>
/// The workspace search box over a real workspace folder: text and regular-expression hits with their file, line and match,
/// an invalid regular expression shown as an error, and the keyboard path to the search and its results.
/// </summary>
public sealed class WorkspaceSearchViewTests
{
    /// <summary>A text search shows each hit's file and line, in file then line order.</summary>
    [Fact]
    public async Task EachTextHitShowsItsFileAndLine()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("notes/a.md", "alpha\nbeta\nalpha beta");
        workspace.Write("b.txt", "gamma alpha");
        var search = new WorkspaceSearch(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new WorkspaceSearchModel(search);
            var view = new WorkspaceSearchView { DataContext = model };
            var window = ViewHost.Show(view);

            view.PatternInput.Text = "alpha";
            Click(view.SearchButton);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);

            ShownHits(view).ShouldBe(
            [
                new ShownHit("b.txt", "1", "alpha"),
                new ShownHit("notes/a.md", "1", "alpha"),
                new ShownHit("notes/a.md", "3", "alpha"),
            ]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A regular-expression search shows the text each match covers, with its file and line.</summary>
    [Fact]
    public async Task ARegularExpressionShowsEachMatchWithItsFileAndLine()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("notes/a.md", "alpha beta\nbravo");
        var search = new WorkspaceSearch(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new WorkspaceSearchModel(search);
            var view = new WorkspaceSearchView { DataContext = model };
            var window = ViewHost.Show(view);

            view.PatternInput.Text = "b[a-z]+";
            view.RegexCheck.IsChecked = true;
            Click(view.SearchButton);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);

            ShownHits(view).ShouldBe(
            [
                new ShownHit("notes/a.md", "1", "beta"),
                new ShownHit("notes/a.md", "2", "bravo"),
            ]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>An invalid regular expression is shown as an error, and no hits are listed.</summary>
    [Fact]
    public async Task AnInvalidRegularExpressionShowsAnError()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("notes/a.md", "alpha");
        var search = new WorkspaceSearch(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new WorkspaceSearchModel(search);
            var view = new WorkspaceSearchView { DataContext = model };
            var window = ViewHost.Show(view);

            view.PatternInput.Text = "(unclosed";
            view.RegexCheck.IsChecked = true;
            Click(view.SearchButton);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);

            view.SearchError.IsVisible.ShouldBeTrue();
            view.SearchError.Text.ShouldNotBeNullOrWhiteSpace();
            view.HitRows.ItemCount.ShouldBe(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Ignore case finds the capitalised occurrence that a case-sensitive search does not.</summary>
    [Fact]
    public async Task IgnoreCaseFindsCapitalisedText()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("notes/a.md", "Alpha here");
        var search = new WorkspaceSearch(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new WorkspaceSearchModel(search);
            var view = new WorkspaceSearchView { DataContext = model };
            var window = ViewHost.Show(view);

            view.PatternInput.Text = "alpha";
            Click(view.SearchButton);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);
            ShownHits(view).ShouldBeEmpty();

            view.IgnoreCaseCheck.IsChecked = true;
            Click(view.SearchButton);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);

            ShownHits(view).ShouldBe([new ShownHit("notes/a.md", "1", "Alpha")]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter in the pattern box runs the search, and every search control is named and reachable by Tab.</summary>
    [Fact]
    public async Task EnterRunsTheSearchAndEveryControlIsNamedAndReachable()
    {
        using var workspace = new TempWorkspace();
        workspace.Write("b.txt", "gamma alpha");
        var search = new WorkspaceSearch(new WorkspaceRoot(workspace.Root));

        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new WorkspaceSearchModel(search);
            var view = new WorkspaceSearchView { DataContext = model };
            var window = ViewHost.Show(view);

            ViewHost.UnnamedTabStops(view).ShouldBeEmpty();

            view.PatternInput.Focus();
            view.PatternInput.Text = "gamma";
            ViewHost.Press(window, Key.Enter, PhysicalKey.Enter);
            await ViewHost.WaitForSearchAsync(model).ConfigureAwait(true);
            ShellHarness.Settle(window);

            ShownHits(view).ShouldBe([new ShownHit("b.txt", "1", "gamma")]);
            ViewHost.UnnamedTabStops(view).ShouldBeEmpty("the result rows are keyboard stops and must be named");

            view.PatternInput.Focus();
            ViewHost.Press(window, Key.Tab, PhysicalKey.Tab);
            view.RegexCheck.IsFocused.ShouldBeTrue("Tab moves from the pattern to the regular-expression option");
            ViewHost.Press(window, Key.Tab, PhysicalKey.Tab);
            view.IgnoreCaseCheck.IsFocused.ShouldBeTrue("Tab moves to the ignore-case option");
            ViewHost.Press(window, Key.Tab, PhysicalKey.Tab);
            view.SearchButton.IsFocused.ShouldBeTrue("Tab moves to the search button");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<ShownHit> ShownHits(WorkspaceSearchView view) =>
        view.HitRows.GetVisualDescendants()
            .OfType<ListBoxItem>()
            .Select(item => new ShownHit(
                TextOf(item, "path"),
                TextOf(item, "line"),
                TextOf(item, "match")))
            .ToList();

    private static string TextOf(ListBoxItem item, string className) =>
        item.GetVisualDescendants()
            .OfType<TextBlock>()
            .Single(text => text.Classes.Contains(className))
            .Text ?? string.Empty;

    /// <summary>One hit as the results list shows it: its file, its line number text and its match text.</summary>
    /// <param name="File">The file path shown.</param>
    /// <param name="Line">The line number shown.</param>
    /// <param name="Match">The match shown.</param>
    private sealed record ShownHit(string File, string Line, string Match);
}
