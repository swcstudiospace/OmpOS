using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;
using static OmpOS.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace OmpOS.Presentation.Tests.Kanban;

/// <summary>
/// The Kanban board model: one card per chat from the session catalog rows, status only from runtime signals, only Backlog and Archived
/// accepting a drop, the stale flag independent of the status, and the unreachable error state.
/// </summary>
public sealed class KanbanBoardModelTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();

    /// <summary>Each chat has one card with its title, model and provider, and the relative time of its last activity.</summary>
    [Fact]
    public void OneCardPerChatShowsTitleModelProviderAndLastActivity()
    {
        _catalog.Sessions.AddRange(
        [
            Session("a", SessionLifecycle.Running, minutesAgo: 3, title: "Refactor the parser"),
            Session("b", SessionLifecycle.Completed, minutesAgo: 90, title: "Write the docs"),
            Session("c", SessionLifecycle.Created, minutesAgo: 0, title: "Spike"),
        ]);
        var info = new Dictionary<string, KanbanModelInfo>(StringComparer.Ordinal)
        {
            ["a"] = new("gpt-5", "openai"),
            ["b"] = new("claude-sonnet-5", null),
        };

        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock, new KanbanBoardOptions { ModelOf = id => info.GetValueOrDefault(id) });

        board.Cards.Count.ShouldBe(3);
        board.Cards.Select(card => card.Id).ShouldBe(["c", "a", "b"]);

        var a = board.CardOf("a").ShouldNotBeNull();
        a.Title.ShouldBe("Refactor the parser");
        a.Model.ShouldBe("gpt-5");
        a.Provider.ShouldBe("openai");
        a.ModelProviderText.ShouldBe("gpt-5 - openai");
        a.LastActivityText.ShouldBe("3 min ago");
        a.LastActivity.ShouldBe(_clock.GetUtcNow().AddMinutes(-3));
        a.ColumnKey.ShouldBe("running");

        board.CardOf("b").ShouldNotBeNull().ModelProviderText.ShouldBe("claude-sonnet-5");
        board.CardOf("b")!.LastActivityText.ShouldBe("1 h ago");
        board.CardOf("c").ShouldNotBeNull().ModelProviderText.ShouldBe("Model unknown");
        board.CardOf("a")!.AccessibleName.ShouldBe("Refactor the parser, Running, gpt-5 - openai, 3 min ago, Stale, no recent heartbeat");

        board.Columns.Sum(column => column.Count).ShouldBe(3);
        board.Columns.Select(column => column.Title).ShouldBe(
            ["Backlog", "Queued", "Running", "Awaiting input", "Needs attention", "Paused", "Completed", "Failed", "Unreachable", "Archived"]);
    }

    /// <summary>A signal sets the status the map gives its kind and moves the card to that column; an unmapped kind changes nothing.</summary>
    [Theory]
    [InlineData(RuntimeSignalKind.RunQueued, RuntimeStatus.Queued, "queued")]
    [InlineData(RuntimeSignalKind.RunStarted, RuntimeStatus.Running, "running")]
    [InlineData(RuntimeSignalKind.InputRequested, RuntimeStatus.AwaitingInput, "awaiting-input")]
    [InlineData(RuntimeSignalKind.ApprovalRequested, RuntimeStatus.NeedsAttention, "needs-attention")]
    [InlineData(RuntimeSignalKind.RunPaused, RuntimeStatus.Paused, "paused")]
    [InlineData(RuntimeSignalKind.RunCompleted, RuntimeStatus.Completed, "completed")]
    [InlineData(RuntimeSignalKind.RunFailed, RuntimeStatus.Failed, "failed")]
    [InlineData(RuntimeSignalKind.SessionUnreachable, RuntimeStatus.Unreachable, "unreachable")]
    public void StatusComesOnlyFromRuntimeSignals(RuntimeSignalKind kind, RuntimeStatus expected, string column)
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Paused, minutesAgo: 5));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("a").ShouldNotBeNull().Status.ShouldBe(RuntimeStatus.Paused);

        board.Apply("a", new TestRuntimeSignal(kind)).ShouldBeTrue();

        var card = board.CardOf("a").ShouldNotBeNull();
        card.Status.ShouldBe(expected);
        card.ColumnKey.ShouldBe(column);
        board.Columns.Single(candidate => string.Equals(candidate.Key, column, StringComparison.Ordinal)).Cards.ShouldContain(card);
    }

    /// <summary>An unmapped signal kind is refused with the card untouched, a signal for no chat changes nothing, and no other call sets a status.</summary>
    [Fact]
    public void AnUnmappedSignalAndAnUnknownChatChangeNothing()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var before = board.CardOf("a");

        Should.Throw<UnmappedRuntimeSignalException>(() => board.Apply("a", new TestRuntimeSignal((RuntimeSignalKind)999)));
        board.Apply("nobody", new TestRuntimeSignal(RuntimeSignalKind.RunFailed)).ShouldBeFalse();
        board.SetStale("nobody", true).ShouldBeFalse();
        Should.Throw<ArgumentNullException>(() => board.Apply("a", null!));

        board.CardOf("a").ShouldBe(before);
        board.Cards.Count.ShouldBe(1);
    }

    /// <summary>A signal stays in force when the catalog refreshes without seeing it, and the catalog's own status change replaces it.</summary>
    [Fact]
    public void ASignalSurvivesARefreshThatHasNotSeenItUntilTheRowStatusChanges()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Created, minutesAgo: 2));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("a").ShouldNotBeNull().Status.ShouldBe(RuntimeStatus.Queued);

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.ApprovalRequested));
        _catalog.Put(Session("a", SessionLifecycle.Created, minutesAgo: 1));
        board.CardOf("a").ShouldNotBeNull().Status.ShouldBe(RuntimeStatus.NeedsAttention);

        _catalog.Put(Session("a", SessionLifecycle.Completed, minutesAgo: 0));
        board.CardOf("a").ShouldNotBeNull().Status.ShouldBe(RuntimeStatus.Completed);
        board.CardOf("a")!.ColumnKey.ShouldBe("completed");
    }

    /// <summary>A signal records now as the chat's last activity.</summary>
    [Fact]
    public void ASignalRecordsTheTimeAsLastActivity()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 30));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("a").ShouldNotBeNull().LastActivityText.ShouldBe("30 min ago");

        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.InputRequested));

        var card = board.CardOf("a").ShouldNotBeNull();
        card.LastActivity.ShouldBe(_clock.GetUtcNow());
        card.LastActivityText.ShouldBe("just now");
    }

    /// <summary>A drop on a runtime column is refused and leaves the card, its status and every column as they were.</summary>
    [Fact]
    public void RuntimeStatusCardRefusesDrag()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var before = board.CardOf("a");

        foreach (var column in board.Columns.Where(candidate => candidate.RuntimeStatus is not null))
        {
            column.AcceptsDrop.ShouldBeFalse(column.Key);
            board.CanDrop(column.Key).ShouldBeFalse(column.Key);
            board.Move("a", column.Key).ShouldBe(KanbanMoveOutcome.RefusedRuntimeColumn, column.Key);
        }

        board.CardOf("a").ShouldBe(before);
        board.CardOf("a")!.Status.ShouldBe(RuntimeStatus.Running);
        board.CardOf("a")!.UserStatus.ShouldBeNull();
    }

    /// <summary>Only Backlog and Archived accept a drop; the card keeps its runtime status and returns to its runtime column when that status changes.</summary>
    [Fact]
    public void BacklogAndArchivedAcceptDrag()
    {
        _catalog.Sessions.AddRange([Session("a", SessionLifecycle.Running, minutesAgo: 1), Session("b", SessionLifecycle.Failed, minutesAgo: 2)]);
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        board.Columns.Where(column => column.AcceptsDrop).Select(column => column.Key).ShouldBe(["backlog", "archived"]);
        board.CanDrop("backlog").ShouldBeTrue();
        board.CanDrop("archived").ShouldBeTrue();
        board.CanDrop("nowhere").ShouldBeFalse();

        board.Move("a", "archived").ShouldBe(KanbanMoveOutcome.Moved);
        var archived = board.CardOf("a").ShouldNotBeNull();
        archived.ColumnKey.ShouldBe("archived");
        archived.UserStatus.ShouldBe(UserSettableStatus.Archived);
        archived.Status.ShouldBe(RuntimeStatus.Running);
        board.Columns.Single(column => string.Equals(column.Key, "archived", StringComparison.Ordinal)).Cards.Select(card => card.Id).ShouldBe(["a"]);
        board.Columns.Single(column => string.Equals(column.Key, "running", StringComparison.Ordinal)).Cards.ShouldBeEmpty();
        board.Move("a", "archived").ShouldBe(KanbanMoveOutcome.AlreadyThere);

        board.Move("a", "backlog").ShouldBe(KanbanMoveOutcome.Moved);
        board.CardOf("a")!.UserStatus.ShouldBe(UserSettableStatus.Backlog);
        board.Columns.Single(column => string.Equals(column.Key, "backlog", StringComparison.Ordinal)).Cards.Select(card => card.Id).ShouldBe(["a"]);

        // A new runtime status takes the card back to its runtime column.
        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.RunCompleted));
        board.CardOf("a")!.UserStatus.ShouldBeNull();
        board.CardOf("a")!.ColumnKey.ShouldBe("completed");

        // The same status again does not undo a placement made in the meantime.
        board.Move("b", "archived").ShouldBe(KanbanMoveOutcome.Moved);
        _catalog.Put(Session("b", SessionLifecycle.Failed, minutesAgo: 0));
        board.CardOf("b")!.ColumnKey.ShouldBe("archived");
    }

    /// <summary>A drop names a chat and a column that exist, or says which one does not.</summary>
    [Fact]
    public void AMoveNamesAnExistingChatAndColumn()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        board.Move("nobody", "archived").ShouldBe(KanbanMoveOutcome.UnknownCard);
        board.Move("a", "nowhere").ShouldBe(KanbanMoveOutcome.UnknownColumn);
        board.Move("a", "backlog").ShouldBe(KanbanMoveOutcome.Moved);
    }

    /// <summary>A heartbeat change sets only the stale flag, in both directions; status and column stay.</summary>
    [Fact]
    public void StaleHeartbeatSetsTheFlagWithoutChangingStatus()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 0));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        var fresh = board.CardOf("a").ShouldNotBeNull();
        fresh.IsStale.ShouldBeFalse();
        fresh.StaleLabel.ShouldBeNull();

        board.SetStale("a", true).ShouldBeTrue();

        var stale = board.CardOf("a").ShouldNotBeNull();
        stale.IsStale.ShouldBeTrue();
        stale.Status.ShouldBe(RuntimeStatus.Running);
        stale.ColumnKey.ShouldBe("running");
        stale.StaleLabel.ShouldBe("Stale, no recent heartbeat");
        stale.AccessibleName.ShouldEndWith(", Stale, no recent heartbeat");
        stale.IsError.ShouldBeFalse();

        // The flag survives a catalog refresh that does not change the row's own flag, and a signal leaves it alone.
        _catalog.Put(Session("a", SessionLifecycle.Running, minutesAgo: 0));
        board.CardOf("a")!.IsStale.ShouldBeTrue();
        board.Apply("a", new TestRuntimeSignal(RuntimeSignalKind.InputRequested));
        var waiting = board.CardOf("a").ShouldNotBeNull();
        waiting.Status.ShouldBe(RuntimeStatus.AwaitingInput);
        waiting.IsStale.ShouldBeTrue();

        board.SetStale("a", false).ShouldBeTrue();
        var recovered = board.CardOf("a").ShouldNotBeNull();
        recovered.IsStale.ShouldBeFalse();
        recovered.Status.ShouldBe(RuntimeStatus.AwaitingInput);
        recovered.StaleLabel.ShouldBeNull();
    }

    /// <summary>The catalog's own stale flag (a Running chat that went quiet) reaches the card with the status unchanged.</summary>
    [Fact]
    public void ACatalogStaleFlagReachesTheCardAndLeavesTheStatus()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 0));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("a")!.IsStale.ShouldBeFalse();

        _clock.Advance(TimeSpan.FromSeconds(90));
        catalog.RefreshTimes();
        board.RefreshTimes();

        var card = board.CardOf("a").ShouldNotBeNull();
        card.IsStale.ShouldBeTrue();
        card.Status.ShouldBe(RuntimeStatus.Running);
        card.LastActivityText.ShouldBe("1 min ago");
    }

    /// <summary>A chat whose runtime session cannot be reached sits in Unreachable and draws the error state; its accessible name says so.</summary>
    [Fact]
    public void UnreachableSessionShowsTheErrorState()
    {
        _catalog.Sessions.AddRange([Session("lost", SessionLifecycle.Lost, minutesAgo: 9), Session("ok", SessionLifecycle.Running, minutesAgo: 1)]);
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        var lost = board.CardOf("lost").ShouldNotBeNull();
        lost.Status.ShouldBe(RuntimeStatus.Unreachable);
        lost.IsError.ShouldBeTrue();
        lost.ErrorText.ShouldBe("Session unreachable");
        lost.ColumnKey.ShouldBe("unreachable");
        lost.AccessibleName.ShouldEndWith(", Session unreachable");
        board.Columns.Single(column => string.Equals(column.Key, "unreachable", StringComparison.Ordinal)).Cards.ShouldBe([lost]);

        var ok = board.CardOf("ok").ShouldNotBeNull();
        ok.IsError.ShouldBeFalse();
        ok.ErrorText.ShouldBeNull();

        // A runtime signal reaches the same state, and a later signal clears it.
        board.Apply("ok", new TestRuntimeSignal(RuntimeSignalKind.SessionUnreachable));
        board.CardOf("ok")!.IsError.ShouldBeTrue();
        board.Apply("ok", new TestRuntimeSignal(RuntimeSignalKind.RunStarted));
        board.CardOf("ok")!.IsError.ShouldBeFalse();
    }

    /// <summary>A chat whose lifecycle the runtime does not report has no status and waits in Backlog; the board does not guess one.</summary>
    [Fact]
    public void AChatWithoutARuntimeStatusSitsInBacklogWithNoStatus()
    {
        _catalog.Sessions.Add(Session("odd", (SessionLifecycle)99, minutesAgo: 1));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);

        var card = board.CardOf("odd").ShouldNotBeNull();
        card.Status.ShouldBeNull();
        card.StatusText.ShouldBe("No status");
        card.ColumnKey.ShouldBe("backlog");
        board.SetStale("odd", true).ShouldBeFalse();

        board.Apply("odd", new TestRuntimeSignal(RuntimeSignalKind.RunStarted)).ShouldBeTrue();
        board.CardOf("odd")!.ColumnKey.ShouldBe("running");
    }

    /// <summary>Chats that join or leave the catalog join or leave the board, and the board announces the change once.</summary>
    [Fact]
    public void ChatsJoinAndLeaveWithTheCatalogAndTheBoardAnnouncesIt()
    {
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock);
        board.IsEmpty.ShouldBeTrue();
        board.Columns.Count.ShouldBe(10);
        var changed = new List<string?>();
        board.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        _catalog.Put(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        board.IsEmpty.ShouldBeFalse();
        changed.ShouldContain(nameof(KanbanBoardModel.Cards), StringComparer.Ordinal);
        changed.ShouldContain(nameof(KanbanBoardModel.Columns), StringComparer.Ordinal);

        changed.Clear();
        board.RefreshTimes();
        changed.ShouldBeEmpty();

        _catalog.Remove("a");
        board.CardOf("a").ShouldBeNull();
        board.IsEmpty.ShouldBeTrue();
    }

    /// <summary>A disposed board stops following the catalog.</summary>
    [Fact]
    public void ADisposedBoardStopsFollowingTheCatalog()
    {
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        var board = new KanbanBoardModel(catalog, _clock);
        board.Dispose();
        board.Dispose();

        _catalog.Put(Session("a", SessionLifecycle.Running, minutesAgo: 1));

        board.IsEmpty.ShouldBeTrue();
    }

    /// <summary>Titles and model texts are plain: control characters are removed, and an empty model reads as unknown.</summary>
    [Fact]
    public void TextsAreSanitised()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1, title: "Fix\u0007 it"));
        using var catalog = new SessionCatalogModel(_catalog, _clock);
        using var board = new KanbanBoardModel(catalog, _clock, new KanbanBoardOptions { ModelOf = _ => new KanbanModelInfo("  ", "\u0001") });

        var card = board.CardOf("a").ShouldNotBeNull();
        card.Title.ShouldBe("Fix it");
        card.Model.ShouldBeNull();
        card.Provider.ShouldBeNull();
        card.ModelProviderText.ShouldBe("Model unknown");
    }

    private SessionSummary Session(string id, SessionLifecycle lifecycle, int minutesAgo, string? title = null)
    {
        var now = _clock.GetUtcNow();
        return Summary(id, lifecycle, now.AddMinutes(-minutesAgo - 5), now.AddMinutes(-minutesAgo), title);
    }
}
