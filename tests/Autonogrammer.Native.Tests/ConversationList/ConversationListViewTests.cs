using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Autonogrammer.Native.Views;
using Autonogrammer.Presentation.Components;
using Autonogrammer.Presentation.State;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.ConversationList;

/// <summary>
/// Headless checks of the session list bound to its <see cref="ConversationListModel"/> (UI-01): a change made while the
/// list is shown is drawn without reopening it, and a list that has left the window keeps what it drew.
/// </summary>
public sealed class ConversationListViewTests
{
    private static readonly ConversationSummary First = new() { Id = "s-1", Title = "First conversation" };
    private static readonly ConversationSummary Second = new() { Id = "s-2", Title = "Second conversation" };
    private static readonly ConversationSummary Third = new() { Id = "s-3", Title = "Third conversation" };

    /// <summary>A model change made after the list is shown appears in the list, with no reopening.</summary>
    [Fact]
    public async Task ChangeToTheModelWhileShownIsDrawn()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ConversationListModel();
            model.Load([First]);
            var (window, _, list) = ShowBound(model);
            RowNames(list).ShouldBe([First.Title]);

            model.Load([First, Second]);
            ShellHarness.Settle(window);

            RowNames(list).ShouldBe([First.Title, Second.Title]);
            VisibleRow(list, Second.Title).Bounds.Height.ShouldBeGreaterThan(0);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A load that finds no conversations, made while the list is shown, draws the empty prompt.</summary>
    [Fact]
    public async Task EmptyLoadWhileShownDrawsTheEmptyPrompt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ConversationListModel();
            model.Load([First]);
            var (window, _, list) = ShowBound(model);

            model.Load([]);
            ShellHarness.Settle(window);

            RowNames(list).ShouldBeEmpty();
            list.FindControl<TextBlock>("StateHeading")!.Text.ShouldBe("No conversations yet.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A failed load made while the list is shown draws the failure and clears the rows.</summary>
    [Fact]
    public async Task FailureWhileShownDrawsTheFailure()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ConversationListModel();
            model.Load([First]);
            var (window, _, list) = ShowBound(model);

            model.Fail();
            ShellHarness.Settle(window);

            RowNames(list).ShouldBeEmpty();
            list.FindControl<TextBlock>("StateHeading")!.Text.ShouldBe("Could not load conversations.");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>After the list leaves the window, changes to its model are not drawn, and nothing throws.</summary>
    [Fact]
    public async Task ChangeAfterDetachIsNotDrawn()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ConversationListModel();
            model.Load([First]);
            var (window, host, list) = ShowBound(model);

            host.Child = null;
            list.GetVisualAncestors().ShouldBeEmpty();
            model.Load([Second, Third]);
            model.Fail();
            ShellHarness.Settle(window);

            RowNames(list).ShouldBe([First.Title]);
            list.FindControl<TextBlock>("StateHeading")!.Text.ShouldBeEmpty();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A list shown again draws the model as it is then, including the changes made while it was away.</summary>
    [Fact]
    public async Task ReattachingDrawsTheModelsCurrentConversations()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            var model = new ConversationListModel();
            model.Load([First]);
            var (window, host, list) = ShowBound(model);
            host.Child = null;
            model.Load([Second, Third]);

            host.Child = list;
            ShellHarness.Settle(window);

            RowNames(list).ShouldBe([Second.Title, Third.Title]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A change made on a background thread is drawn on the UI thread once the dispatcher runs.</summary>
    [Fact]
    public async Task ChangeOnABackgroundThreadIsDrawn()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(async () =>
        {
            var model = new ConversationListModel();
            var (window, _, list) = ShowBound(model);

            await Task.Run(() => model.Load([First, Second])).ConfigureAwait(true);
            ShellHarness.Settle(window);

            RowNames(list).ShouldBe([First.Title, Second.Title]);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Shows a list bound to the model inside a host that can detach it, and settles the window.</summary>
    private static (Window Window, Border Host, ConversationListView List) ShowBound(ConversationListModel model)
    {
        var list = new ConversationListView { DataContext = model };
        var host = new Border { Child = list };
        var window = new Window { Content = host };
        window.Show();
        ShellHarness.Settle(window);
        return (window, host, list);
    }

    /// <summary>Gets the accessible name of each row the list draws, in order.</summary>
    private static List<string> RowNames(ConversationListView list) =>
        Rows(list).Select(row => AutomationProperties.GetName(row) ?? string.Empty).ToList();

    /// <summary>Gets the drawn row whose accessible name is the given title.</summary>
    private static ListBoxItem VisibleRow(ConversationListView list, string title) =>
        Rows(list).Single(row => string.Equals(AutomationProperties.GetName(row), title, StringComparison.Ordinal));

    private static IEnumerable<ListBoxItem> Rows(ConversationListView list) =>
        list.FindControl<ListBox>("ConversationRows")!.GetVisualDescendants().OfType<ListBoxItem>();
}
