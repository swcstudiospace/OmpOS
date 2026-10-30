using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>
/// The board state machine, the session kind tag, ended and disconnected cards, the Substrate fields, the announcements and the open and start requests.
/// </summary>
public sealed class KanbanBoardStateTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();

    /// <summary>A head that is still restoring shows Loading until it says it is done, then Empty or Ready.</summary>
    [Fact]
    public void LoadingHoldsUntilTheHeadCompletesIt()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock, new KanbanBoardOptions { StartLoading = true });
        var states = new List<BoardState>();
        board.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(KanbanBoardModel.State), StringComparison.Ordinal))
            {
                states.Add(board.State);
            }
        };

        board.State.ShouldBe(BoardState.Loading);
        board.CompleteLoading();
        board.CompleteLoading();

        board.State.ShouldBe(BoardState.Ready);
        states.ShouldBe([BoardState.Ready]);

        _catalog.Remove("a");
        board.State.ShouldBe(BoardState.Empty);
    }

    /// <summary>A board with no session is Empty, and the first session makes it Ready.</summary>
    [Fact]
    public void AnEmptyCatalogIsEmptyAndTheFirstSessionMakesItReady()
    {
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.State.ShouldBe(BoardState.Empty);
        board.ErrorText.ShouldBeNull();

        _catalog.Put(Session("a", SessionLifecycle.Created));

        board.State.ShouldBe(BoardState.Ready);
    }

    /// <summary>A catalog that cannot be read is Error with its reason; Retry shows Loading while it reads and ends in the new state.</summary>
    [Fact]
    public async Task AFailedLoadIsErrorAndRetryReadsAgain()
    {
        _catalog.Failure = new InvalidOperationException("The registry is offline");
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.State.ShouldBe(BoardState.Error);
        board.ErrorText.ShouldBe("The registry is offline");

        await board.RetryCommand.ExecuteAsync(null).ConfigureAwait(true);
        board.State.ShouldBe(BoardState.Error, "the retry failed again");

        _catalog.Failure = null;
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        var states = new List<BoardState>();
        board.PropertyChanged += (_, args) =>
        {
            if (string.Equals(args.PropertyName, nameof(KanbanBoardModel.State), StringComparison.Ordinal))
            {
                states.Add(board.State);
            }
        };
        await board.RetryCommand.ExecuteAsync(null).ConfigureAwait(true);

        states.ShouldBe([BoardState.Loading, BoardState.Ready]);
        board.ErrorText.ShouldBeNull();
        board.CardOf("a").ShouldNotBeNull();
    }

    /// <summary>A failure after cards exist keeps the last good cards in Ready and exposes the reason, so nothing disappears silently.</summary>
    [Fact]
    public void AFailureWithCardsKeepsTheCardsAndShowsTheReason()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        _catalog.Failure = new InvalidOperationException("The registry went away");
        catalog.Refresh().ShouldBeFalse();

        board.State.ShouldBe(BoardState.Ready);
        board.ErrorText.ShouldBe("The registry went away");
        board.CardOf("a").ShouldNotBeNull();
    }

    /// <summary>Each card says whether its session is a chat or an omp terminal, in words, and the spoken name carries it.</summary>
    [Fact]
    public void CardsCarryTheirKindAsWords()
    {
        _catalog.Sessions.Add(Session("chat", SessionLifecycle.Running, title: "Plan it"));
        _catalog.Sessions.Add(Session("term", SessionLifecycle.Running, title: "Shell one") with { Kind = SessionKind.Terminal });
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        var chat = board.CardOf("chat").ShouldNotBeNull();
        var terminal = board.CardOf("term").ShouldNotBeNull();
        chat.KindText.ShouldBe("Chat");
        chat.Kind.ShouldBe(SessionKind.Chat);
        terminal.KindText.ShouldBe("Terminal");
        terminal.Kind.ShouldBe(SessionKind.Terminal);
        terminal.SpokenName.ShouldContain(", Terminal", Case.Sensitive);
        chat.SpokenName.ShouldContain(", Chat", Case.Sensitive);
        terminal.AccessibleName.ShouldNotContain("Terminal", Case.Sensitive);
    }

    /// <summary>A completed, failed or closed session stays as a card in its terminal column and is flagged ended; closing never removes it (DR-5b).</summary>
    [Fact]
    public void EndedAndClosedSessionsStayInTheirTerminalColumn()
    {
        _catalog.Sessions.AddRange(
        [
            Session("done", SessionLifecycle.Completed),
            Session("bad", SessionLifecycle.Failed),
            Session("shut", SessionLifecycle.Paused),
            Session("live", SessionLifecycle.Running),
        ]);
        var closed = new HashSet<string>(StringComparer.Ordinal);
        using var catalog = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = closed.Contains });
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("shut").ShouldNotBeNull().ColumnKey.ShouldBe("paused");

        closed.Add("shut");
        _catalog.RaiseChanged();

        board.CardOf("done").ShouldNotBeNull().ColumnKey.ShouldBe("completed");
        board.CardOf("done")!.IsEnded.ShouldBeTrue();
        board.CardOf("bad").ShouldNotBeNull().ColumnKey.ShouldBe("failed");
        board.CardOf("bad")!.IsEnded.ShouldBeTrue();
        var shut = board.CardOf("shut").ShouldNotBeNull();
        shut.ColumnKey.ShouldBe("archived");
        shut.IsClosed.ShouldBeTrue();
        shut.IsEnded.ShouldBeTrue();
        shut.SpokenName.ShouldContain(", closed", Case.Sensitive);
        board.CardOf("live").ShouldNotBeNull().IsEnded.ShouldBeFalse();
        board.Cards.Count.ShouldBe(4);
        board.Columns.Single(column => string.Equals(column.Key, "archived", StringComparison.Ordinal)).Cards.Select(card => card.Id).ShouldBe(["shut"]);
    }

    /// <summary>A session that ends through a signal moves to its terminal column, and a terminal session behaves the same as a chat.</summary>
    [Fact]
    public void ARunThatEndsMovesItsCardToTheTerminalColumn()
    {
        _catalog.Sessions.Add(Session("term", SessionLifecycle.Running) with { Kind = SessionKind.Terminal });
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("term").ShouldNotBeNull().ColumnKey.ShouldBe("running");

        board.Apply("term", new TestRuntimeSignal(RuntimeSignalKind.RunCompleted));

        var card = board.CardOf("term").ShouldNotBeNull();
        card.ColumnKey.ShouldBe("completed");
        card.IsEnded.ShouldBeTrue();
        board.Cards.Count.ShouldBe(1);
    }

    /// <summary>A dropped engine is Disconnected, in its own column and words, and a failed run is not.</summary>
    [Fact]
    public void ADroppedEngineIsDisconnectedAndNotFailed()
    {
        _catalog.Sessions.Add(Session("lost", SessionLifecycle.Lost) with { Kind = SessionKind.Terminal });
        _catalog.Sessions.Add(Session("bad", SessionLifecycle.Failed));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        var lost = board.CardOf("lost").ShouldNotBeNull();
        lost.IsDisconnected.ShouldBeTrue();
        lost.DisconnectedText.ShouldBe("Disconnected, the engine dropped");
        lost.ColumnKey.ShouldBe("unreachable");
        lost.SpokenName.ShouldContain("Disconnected, the engine dropped", Case.Sensitive);
        var failed = board.CardOf("bad").ShouldNotBeNull();
        failed.IsDisconnected.ShouldBeFalse();
        failed.DisconnectedText.ShouldBeNull();
        failed.StatusText.ShouldBe("Failed");
        string.Equals(lost.StatusText, failed.StatusText, StringComparison.Ordinal).ShouldBeFalse("a lost card and a failed card must not share a status text");
    }

    /// <summary>Substrate fields come from the source only: unreachable, null, missing fields and a faulting source all read unavailable.</summary>
    [Fact]
    public async Task SubstrateFieldsAreShownOnlyWhenReportedAndUnavailableOtherwise()
    {
        _catalog.Sessions.AddRange([Session("a", SessionLifecycle.Running), Session("b", SessionLifecycle.Running), Session("c", SessionLifecycle.Running)]);
        var source = new FakeCardDetailsSource();
        source.Answers["a"] = new KanbanCardDetails("Ship the board", "ut-abc-1234", 3, "agent-swarm", Available: true);
        source.Answers["b"] = new KanbanCardDetails("Ignored", "ignored", 9, "ignored", Available: false);
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock, new KanbanBoardOptions { Details = source });

        board.CardModelOf("a").ShouldNotBeNull().TaskText.ShouldBe("Task: unavailable", "nothing is read before the first refresh");

        await board.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);

        var a = board.CardModelOf("a").ShouldNotBeNull();
        a.IsSubstrateAvailable.ShouldBeTrue();
        a.TaskText.ShouldBe("Task: Ship the board");
        a.GraphText.ShouldBe("Graph: ut-abc-1234");
        a.LessonsText.ShouldBe("Lessons: 3");
        a.LeaseText.ShouldBe("Lease: agent-swarm");
        a.SubstrateName.ShouldBe("Substrate, Task: Ship the board, Graph: ut-abc-1234, Lessons: 3, Lease: agent-swarm");
        foreach (var id in new[] { "b", "c" })
        {
            var model = board.CardModelOf(id).ShouldNotBeNull();
            model.IsSubstrateAvailable.ShouldBeFalse();
            model.TaskText.ShouldBe("Task: unavailable");
            model.GraphText.ShouldBe("Graph: unavailable");
            model.LessonsText.ShouldBe("Lessons: unavailable");
            model.LeaseText.ShouldBe("Lease: unavailable");
        }

        source.Answers["c"] = new KanbanCardDetails("Only a task", null, null, null, Available: true);
        await board.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        var partial = board.CardModelOf("c").ShouldNotBeNull();
        partial.TaskText.ShouldBe("Task: Only a task");
        partial.GraphText.ShouldBe("Graph: unavailable");
        partial.LessonsText.ShouldBe("Lessons: unavailable");
        partial.LeaseText.ShouldBe("Lease: unavailable");

        source.Failure = new InvalidOperationException("substrate is down");
        await board.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        board.CardModels.ShouldAllBe(model => !model.IsSubstrateAvailable);
    }

    /// <summary>Without a source every card reads unavailable, and a cancelled read is cancelled, not unavailable.</summary>
    [Fact]
    public async Task NoSourceReadsUnavailableAndCancellationPropagates()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var bare = new KanbanBoardModel(catalog, _clock);
        await bare.RefreshDetailsAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
        bare.CardModelOf("a").ShouldNotBeNull().LeaseText.ShouldBe("Lease: unavailable");

        var source = new FakeCardDetailsSource();
        using var sourced = new KanbanBoardModel(catalog, _clock, new KanbanBoardOptions { Details = source });
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync().ConfigureAwait(true);

        await Should.ThrowAsync<OperationCanceledException>(() => sourced.RefreshDetailsAsync(cancelled.Token)).ConfigureAwait(true);
        source.Reads.ShouldBeEmpty();
    }

    /// <summary>A status change is announced with the card's title; a new card and an unchanged refresh say nothing; a disconnect says so.</summary>
    [Fact]
    public void StatusChangesAreAnnounced()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, title: "Build"));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.Announcement.ShouldBeEmpty();

        board.RefreshTimes();
        board.Announcement.ShouldBeEmpty();

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.ApprovalRequested));
        board.Announcement.ShouldBe("Build: Needs attention");

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.SessionUnreachable));
        board.Announcement.ShouldBe("Build: Disconnected, the engine dropped");

        board.Move("a", "backlog").ShouldBe(KanbanMoveOutcome.Moved);
        board.Announcement.ShouldBe("Build: moved to Backlog");
    }

    /// <summary>Activating a card raises the open request for its session; an unknown session raises nothing.</summary>
    [Fact]
    public async Task ActivatingACardRequestsOpeningItsSession()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var opened = new List<string?>();
        board.OpenSessionRequested += (_, args) => opened.Add(args.SessionId);

        await board.CardModelOf("a").ShouldNotBeNull().OpenCommand.ExecuteAsync(null).ConfigureAwait(true);
        board.RequestOpen("nobody").ShouldBeFalse();
        board.RequestOpen("a").ShouldBeTrue();

        opened.ShouldBe(["a", "a"]);
    }

    /// <summary>The start request of the empty board reaches the owner of the sessions.</summary>
    [Fact]
    public async Task StartSessionCommandRaisesTheRequest()
    {
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var raised = 0;
        board.StartSessionRequested += (_, _) => raised++;

        await board.StartSessionCommand.ExecuteAsync(null).ConfigureAwait(true);

        raised.ShouldBe(1);
    }

    /// <summary>A card keeps its view model across status changes; a session that leaves the catalog loses it.</summary>
    [Fact]
    public void TheViewModelOfACardLivesAsLongAsItsSessionIsOnTheBoard()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var model = board.CardModelOf("a").ShouldNotBeNull();
        var before = model.Card;

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.InputRequested));

        board.CardModelOf("a").ShouldBeSameAs(model);
        model.Card.ShouldNotBe(before);
        model.Card.Status.ShouldBe(RuntimeStatus.AwaitingInput);
        board.CardModels.Select(candidate => candidate.Id).ShouldBe(board.Cards.Select(card => card.Id));

        _catalog.Remove("a");
        board.CardModelOf("a").ShouldBeNull();
        board.CardModels.ShouldBeEmpty();
    }

    private SessionSummary Session(string id, SessionLifecycle lifecycle, string? title = null)
    {
        var now = _clock.GetUtcNow();
        return Summary(id, lifecycle, now.AddMinutes(-10), now.AddMinutes(-1), title);
    }
}
