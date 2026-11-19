using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Autonogrammer.Presentation.Tests.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.Kanban;

/// <summary>A closed session sits in Archived whatever its runtime status, a drop cannot move it out, and the column agrees with <see cref="KanbanCard.IsEnded"/>.</summary>
public sealed class KanbanClosedColumnRegressionTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();
    private readonly HashSet<string> _closed = new(StringComparer.Ordinal);

    /// <summary>A closed session whose runtime status is Completed lands in Archived, not in its runtime column.</summary>
    [Fact]
    public void AClosedCompletedSessionLandsInArchived()
    {
        _catalog.Sessions.Add(Session("done", SessionLifecycle.Completed));
        using var catalog = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = _closed.Contains });
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("done").ShouldNotBeNull().ColumnKey.ShouldBe("completed");

        _closed.Add("done");
        _catalog.RaiseChanged();

        var card = board.CardOf("done").ShouldNotBeNull();
        card.Status.ShouldBe(RuntimeStatus.Completed);
        card.ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        card.UserStatus.ShouldBe(UserSettableStatus.Archived);
        card.IsClosed.ShouldBeTrue();
        card.IsEnded.ShouldBeTrue();
    }

    /// <summary>A closed session stays in Archived when the user had placed it in Backlog before closing, and a drop out of Archived is refused.</summary>
    [Fact]
    public void AClosedSessionIgnoresAPlacementAndRefusesADropOutOfArchived()
    {
        _catalog.Sessions.Add(Session("shut", SessionLifecycle.Paused));
        using var catalog = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = _closed.Contains });
        using var board = new KanbanBoardModel(catalog, _clock);
        board.Move("shut", KanbanColumn.BacklogKey).ShouldBe(KanbanMoveOutcome.Moved);
        board.CardOf("shut").ShouldNotBeNull().ColumnKey.ShouldBe(KanbanColumn.BacklogKey);

        _closed.Add("shut");
        _catalog.RaiseChanged();

        var shut = board.CardOf("shut").ShouldNotBeNull();
        shut.ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        shut.UserStatus.ShouldBe(UserSettableStatus.Archived);
        board.Move("shut", KanbanColumn.BacklogKey).ShouldBe(KanbanMoveOutcome.RefusedClosed);
        board.Move("shut", KanbanColumn.ArchivedKey).ShouldBe(KanbanMoveOutcome.AlreadyThere);
        board.CardOf("shut").ShouldNotBeNull().ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        board.Columns.Single(column => string.Equals(column.Key, KanbanColumn.BacklogKey, StringComparison.Ordinal)).Cards.ShouldBeEmpty();
    }

    /// <summary>Every card in Archived is ended, and a card is ended exactly when it sits in a terminal column.</summary>
    [Fact]
    public void ColumnAndIsEndedAgreeForEveryCard()
    {
        _catalog.Sessions.AddRange([Session("paused", SessionLifecycle.Paused), Session("done", SessionLifecycle.Completed), Session("bad", SessionLifecycle.Failed), Session("live", SessionLifecycle.Running)]);
        _closed.UnionWith(["paused", "done"]);
        using var catalog = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = _closed.Contains });
        using var board = new KanbanBoardModel(catalog, _clock);

        board.Cards.Count.ShouldBe(4);
        var terminalColumns = new[] { KanbanColumn.ArchivedKey, KanbanColumn.KeyOf(RuntimeStatus.Completed), KanbanColumn.KeyOf(RuntimeStatus.Failed) };
        foreach (var card in board.Cards)
        {
            card.IsEnded.ShouldBe(terminalColumns.Contains(card.ColumnKey), card.Id);
        }

        board.CardOf("paused").ShouldNotBeNull().ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        board.CardOf("done").ShouldNotBeNull().ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        board.CardOf("live").ShouldNotBeNull().IsEnded.ShouldBeFalse();
    }

    /// <summary>A closed session with no engine attached lands in Archived even when its lifecycle is Running.</summary>
    [Fact]
    public void AClosedRunningSessionWithoutAnEngineLandsInArchived()
    {
        _catalog.Sessions.Add(Session("run", SessionLifecycle.Running));
        using var catalog = new SessionCatalogModel(_catalog, _clock, new SessionCatalogOptions { IsClosed = _closed.Contains });
        using var board = new KanbanBoardModel(catalog, _clock);
        board.CardOf("run").ShouldNotBeNull().ColumnKey.ShouldBe(KanbanColumn.KeyOf(RuntimeStatus.Running));

        _closed.Add("run");
        _catalog.RaiseChanged();

        var card = board.CardOf("run").ShouldNotBeNull();
        card.Status.ShouldBe(RuntimeStatus.Running);
        card.ColumnKey.ShouldBe(KanbanColumn.ArchivedKey);
        card.IsClosed.ShouldBeTrue();
        card.IsEnded.ShouldBeTrue();
        board.Columns.Single(column => string.Equals(column.Key, KanbanColumn.KeyOf(RuntimeStatus.Running), StringComparison.Ordinal)).Cards.ShouldBeEmpty();
    }

    private SessionSummary Session(string id, SessionLifecycle lifecycle)
    {
        var now = _clock.GetUtcNow();
        return Summary(id, lifecycle, now.AddMinutes(-10), now.AddMinutes(-1), null);
    }
}
