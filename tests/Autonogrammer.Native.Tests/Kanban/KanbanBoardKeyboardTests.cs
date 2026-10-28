using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Tests.AgentPanel.Views;
using Autonogrammer.Native.Views.Kanban;
using Shouldly;
using Xunit;

namespace Autonogrammer.Native.Tests.Kanban;

/// <summary>Keyboard use of the Kanban tab: one roving tab stop, arrow keys between cards and columns, Enter, S, Esc and a visible focus ring.</summary>
public sealed class KanbanBoardKeyboardTests
{
    private static void PopulateTwoColumns(FakeSessionCatalog fake, ManualTimeProvider clock)
    {
        fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "First running", minutesAgo: 1));
        fake.Sessions.Add(KanbanFixture.Session(clock, "run-2", SessionLifecycle.Running, "Second running", minutesAgo: 2));
        fake.Sessions.Add(KanbanFixture.Session(clock, "done-1", SessionLifecycle.Completed, "Finished work", minutesAgo: 30));
        fake.Sessions.Add(KanbanFixture.Session(clock, "new-1", SessionLifecycle.Created, "Queued work", minutesAgo: 3));
    }

    private static string? FocusedCardId(KanbanFixture fixture)
    {
        var focused = TopLevel.GetTopLevel(fixture.View)!.FocusManager!.GetFocusedElement();
        return (focused as KanbanCardView)?.Model?.Id;
    }

    /// <summary>The board has one tab stop for the cards, the current card, followed by its Steer button; every stop is named.</summary>
    [Fact]
    public async Task TheBoardHasOneCardStopAndItsSteerButton()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns);
            var current = fixture.View.CurrentCard.ShouldNotBeNull();

            var order = ViewHarness.TabOrder(fixture.View);

            order.ShouldBe([AutomationProperties.GetName(current).ShouldNotBeNull(), $"Steer {current.Model!.Card.Title}"]);
            ViewHarness.TabStops(fixture.View).OfType<KanbanCardView>().Count().ShouldBe(1);
            ViewHarness.Unnamed(fixture.View).ShouldBeEmpty();
            fixture.View.CardViews.Count(static card => card.IsTabStop).ShouldBe(1);
            current.SteerTrigger.MinHeight.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            current.SteerTrigger.MinWidth.ShouldBeGreaterThanOrEqualTo(TargetSize.MinimumTarget);
            AutomationProperties.GetName(current.SteerTrigger).ShouldBe($"Steer {current.Model.Card.Title}");
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Down and Up move inside a column, Right and Left move to the neighbouring column that has cards, and the edges stop.</summary>
    [Fact]
    public async Task ArrowKeysMoveBetweenCardsAndColumns()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns);
            var first = fixture.View.CardViewOf("new-1").ShouldNotBeNull();
            first.Focus(NavigationMethod.Tab);
            fixture.Settle();
            FocusedCardId(fixture).ShouldBe("new-1", "the Queued column is the first with a card");

            ViewHarness.Press(fixture.Window, Key.Right, PhysicalKey.ArrowRight);
            FocusedCardId(fixture).ShouldBe("run-1", "Right moves to the Running column, same row");

            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
            FocusedCardId(fixture).ShouldBe("run-2");
            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
            FocusedCardId(fixture).ShouldBe("run-2", "the last card of a column stops Down");

            ViewHarness.Press(fixture.Window, Key.Right, PhysicalKey.ArrowRight);
            FocusedCardId(fixture).ShouldBe("done-1", "Right skips the empty columns");
            ViewHarness.Press(fixture.Window, Key.Right, PhysicalKey.ArrowRight);
            FocusedCardId(fixture).ShouldBe("done-1", "the last column stops Right");

            ViewHarness.Press(fixture.Window, Key.Left, PhysicalKey.ArrowLeft);
            FocusedCardId(fixture).ShouldBe("run-1", "Left returns to the Running column, same row");
            ViewHarness.Press(fixture.Window, Key.Up, PhysicalKey.ArrowUp);
            FocusedCardId(fixture).ShouldBe("run-1", "the first card of a column stops Up");
            ViewHarness.Press(fixture.Window, Key.Down, PhysicalKey.ArrowDown);
            ViewHarness.Press(fixture.Window, Key.Up, PhysicalKey.ArrowUp);
            FocusedCardId(fixture).ShouldBe("run-1", "Down then Up returns to the first card");

            fixture.View.CurrentCard.ShouldBeSameAs(fixture.View.CardViewOf("run-1"));
            fixture.View.CardViews.Count(static card => card.IsTabStop).ShouldBe(1, "the tab stop roves with the focus");
            fixture.View.CardViewOf("run-1")!.IsTabStop.ShouldBeTrue();
            fixture.View.CardViewOf("run-1")!.SteerTrigger.IsTabStop.ShouldBeTrue();
            fixture.View.CardViewOf("new-1")!.SteerTrigger.IsTabStop.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>Enter on a card asks the owner to open its session; Enter on the Steer button opens the flyout instead.</summary>
    [Fact]
    public async Task EnterOpensTheSessionOfTheFocusedCard()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns);
            var opened = new List<string?>();
            fixture.Board.OpenSessionRequested += (_, args) => opened.Add(args.SessionId);
            var card = fixture.View.CardViewOf("run-2").ShouldNotBeNull();
            card.Focus(NavigationMethod.Tab);
            fixture.Settle();

            ViewHarness.Press(fixture.Window, Key.Enter, PhysicalKey.Enter);

            opened.ShouldBe(["run-2"]);
            card.IsSteerOpen.ShouldBeFalse();

            card.SteerTrigger.Focus(NavigationMethod.Tab);
            ViewHarness.Press(fixture.Window, Key.Enter, PhysicalKey.Enter);

            opened.ShouldBe(["run-2"], "Enter on the Steer button belongs to the button");
            card.IsSteerOpen.ShouldBeTrue();
            card.CloseSteer();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>S opens the steer flyout of the focused card and Esc inside the flyout closes it.</summary>
    [Fact]
    public async Task SOpensTheSteerFlyoutAndEscapeClosesIt()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns);
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            card.Focus(NavigationMethod.Tab);
            fixture.Settle();
            card.IsSteerOpen.ShouldBeFalse();

            ViewHarness.Press(fixture.Window, Key.S, PhysicalKey.S);

            card.IsSteerOpen.ShouldBeTrue();
            card.SteerInput.AcceptsReturn.ShouldBeTrue("the steering message is multi-line");
            card.SteerInput.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape, PhysicalKey = PhysicalKey.Escape, Source = card.SteerInput });
            fixture.Settle();

            card.IsSteerOpen.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A card with no steering service ignores S and shows its Steer button disabled.</summary>
    [Fact]
    public async Task WithoutASteeringServiceSDoesNothing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns, withSteering: false);
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            card.Focus(NavigationMethod.Tab);
            fixture.Settle();

            ViewHarness.Press(fixture.Window, Key.S, PhysicalKey.S);

            card.IsSteerOpen.ShouldBeFalse();
            card.SteerTrigger.IsEffectivelyEnabled.ShouldBeFalse();
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }

    /// <summary>A card focused by keyboard draws the accent focus border at the focus thickness; an unfocused card keeps the same thickness, transparent.</summary>
    [Fact]
    public async Task TheFocusedCardDrawsTheFocusRing()
    {
        await using var session = TestAppBuilder.StartSession();
        await session.Dispatch(() =>
        {
            using var fixture = KanbanFixture.Open(PopulateTwoColumns);
            var application = Application.Current!;
            application.TryGetResource("Autonogrammer.Color.AccentPurple", application.ActualThemeVariant, out var accentBrush).ShouldBeTrue();
            application.TryGetResource("Autonogrammer.Size.FocusBorder", application.ActualThemeVariant, out var thickness).ShouldBeTrue();
            var accent = ((ISolidColorBrush)accentBrush!).Color;
            var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
            var other = fixture.View.CardViewOf("run-2").ShouldNotBeNull();
            var frame = ViewHarness.Find<Border>(card, "Frame");
            var otherFrame = ViewHarness.Find<Border>(other, "Frame");

            card.Focus(NavigationMethod.Tab);
            fixture.Settle();

            ((ISolidColorBrush)frame.BorderBrush!).Color.ShouldBe(accent);
            frame.BorderThickness.ShouldBe((Thickness)thickness!);
            otherFrame.BorderThickness.ShouldBe((Thickness)thickness!, "the ring never moves the layout");
            (otherFrame.BorderBrush as ISolidColorBrush)?.Color.ShouldNotBe(accent);
        }, TestContext.Current.CancellationToken).ConfigureAwait(true);
    }
}
