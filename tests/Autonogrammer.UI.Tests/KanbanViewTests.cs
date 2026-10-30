using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.UI.Components.Kanban;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using Shouldly;
using Xunit;

namespace Autonogrammer.UI.Tests;

/// <summary>The web Kanban tab: ten columns on a scrolling board, cards with status words and icons, every board state, steering with a reason when it is off, and arrow-key navigation (PX-09).</summary>
public sealed class KanbanViewTests : UiTestContext
{
    private readonly Dictionary<string, string> _cardIds = new(StringComparer.Ordinal);

    private static readonly string[] ColumnKeys =
    [
        "backlog", "queued", "running", "awaiting-input", "needs-attention", "paused", "completed", "failed", "unreachable", "archived",
    ];

    /// <summary>The board draws all ten desktop columns in desktop order inside one scrolling group, each named with its card count, so no column is cut off.</summary>
    [Fact]
    public void TenColumnsAreDrawnInDesktopOrderInOneScrollingGroup()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build the board", SessionLifecycle.Running), ("b", "Write the docs", SessionLifecycle.Completed)), withSteering: false);

        var cut = RenderBoard(fixture.Board);

        var columns = cut.FindAll("[data-column]");
        columns.Select(static column => column.GetAttribute("data-column")).ShouldBe(ColumnKeys);
        cut.FindAll(".autonogrammer-kanban-columns").Count.ShouldBe(1);
        cut.Find(".autonogrammer-kanban-columns").GetAttribute("aria-label").ShouldBe(KanbanCopy.ColumnsName);
        columns.ShouldAllBe(column => column.ParentElement!.ClassList.Contains("autonogrammer-kanban-columns"));
        cut.Find("[data-column='running']").GetAttribute("aria-label").ShouldBe("Running, 1 task");
        cut.Find("[data-column='failed']").GetAttribute("aria-label").ShouldBe("Failed, 0 tasks");
        cut.Find("[data-column='failed']").TextContent.ShouldContain(KanbanCopy.EmptyColumn);
    }

    /// <summary>A card names its session, says its status in words beside an icon, names its kind, and reads every Substrate field as unavailable instead of inventing one.</summary>
    [Fact]
    public void CardShowsTitleStatusWordsIconKindAndHonestSubstrateFields()
    {
        using var fixture = new KanbanBoardFixture(CatalogWithKind("term-1", "Shell session", SessionLifecycle.Running, SessionKind.Terminal), withSteering: false);

        var cut = RenderBoard(fixture.Board);

        var card = cut.Find("[data-column='running'] li");
        card.QuerySelector(".autonogrammer-kanban-card-open")!.TextContent.ShouldBe("Shell session");
        card.QuerySelector(".autonogrammer-kanban-status-chip")!.TextContent.Trim().ShouldBe("Running");
        card.QuerySelector(".autonogrammer-kanban-status-chip svg")!.GetAttribute("aria-hidden").ShouldBe("true");
        card.QuerySelector(".autonogrammer-kanban-status-chip svg")!.GetAttribute("data-icon").ShouldBe("Running");
        card.QuerySelector(".autonogrammer-chip")!.TextContent.ShouldBe("Terminal");
        card.QuerySelectorAll(".autonogrammer-kanban-substrate li").Select(static item => item.TextContent)
            .ShouldBe(["Task: unavailable", "Graph: unavailable", "Lessons: unavailable", "Lease: unavailable"]);
        cut.Find("[data-substrate='unavailable']").TextContent.ShouldBe(KanbanCopy.SubstrateUnavailable);
    }

    /// <summary>A session whose engine dropped is Unreachable and says it is disconnected, which is not the Failed column.</summary>
    [Fact]
    public void DroppedEngineShowsDisconnectedInTheUnreachableColumn()
    {
        using var fixture = new KanbanBoardFixture(CatalogWithKind("lost-1", "Dropped terminal", SessionLifecycle.Lost, SessionKind.Terminal), withSteering: false);

        var cut = RenderBoard(fixture.Board);

        cut.Find("[data-column='unreachable'] li").TextContent.ShouldContain("Disconnected, the engine dropped");
        cut.FindAll("[data-column='failed'] li").Count.ShouldBe(0);
    }

    /// <summary>While the head restores its sessions the board shows the loading state and no columns.</summary>
    [Fact]
    public void LoadingStateShowsAHeadingAndNoColumns()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: false, startLoading: true);

        var cut = RenderBoard(fixture.Board);

        cut.Find(".autonogrammer-kanban").GetAttribute("data-state").ShouldBe("loading");
        cut.Find(".autonogrammer-kanban-state").GetAttribute("aria-busy").ShouldBe("true");
        cut.Find("h3").TextContent.ShouldBe(KanbanCopy.LoadingHeading);
        cut.FindAll("[data-column]").Count.ShouldBe(0);

        fixture.Board.CompleteLoading();

        cut.WaitForAssertion(() => cut.FindAll("[data-column]").Count.ShouldBe(10));
    }

    /// <summary>An empty board says so and offers New agent, which asks the board's owner to start a session.</summary>
    [Fact]
    public void EmptyStateOffersNewAgentWhichRaisesTheStartRequest()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(), withSteering: false);
        var starts = 0;
        fixture.Board.StartSessionRequested += (_, _) => starts++;

        var cut = RenderBoard(fixture.Board);

        cut.Find("h3").TextContent.ShouldBe(KanbanCopy.EmptyHeading);
        cut.Find(".autonogrammer-kanban-state-body").TextContent.ShouldBe(KanbanCopy.EmptyBody);
        cut.FindAll("[data-column]").Count.ShouldBe(0);
        var button = cut.Find(".autonogrammer-kanban-state button");
        button.TextContent.ShouldBe(KanbanCopy.NewAgent);
        button.GetAttribute("aria-label")!.ShouldContain(KanbanCopy.NewAgent);

        button.Click();

        starts.ShouldBe(1);
    }

    /// <summary>An unreadable catalog shows its reason in an alert with Retry, and Retry reads again and ends in the columns.</summary>
    [Fact]
    public void ErrorStateShowsTheReasonAndRetryRecovers()
    {
        var catalog = CatalogOf();
        catalog.Failure = new InvalidOperationException("The registry is offline");
        using var fixture = new KanbanBoardFixture(catalog, withSteering: false);

        var cut = RenderBoard(fixture.Board);

        cut.Find(".autonogrammer-kanban").GetAttribute("data-state").ShouldBe("error");
        cut.Find("[role='alert']").TextContent.ShouldBe("The registry is offline");
        catalog.Failure = null;
        catalog.Sessions.Add(KanbanBoardFixture.Session("a", "Build", SessionLifecycle.Running));

        cut.Find(".autonogrammer-kanban-state button").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find(".autonogrammer-kanban").GetAttribute("data-state").ShouldBe("ready");
            cut.FindAll("[data-column]").Count.ShouldBe(10);
        });
    }

    /// <summary>Activating a card reports its session to the host; without a host callback the board raises its own open request.</summary>
    [Fact]
    public void OpeningACardReportsTheSessionToTheHostOrTheBoard()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: false);
        var opened = new List<string>();
        var cut = RenderBoard(fixture.Board, opened);

        cut.Find(".autonogrammer-kanban-card-open").Click();

        opened.ShouldBe(["a"]);

        var requested = new List<string>();
        fixture.Board.OpenSessionRequested += (_, args) => requested.Add(args.SessionId!);
        var bare = RenderBoard(fixture.Board);

        bare.Find(".autonogrammer-kanban-card-open").Click();

        requested.ShouldBe(["a"]);
        opened.Count.ShouldBe(1, "the host callback is not called when the board answered");
    }

    /// <summary>A session that cannot be steered keeps a focusable but inert Steer button and says why, in text the button points to.</summary>
    [Fact]
    public void SteerIsDisabledWithAVisibleReasonWhenTheBoardHasNoSteeringService()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: false);
        var cut = RenderBoard(fixture.Board);

        var steer = cut.Find("button[aria-label='Steer Build']");
        steer.GetAttribute("aria-disabled").ShouldBe("true");
        var reason = cut.Find("#" + steer.GetAttribute("aria-describedby"));
        reason.TextContent.ShouldBe(KanbanCopy.SteerUnavailable);

        steer.Click();

        cut.FindAll("textarea").Count.ShouldBe(0, "a disabled Steer must not open the box");
        steer.GetAttribute("aria-expanded").ShouldBe("false");
    }

    /// <summary>An ended session explains that it cannot be steered even when the board has a steering service.</summary>
    [Fact]
    public void SteerExplainsThatAnEndedSessionCannotBeSteered()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Write the docs", SessionLifecycle.Completed)), withSteering: true);
        var cut = RenderBoard(fixture.Board);

        var steer = cut.Find("button[aria-label='Steer Write the docs']");

        steer.GetAttribute("aria-disabled").ShouldBe("true");
        cut.Find("#" + steer.GetAttribute("aria-describedby")).TextContent.ShouldBe(KanbanCopy.SteerEnded);
    }

    /// <summary>Steer opens an inline box; Send now and Queue deliver the typed text through the steering service with the right mode, an accepted send clears the box and announces the result.</summary>
    [Fact]
    public void SteeringBoxSendsTheTypedTextWithTheChosenModeAndReportsTheResult()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: true);
        var cut = RenderBoard(fixture.Board);
        cut.Find("button[aria-label='Steer Build']").Click();
        var group = cut.Find("[role='group'][aria-label='Steer session Build']");
        group.QuerySelector("textarea")!.GetAttribute("aria-label").ShouldBe(KanbanCopy.SteerBoxName);
        group.QuerySelector("button[aria-label='" + KanbanCopy.SendNowName + "']")!.HasAttribute("disabled").ShouldBeTrue("nothing is typed yet");

        cut.Find("textarea").Input("Check the logs");
        cut.Find("button[aria-label='" + KanbanCopy.SendNowName + "']").Click();

        cut.WaitForAssertion(() => cut.Find(".autonogrammer-kanban-steer-result").TextContent.ShouldBe("Sent to the session"));
        fixture.Steering.Calls.ShouldBe([("a", "Check the logs", CardSteerMode.SendNow)]);
        cut.Find("textarea").GetAttribute("value").ShouldBeNullOrEmpty();

        cut.Find("textarea").Input("Then the tests");
        cut.Find("button[aria-label='" + KanbanCopy.QueueNextName + "']").Click();

        cut.WaitForAssertion(() => fixture.Steering.Calls.Count.ShouldBe(2));
        fixture.Steering.Calls[1].ShouldBe(("a", "Then the tests", CardSteerMode.FollowUp));
    }

    /// <summary>A refused send keeps the typed text and shows the refusal as an error.</summary>
    [Fact]
    public void RefusedSteeringKeepsTheTextAndShowsTheError()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: true);
        fixture.Steering.Result = CardSteerResult.Failed("omp refused the message");
        var cut = RenderBoard(fixture.Board);
        cut.Find("button[aria-label='Steer Build']").Click();
        cut.Find("textarea").Input("Stop");

        cut.Find("button[aria-label='" + KanbanCopy.SendNowName + "']").Click();

        cut.WaitForAssertion(() =>
        {
            var result = cut.Find(".autonogrammer-kanban-steer-result");
            result.TextContent.ShouldBe("omp refused the message");
            result.GetAttribute("data-failed").ShouldBe("true");
            result.GetAttribute("aria-live").ShouldBe("polite");
        });
        cut.Find("textarea").GetAttribute("value").ShouldBe("Stop");
    }

    /// <summary>Escape inside the steering box closes it and returns the focus to the Steer button.</summary>
    [Fact]
    public void EscapeClosesTheSteeringBoxAndReturnsFocusToSteer()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: true);
        var cut = RenderBoard(fixture.Board);
        var steerId = cut.Find("button[aria-label='Steer Build']").InitialReferenceId();
        cut.Find("button[aria-label='Steer Build']").Click();

        cut.Find("[role='group'][aria-label='Steer session Build']").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        cut.FindAll("textarea").Count.ShouldBe(0);
        JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(steerId);
    }

    /// <summary>One card holds the tab stop; the arrow keys move it Down and Up inside a column, Left and Right across columns (keeping the row where the column has one), and Home and End to the ends.</summary>
    [Fact]
    public void ArrowKeysMoveTheFocusBetweenCardsAndColumns()
    {
        using var fixture = new KanbanBoardFixture(
            CatalogOf(("a", "First running", SessionLifecycle.Running), ("c", "Second running", SessionLifecycle.Running), ("b", "Finished", SessionLifecycle.Completed)),
            withSteering: false);
        var cut = RenderBoard(fixture.Board);
        TabStops(cut).ShouldBe(["First running"]);

        Press(cut, "First running", "ArrowDown");
        AssertFocusMovedTo(cut, 1, "Second running");
        TabStops(cut).ShouldBe(["Second running"]);

        Press(cut, "Second running", "ArrowRight");
        AssertFocusMovedTo(cut, 2, "Finished");
        TabStops(cut).ShouldBe(["Finished"]);

        Press(cut, "Finished", "ArrowLeft");
        AssertFocusMovedTo(cut, 3, "First running");

        Press(cut, "First running", "End");
        AssertFocusMovedTo(cut, 4, "Second running");

        Press(cut, "Second running", "Home");
        AssertFocusMovedTo(cut, 5, "First running");
    }

    /// <summary>At the edge of the board a navigation key leaves the focus where it is.</summary>
    [Fact]
    public void ArrowKeysStopAtTheEdgesOfTheBoard()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Only card", SessionLifecycle.Running)), withSteering: false);
        var cut = RenderBoard(fixture.Board);

        foreach (var key in new[] { "ArrowUp", "ArrowDown", "ArrowLeft", "ArrowRight" })
        {
            Press(cut, "Only card", key);
        }

        JSInterop.FocusCalls().ShouldBeEmpty("there is no other card to focus");
        TabStops(cut).ShouldBe(["Only card"]);
    }

    /// <summary>Moving a card to Backlog puts it in that column, announces the move in the polite live region and keeps the focus on the card.</summary>
    [Fact]
    public void MovingACardToBacklogAnnouncesTheMoveAndKeepsTheFocus()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: false);
        var cut = RenderBoard(fixture.Board);

        cut.Find("button[aria-label='Move to Backlog, Build']").Click();

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-column='backlog'] .autonogrammer-kanban-card-open").TextContent.ShouldBe("Build");
            cut.FindAll("[data-column='running'] li").Count.ShouldBe(0);
            var live = cut.Find(".autonogrammer-kanban-live");
            live.GetAttribute("role").ShouldBe("status");
            live.TextContent.ShouldBe("Build: moved to Backlog");
        });
        // The card is a new element in the Backlog column, so its reference id is not in the markup: the focus call and the roving tab stop show where the focus went.
        JSInterop.FocusCalls().Count.ShouldBe(1);
        cut.Find("[data-column='backlog'] .autonogrammer-kanban-card-open").GetAttribute("tabindex").ShouldBe("0");
    }

    /// <summary>A runtime signal that changes a card's status is announced by the live region and moves the card to the column of its new status.</summary>
    [Fact]
    public void ARuntimeSignalMovesTheCardAndIsAnnounced()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running)), withSteering: false);
        var cut = RenderBoard(fixture.Board);

        fixture.Board.Apply("a", new WorkspaceRuntimeSignal(RuntimeSignalKind.RunCompleted));

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-column='completed'] .autonogrammer-kanban-card-open").TextContent.ShouldBe("Build");
            cut.Find(".autonogrammer-kanban-live").TextContent.ShouldBe("Build: Completed");
        });
    }

    /// <summary>Every button, region and text box on every board state has an accessible name.</summary>
    [Fact]
    public void EveryInteractiveElementHasAnAccessibleName()
    {
        using var fixture = new KanbanBoardFixture(CatalogOf(("a", "Build", SessionLifecycle.Running), ("b", "Done", SessionLifecycle.Completed)), withSteering: true);
        var cut = RenderBoard(fixture.Board);
        cut.Find("button[aria-label='Steer Build']").Click();

        foreach (var element in cut.FindAll("button, textarea, [role='group']"))
        {
            var named = element.HasAttribute("aria-label") || element.TextContent.Trim().Length > 0 || element.HasAttribute("aria-labelledby");
            named.ShouldBeTrue("an element has no accessible name: " + element.OuterHtml);
        }

        cut.Markup.ShouldNotContain("<script", Case.Insensitive);
        cut.FindAll("[style]").Count.ShouldBe(0, "inline style attributes are forbidden by the CSP");
    }

    private static WorkspaceSessionCatalog CatalogOf(params (string Id, string Title, SessionLifecycle Lifecycle)[] sessions)
    {
        var catalog = new WorkspaceSessionCatalog();
        foreach (var (id, title, lifecycle) in sessions)
        {
            catalog.Sessions.Add(KanbanBoardFixture.Session(id, title, lifecycle));
        }

        return catalog;
    }

    private static WorkspaceSessionCatalog CatalogWithKind(string id, string title, SessionLifecycle lifecycle, SessionKind kind)
    {
        var catalog = new WorkspaceSessionCatalog();
        catalog.Sessions.Add(KanbanBoardFixture.Session(id, title, lifecycle, kind));
        return catalog;
    }

    private IRenderedComponent<KanbanView> RenderBoard(KanbanBoardModel board, List<string>? opened = null)
    {
        var cut = Render<KanbanView>(parameters =>
        {
            parameters.Add(p => p.Board, board);
            if (opened is not null)
            {
                parameters.Add(p => p.OnOpenSession, (string id) => opened.Add(id));
            }
        });

        // The reference ids are in the markup only until a card renders again: record them now, compare them with the focus calls later.
        foreach (var card in cut.FindAll(".autonogrammer-kanban-card-open"))
        {
            _cardIds[card.TextContent] = card.InitialReferenceId();
        }

        return cut;
    }

    private static AngleSharp.Dom.IElement Open(IRenderedComponent<KanbanView> cut, string title) =>
        cut.FindAll(".autonogrammer-kanban-card-open").Single(button => string.Equals(button.TextContent, title, StringComparison.Ordinal));

    private static List<string> TabStops(IRenderedComponent<KanbanView> cut) =>
        cut.FindAll(".autonogrammer-kanban-card-open").Where(static button => string.Equals(button.GetAttribute("tabindex"), "0", StringComparison.Ordinal)).Select(static button => button.TextContent).ToList();

    private static void Press(IRenderedComponent<KanbanView> cut, string title, string key) =>
        Open(cut, title).KeyDown(new KeyboardEventArgs { Key = key });

    private void AssertFocusMovedTo(IRenderedComponent<KanbanView> cut, int expectedCalls, string title) =>
        cut.WaitForAssertion(() =>
        {
            JSInterop.FocusCalls().Count.ShouldBe(expectedCalls);
            JSInterop.FocusCalls()[^1].ReferenceIdOf().ShouldBe(_cardIds[title]);
            Open(cut, title).GetAttribute("tabindex").ShouldBe("0");
        });

    private object LastFocusTarget()
    {
        var calls = JSInterop.FocusCalls();
        calls.ShouldNotBeEmpty("the board should have moved the focus");
        return calls[^1].Arguments[0]!;
    }
}
