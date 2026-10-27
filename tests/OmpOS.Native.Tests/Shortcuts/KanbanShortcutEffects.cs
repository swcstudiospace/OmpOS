using Avalonia.Controls;
using Avalonia.Input;
using OmpOS.Contracts.Agents;
using OmpOS.Native.Tests.AgentPanel.Views;
using OmpOS.Native.Tests.Kanban;
using OmpOS.Native.Views.Kanban;
using Shouldly;

namespace OmpOS.Native.Tests.Shortcuts;

/// <summary>What each gesture of the Kanban board does, observed on a real board over a scripted catalog: where the focus moves, what opens.</summary>
internal static class KanbanShortcutEffects
{
    private static void Populate(FakeSessionCatalog fake, ManualTimeProvider clock)
    {
        fake.Sessions.Add(KanbanFixture.Session(clock, "run-1", SessionLifecycle.Running, "First running", minutesAgo: 1));
        fake.Sessions.Add(KanbanFixture.Session(clock, "run-2", SessionLifecycle.Running, "Second running", minutesAgo: 2));
        fake.Sessions.Add(KanbanFixture.Session(clock, "done-1", SessionLifecycle.Completed, "Finished work", minutesAgo: 30));
        fake.Sessions.Add(KanbanFixture.Session(clock, "new-1", SessionLifecycle.Created, "Queued work", minutesAgo: 3));
    }

    private static string? FocusedCardId(KanbanFixture fixture) =>
        (TopLevel.GetTopLevel(fixture.View)!.FocusManager!.GetFocusedElement() as KanbanCardView)?.Model?.Id;

    /// <summary>An arrow key moves the focus from one card to the neighbour in its direction.</summary>
    /// <param name="gesture">The arrow gesture.</param>
    /// <param name="from">The card that has the focus before.</param>
    /// <param name="to">The card that must have the focus after.</param>
    internal static void ArrowMovesFocus(KeyGesture gesture, string from, string to)
    {
        using var fixture = KanbanFixture.Open(Populate);
        fixture.View.CardViewOf(from).ShouldNotBeNull().Focus(NavigationMethod.Tab);
        fixture.Settle();
        FocusedCardId(fixture).ShouldBe(from);

        ShortcutKeys.Press(fixture.Window, gesture);

        FocusedCardId(fixture).ShouldBe(to);
    }

    /// <summary>Enter on a card asks the owner to open its session.</summary>
    /// <param name="gesture">The Enter gesture.</param>
    internal static void OpenCard(KeyGesture gesture)
    {
        using var fixture = KanbanFixture.Open(Populate);
        var opened = new List<string?>();
        fixture.Board.OpenSessionRequested += (_, args) => opened.Add(args.SessionId);
        var card = fixture.View.CardViewOf("run-2").ShouldNotBeNull();
        card.Focus(NavigationMethod.Tab);
        fixture.Settle();

        ShortcutKeys.Press(fixture.Window, gesture);

        opened.ShouldBe(["run-2"]);
    }

    /// <summary>S on a card opens its steering box.</summary>
    /// <param name="gesture">The S gesture.</param>
    internal static void SteerCard(KeyGesture gesture)
    {
        using var fixture = KanbanFixture.Open(Populate);
        var card = fixture.View.CardViewOf("run-1").ShouldNotBeNull();
        card.Focus(NavigationMethod.Tab);
        fixture.Settle();
        card.IsSteerOpen.ShouldBeFalse();

        ShortcutKeys.Press(fixture.Window, gesture);

        card.IsSteerOpen.ShouldBeTrue();
        card.CloseSteer();
    }
}
