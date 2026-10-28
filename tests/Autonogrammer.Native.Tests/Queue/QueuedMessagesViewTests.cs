using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Autonogrammer.Native.Tests.Settings;
using Autonogrammer.Native.Tests.Workspace;
using Autonogrammer.Native.Views.Queue;
using Autonogrammer.Presentation.Queue;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Queue;

/// <summary>
/// The queued messages under the composer on the headless desktop: the view hides itself while the queue is empty, each row names its message and buttons by
/// position, every button reaches the model, a focused row reorders and deletes from the keyboard, and the announcement is a polite live region.
/// </summary>
public sealed class QueuedMessagesViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static QueuedMessagesModel ModelWith(params string[] texts)
    {
        var model = new QueuedMessagesModel("session-1", TimeProvider.System);
        foreach (var text in texts)
        {
            _ = model.Enqueue(text);
        }

        return model;
    }

    private static Button Named(Control view, string name) => SettingsTree.Named<Button>(view, name);

    private static Border RowOf(Control view, int position, string preview) =>
        SettingsTree.Visible<Border>(view).First(border => string.Equals(AutomationProperties.GetName(border), QueueCopy.RowName(position, preview), StringComparison.Ordinal));

    private static List<string> Order(QueuedMessagesModel model) => [.. model.Messages.Select(static message => message.Text)];

    /// <summary>The view is hidden while the queue is empty and shows the pill with the count as soon as a message is queued; emptying the queue hides it again.</summary>
    [Fact]
    public async Task TheViewHidesWhileTheQueueIsEmptyAndShowsThePillWithTheCount()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = ModelWith();
            var view = new QueuedMessagesView { DataContext = model };
            var window = ViewHost.Show(view);
            view.IsVisible.ShouldBeFalse();

            _ = model.Enqueue("first");
            _ = model.Enqueue("second");
            ShellHarness.Settle(window);
            view.IsVisible.ShouldBeTrue();
            view.Pill.Text.ShouldBe(QueueCopy.Count(2));

            _ = model.Clear();
            ShellHarness.Settle(window);
            view.IsVisible.ShouldBeFalse();
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>
    /// Each row shows its message and names the row and its four buttons with the message's position; the first row cannot move up, the last cannot move down,
    /// every control is named and at least the minimum target, and the announcement is a polite live region.
    /// </summary>
    [Fact]
    public async Task EachRowIsNamedByPositionAndTheEdgeRowsCannotMoveOutwards()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = ModelWith("first", "second", "third");
            var view = new QueuedMessagesView { DataContext = model };
            var window = ViewHost.Show(view);

            var shown = SettingsTree.Texts(view);
            shown.ShouldContain("first", StringComparer.Ordinal);
            shown.ShouldContain("second", StringComparer.Ordinal);
            shown.ShouldContain("third", StringComparer.Ordinal);
            RowOf(view, 2, "second").Focusable.ShouldBeTrue();

            for (var position = 1; position <= 3; position++)
            {
                SettingsTree.Has<Button>(view, QueueCopy.MoveUpName(position)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, QueueCopy.MoveDownName(position)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, QueueCopy.SendNowName(position)).ShouldBeTrue();
                SettingsTree.Has<Button>(view, QueueCopy.RemoveName(position)).ShouldBeTrue();
            }

            Named(view, QueueCopy.MoveUpName(1)).IsEnabled.ShouldBeFalse();
            Named(view, QueueCopy.MoveDownName(1)).IsEnabled.ShouldBeTrue();
            Named(view, QueueCopy.MoveUpName(2)).IsEnabled.ShouldBeTrue();
            Named(view, QueueCopy.MoveDownName(2)).IsEnabled.ShouldBeTrue();
            Named(view, QueueCopy.MoveUpName(3)).IsEnabled.ShouldBeTrue();
            Named(view, QueueCopy.MoveDownName(3)).IsEnabled.ShouldBeFalse();

            SettingsTree.Unnamed(view).ShouldBeEmpty();
            foreach (var button in SettingsTree.Visible<Button>(view).Where(static button => button.TemplatedParent is null))
            {
                button.Bounds.Width.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
                button.Bounds.Height.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            }

            AutomationProperties.GetLiveSetting(view.Announcement).ShouldBe(AutomationLiveSetting.Polite);
            AutomationProperties.GetName(view.Rows).ShouldBe(QueueCopy.Title);
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Move down, Move up, Send now, Remove and Clear all each reach the model with the id of their row, and the live region says what happened.</summary>
    [Fact]
    public async Task EveryButtonReachesTheModelWithTheIdOfItsRow()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = ModelWith("first", "second", "third");
            var sent = new List<string>();
            model.Sender = (text, _) =>
            {
                sent.Add(text);
                return Task.FromResult(QueueSendOutcome.Sent);
            };
            var view = new QueuedMessagesView { DataContext = model };
            var window = ViewHost.Show(view);

            SettingsTree.Click(window, Named(view, QueueCopy.MoveDownName(1)));
            Order(model).ShouldBe(["second", "first", "third"]);
            view.Announcement.Text.ShouldBe(QueueCopy.Moved(1, 2, 3));

            SettingsTree.Click(window, Named(view, QueueCopy.MoveUpName(3)));
            Order(model).ShouldBe(["second", "third", "first"]);

            SettingsTree.Click(window, Named(view, QueueCopy.SendNowName(2)));
            sent.ShouldBe(["third"]);
            Order(model).ShouldBe(["second", "first"]);
            view.Announcement.Text.ShouldBe(QueueCopy.SentNow(2));

            SettingsTree.Click(window, Named(view, QueueCopy.RemoveName(1)));
            Order(model).ShouldBe(["first"]);
            view.Pill.Text.ShouldBe(QueueCopy.Count(1));

            SettingsTree.Click(window, view.ClearAll);
            model.IsEmpty.ShouldBeTrue();
            view.IsVisible.ShouldBeFalse();
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>A focused row reorders with Alt+Down and Alt+Up and keeps keyboard focus on the message; Delete removes it and focus moves to the row that took its place.</summary>
    [Fact]
    public async Task AltArrowsReorderAFocusedRowAndDeleteRemovesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = ModelWith("first", "second", "third");
            var view = new QueuedMessagesView { DataContext = model };
            var window = ViewHost.Show(view);

            RowOf(view, 1, "first").Focus().ShouldBeTrue();
            SettingsTree.Press(window, Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Order(model).ShouldBe(["second", "first", "third"]);
            SettingsTree.FocusedName(window).ShouldBe(QueueCopy.RowName(2, "first"));

            SettingsTree.Press(window, Key.Up, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            Order(model).ShouldBe(["first", "second", "third"]);
            SettingsTree.FocusedName(window).ShouldBe(QueueCopy.RowName(1, "first"));

            SettingsTree.Press(window, Key.Delete, PhysicalKey.Delete);
            Order(model).ShouldBe(["second", "third"]);
            SettingsTree.FocusedName(window).ShouldBe(QueueCopy.RowName(1, "second"));
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }

    /// <summary>Alt+Up on the first row and Alt+Down on the last row change nothing, and a plain arrow key never reorders.</summary>
    [Fact]
    public async Task AltArrowsAtTheEdgesAndPlainArrowsLeaveTheOrderAlone()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.RunAsync(() =>
        {
            var model = ModelWith("first", "second");
            var view = new QueuedMessagesView { DataContext = model };
            var window = ViewHost.Show(view);

            RowOf(view, 1, "first").Focus().ShouldBeTrue();
            SettingsTree.Press(window, Key.Up, PhysicalKey.ArrowUp, RawInputModifiers.Alt);
            SettingsTree.Press(window, Key.Down, PhysicalKey.ArrowDown);
            Order(model).ShouldBe(["first", "second"]);

            RowOf(view, 2, "second").Focus().ShouldBeTrue();
            SettingsTree.Press(window, Key.Down, PhysicalKey.ArrowDown, RawInputModifiers.Alt);
            Order(model).ShouldBe(["first", "second"]);
            window.Close();
            return Task.CompletedTask;
        }, Token).ConfigureAwait(true);
    }
}
