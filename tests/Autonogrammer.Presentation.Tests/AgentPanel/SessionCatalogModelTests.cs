using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// The Agents rail model: rows read from the catalog, grouped and sorted as the spec says, relative time from an injected clock,
/// stale and long-running flags, filter and selection. The model never creates or removes a session.
/// </summary>
public sealed class SessionCatalogModelTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly FakeSessionCatalog _catalog = new();

    /// <summary>Rows come in group order; inside Needs you the oldest waiting is first, inside the others the most recent is first.</summary>
    [Fact]
    public void RowsAreGroupedAndSortedAsTheSpecSays()
    {
        var live = new Dictionary<string, RuntimeStatus>(StringComparer.Ordinal)
        {
            ["approval-old"] = RuntimeStatus.NeedsAttention,
            ["waiting-new"] = RuntimeStatus.AwaitingInput,
        };
        _catalog.Sessions.AddRange(
        [
            Session("running-old", SessionLifecycle.Running, minutesAgo: 1),
            Session("failed", SessionLifecycle.Failed, minutesAgo: 30),
            Session("completed", SessionLifecycle.Completed, minutesAgo: 20),
            Session("lost", SessionLifecycle.Lost, minutesAgo: 9),
            Session("ready", SessionLifecycle.Created, minutesAgo: 4),
            Session("running-new", SessionLifecycle.Running, minutesAgo: 0),
            Session("approval-old", SessionLifecycle.Running, minutesAgo: 10),
            Session("waiting-new", SessionLifecycle.Running, minutesAgo: 2),
        ]);

        using var model = NewModel(new SessionCatalogOptions { LiveStatus = id => live.TryGetValue(id, out var status) ? status : null });

        model.Rows.Select(row => row.Id).ShouldBe(["approval-old", "waiting-new", "running-new", "running-old", "lost", "ready", "completed", "failed"]);
        model.Rows.Where(row => row.IsGroupStart).Select(row => row.GroupHeader).ShouldBe(["Needs you", "Running", "Lost", "Idle", "Finished"]);
        model.Rows.Where(row => row.IsGroupStart).Select(row => row.Id).ShouldBe(["approval-old", "running-new", "lost", "ready", "completed"]);
        model.TotalCount.ShouldBe(8);
    }

    /// <summary>Each row carries the status words, glyph and Kanban card state the mapping gives it.</summary>
    [Fact]
    public void RowsCarryStatusWordsGlyphAndCardState()
    {
        _catalog.Sessions.Add(Session("done", SessionLifecycle.Completed, minutesAgo: 3));

        using var model = NewModel();

        var row = model.Rows.Single();
        row.Status.ShouldBe(AgentRowStatus.Completed);
        row.StatusText.ShouldBe("Completed");
        row.IconKey.ShouldBe(StatusIcon.Check);
        row.Card.ShouldBe(new KanbanCardState(RuntimeStatus.Completed, IsStale: false));
        row.RuntimeStatus.ShouldBe(RuntimeStatus.Completed);
        row.Group.ShouldBe(AgentRowGroup.Finished);
    }

    /// <summary>The stale flag is independent of the status: only a Running row that has been quiet is stale, whatever its lifecycle history.</summary>
    [Theory]
    [InlineData(SessionLifecycle.Created, RuntimeStatus.Queued, false)]
    [InlineData(SessionLifecycle.Running, RuntimeStatus.Running, true)]
    [InlineData(SessionLifecycle.Paused, RuntimeStatus.Paused, false)]
    [InlineData(SessionLifecycle.Completed, RuntimeStatus.Completed, false)]
    [InlineData(SessionLifecycle.Failed, RuntimeStatus.Failed, false)]
    [InlineData(SessionLifecycle.Lost, RuntimeStatus.Unreachable, false)]
    public void EachLifecycleMapsToItsRuntimeStatusAndOnlyRunningGoesStale(SessionLifecycle lifecycle, RuntimeStatus expectedStatus, bool staleWhenQuiet)
    {
        _catalog.Sessions.Add(Summary("fresh", lifecycle, _clock.GetUtcNow().AddHours(-3), _clock.GetUtcNow().AddSeconds(-5)));
        _catalog.Sessions.Add(Summary("quiet", lifecycle, _clock.GetUtcNow().AddHours(-3), _clock.GetUtcNow().AddMinutes(-10)));

        using var model = NewModel();

        var fresh = model.Rows.Single(row => string.Equals(row.Id, "fresh", StringComparison.Ordinal));
        var quiet = model.Rows.Single(row => string.Equals(row.Id, "quiet", StringComparison.Ordinal));
        fresh.Card.ShouldBe(new KanbanCardState(expectedStatus, IsStale: false));
        quiet.Card.ShouldBe(new KanbanCardState(expectedStatus, IsStale: staleWhenQuiet));
        quiet.IsStale.ShouldBe(staleWhenQuiet);
    }

    /// <summary>A session is stale only when its silence is longer than 45 seconds, and the quiet text replaces nothing but says how long.</summary>
    [Fact]
    public void StalenessStartsJustAfterFortyFiveSeconds()
    {
        _catalog.Sessions.Add(Summary("edge", SessionLifecycle.Running, _clock.GetUtcNow().AddHours(-1).AddMinutes(-1), _clock.GetUtcNow().AddSeconds(-45)));
        _catalog.Sessions.Add(Summary("over", SessionLifecycle.Running, _clock.GetUtcNow().AddMinutes(-2), _clock.GetUtcNow().AddSeconds(-46)));
        _catalog.Sessions.Add(Summary("long", SessionLifecycle.Running, _clock.GetUtcNow().AddMinutes(-20), _clock.GetUtcNow().AddMinutes(-5)));

        using var model = NewModel();

        model.Rows.Single(row => string.Equals(row.Id, "edge", StringComparison.Ordinal)).IsStale.ShouldBeFalse();
        model.Rows.Single(row => string.Equals(row.Id, "edge", StringComparison.Ordinal)).QuietText.ShouldBeNull();
        var over = model.Rows.Single(row => string.Equals(row.Id, "over", StringComparison.Ordinal));
        over.IsStale.ShouldBeTrue();
        over.QuietText.ShouldBe("Quiet for 1 min");
        over.LastActivityText.ShouldBe("46 s ago");
        var longQuiet = model.Rows.Single(row => string.Equals(row.Id, "long", StringComparison.Ordinal));
        longQuiet.QuietText.ShouldBe("Quiet for 5 min");
        longQuiet.AccessibleName.ShouldContain(", quiet for 5 min");
    }

    /// <summary>A Running session is long-running only when it was created more than one hour ago.</summary>
    [Fact]
    public void LongRunningMeansRunningAndCreatedMoreThanAnHourAgo()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("exactly", SessionLifecycle.Running, now.AddHours(-1), now));
        _catalog.Sessions.Add(Summary("over", SessionLifecycle.Running, now.AddHours(-1).AddSeconds(-1), now));
        _catalog.Sessions.Add(Summary("young", SessionLifecycle.Running, now.AddMinutes(-59), now));
        _catalog.Sessions.Add(Summary("old-done", SessionLifecycle.Completed, now.AddHours(-9), now));

        using var model = NewModel();

        model.Rows.Where(row => row.IsLongRunning).Select(row => row.Id).ShouldBe(["over"]);
        model.Rows.Single(row => string.Equals(row.Id, "over", StringComparison.Ordinal)).AccessibleName.ShouldContain(", long-running");
    }

    /// <summary>Lost covers a Lost lifecycle and a live unreachable status.</summary>
    [Fact]
    public void LostCoversTheLifecycleAndALiveUnreachableStatus()
    {
        _catalog.Sessions.Add(Session("persisted", SessionLifecycle.Lost, minutesAgo: 3));
        _catalog.Sessions.Add(Session("live", SessionLifecycle.Running, minutesAgo: 2));
        _catalog.Sessions.Add(Session("fine", SessionLifecycle.Running, minutesAgo: 1));

        using var model = NewModel(new SessionCatalogOptions { LiveStatus = id => string.Equals(id, "live", StringComparison.Ordinal) ? RuntimeStatus.Unreachable : null });

        model.Rows.Where(row => row.IsLost).Select(row => row.Id).ShouldBe(["live", "persisted"]);
    }

    /// <summary>The operator's closed flag turns a paused or completed session into a Closed row.</summary>
    [Fact]
    public void AClosedSessionReadsAsClosed()
    {
        _catalog.Sessions.Add(Session("closed", SessionLifecycle.Paused, minutesAgo: 3));

        using var model = NewModel(new SessionCatalogOptions { IsClosed = id => string.Equals(id, "closed", StringComparison.Ordinal) });

        var row = model.Rows.Single();
        row.Status.ShouldBe(AgentRowStatus.Closed);
        row.StatusText.ShouldBe("Closed");
        row.Group.ShouldBe(AgentRowGroup.Idle);
    }

    /// <summary>The task tag is the task id, else the last eight characters of the graph id, else nothing.</summary>
    [Fact]
    public void TheTaskTagPrefersTheTaskThenTheGraph()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("with-task", SessionLifecycle.Running, now, now, taskId: "T-12", graphId: "ut-mvp0bh59-2dd6ef4c"));
        _catalog.Sessions.Add(Summary("with-graph", SessionLifecycle.Running, now, now.AddSeconds(-1), graphId: "ut-mvp0bh59-2dd6ef4c"));
        _catalog.Sessions.Add(Summary("bare", SessionLifecycle.Running, now, now.AddSeconds(-2)));

        using var model = NewModel();

        model.Rows.Single(row => string.Equals(row.Id, "with-task", StringComparison.Ordinal)).TaskTag.ShouldBe("Task T-12");
        model.Rows.Single(row => string.Equals(row.Id, "with-graph", StringComparison.Ordinal)).TaskTag.ShouldBe("Graph 2dd6ef4c");
        model.Rows.Single(row => string.Equals(row.Id, "bare", StringComparison.Ordinal)).TaskTag.ShouldBeNull();
        model.Rows.Single(row => string.Equals(row.Id, "with-task", StringComparison.Ordinal)).AccessibleName.ShouldEndWith(", Task T-12");
    }

    /// <summary>The accessible name reads title, status and last activity.</summary>
    [Fact]
    public void TheAccessibleNameReadsTitleStatusAndActivity()
    {
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Completed, _clock.GetUtcNow().AddHours(-4), _clock.GetUtcNow().AddMinutes(-5), title: "Fix the build"));

        using var model = NewModel();

        model.Rows.Single().AccessibleName.ShouldBe("Fix the build, Completed, 5 min ago");
    }

    /// <summary>A hostile or blank title is made plain; a blank one reads Untitled agent; a repeated id keeps the first row.</summary>
    [Fact]
    public void TitlesArePlainAndIdsAreUnique()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("blank", SessionLifecycle.Created, now, now, title: "   "));
        _catalog.Sessions.Add(Summary("evil", SessionLifecycle.Created, now, now.AddSeconds(-1), title: "\u202Etitle\u0007"));
        _catalog.Sessions.Add(Summary("evil", SessionLifecycle.Failed, now, now.AddSeconds(-2), title: "duplicate"));

        using var model = NewModel();

        model.Rows.Count.ShouldBe(2);
        model.Rows.Single(row => string.Equals(row.Id, "blank", StringComparison.Ordinal)).Title.ShouldBe("Untitled agent");
        model.Rows.Single(row => string.Equals(row.Id, "evil", StringComparison.Ordinal)).Title.ShouldBe("title");
        model.Rows.Single(row => string.Equals(row.Id, "evil", StringComparison.Ordinal)).Status.ShouldBe(AgentRowStatus.Ready);
    }

    /// <summary>An unknown lifecycle reads Status unknown in the Idle group and never throws.</summary>
    [Fact]
    public void AnUnknownLifecycleReadsAsStatusUnknown()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("future", (SessionLifecycle)42, now, now));

        using var model = NewModel();

        var row = model.Rows.Single();
        row.StatusText.ShouldBe("Status unknown");
        row.IconKey.ShouldBe(StatusIcon.Ring);
        row.Group.ShouldBe(AgentRowGroup.Idle);
        row.Card.ShouldBeNull();
        row.RuntimeStatus.ShouldBeNull();
    }

    /// <summary>A catalog change replaces the rows, and with a dispatcher nothing changes until the dispatcher runs the action.</summary>
    [Fact]
    public void ACatalogChangeIsRoutedThroughTheDispatcher()
    {
        var pending = new List<Action>();
        using var model = NewModel(new SessionCatalogOptions { Dispatch = pending.Add });
        var announced = PropertyChangeRecorder.Record(model);

        _catalog.Put(Session("new", SessionLifecycle.Running, minutesAgo: 0));

        model.Rows.ShouldBeEmpty();
        pending.Count.ShouldBe(1);

        pending[0]();

        model.Rows.Select(row => row.Id).ShouldBe(["new"]);
        announced.ShouldAnnounce(nameof(SessionCatalogModel.Rows));
        announced.ShouldAnnounce(nameof(SessionCatalogModel.Sessions));
        announced.ShouldAnnounce(nameof(SessionCatalogModel.TotalCount));
    }

    /// <summary>Without a dispatcher a change is applied at once, and a removal removes the row.</summary>
    [Fact]
    public void ACatalogChangeUpdatesTheRowsAtOnce()
    {
        _catalog.Sessions.Add(Session("one", SessionLifecycle.Running, minutesAgo: 1));
        using var model = NewModel();

        _catalog.Put(Session("two", SessionLifecycle.Running, minutesAgo: 0));
        model.Rows.Count.ShouldBe(2);

        _catalog.Remove("one");
        model.Rows.Select(row => row.Id).ShouldBe(["two"]);
    }

    /// <summary>A catalog that throws leaves the last rows in place and reports a plain reason; the next good read clears it.</summary>
    [Fact]
    public void AFailingCatalogKeepsTheLastRowsAndReportsWhy()
    {
        _catalog.Sessions.Add(Session("one", SessionLifecycle.Running, minutesAgo: 1));
        using var model = NewModel();
        _catalog.Failure = new IOException("disk\u0007 unreadable");

        model.Refresh().ShouldBeFalse();

        model.LoadError.ShouldBe("disk unreadable");
        model.Rows.Select(row => row.Id).ShouldBe(["one"]);

        _catalog.Failure = null;
        model.Refresh().ShouldBeTrue();
        model.LoadError.ShouldBeNull();
    }

    /// <summary>The filter matches title, task, graph and status text, ignoring case, and group headers follow the filtered rows.</summary>
    [Fact]
    public void TheFilterMatchesAndRegroups()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Running, now, now, title: "Fix the build", taskId: "T-1"));
        _catalog.Sessions.Add(Summary("b", SessionLifecycle.Completed, now, now.AddMinutes(-1), title: "Write docs", graphId: "ut-abc-12345678"));
        _catalog.Sessions.Add(Summary("c", SessionLifecycle.Completed, now, now.AddMinutes(-2), title: "Refactor"));
        using var model = NewModel();

        model.Filter = "BUILD";
        model.Rows.Select(row => row.Id).ShouldBe(["a"]);

        model.Filter = "t-1";
        model.Rows.Select(row => row.Id).ShouldBe(["a"]);

        model.Filter = "12345678";
        model.Rows.Select(row => row.Id).ShouldBe(["b"]);

        model.Filter = "completed";
        model.Rows.Select(row => row.Id).ShouldBe(["b", "c"]);
        model.Rows.Count(row => row.IsGroupStart).ShouldBe(1);

        model.Filter = string.Empty;
        model.Rows.Count.ShouldBe(3);
        model.Sessions.Count.ShouldBe(3);
    }

    /// <summary>A filter that hides everything gives the no-match copy with the count of hidden agents.</summary>
    [Fact]
    public void AFilterThatMatchesNothingGivesTheNoMatchCopy()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Running, now, now));
        _catalog.Sessions.Add(Summary("b", SessionLifecycle.Running, now, now));
        _catalog.Sessions.Add(Summary("c", SessionLifecycle.Running, now, now));
        using var model = NewModel();

        model.Filter = "zzz";

        model.HasNoMatches.ShouldBeTrue();
        model.NoMatchHeading.ShouldBe("No agent matches \"zzz\".");
        model.NoMatchBody.ShouldBe("Clear the filter to see all 3 agents.");

        _catalog.Remove("b");
        _catalog.Remove("c");
        model.NoMatchBody.ShouldBe("Clear the filter to see your 1 agent.");

        model.Filter = string.Empty;
        model.HasNoMatches.ShouldBeFalse();
    }

    /// <summary>A filter longer than its cap is cut, and hostile characters are dropped.</summary>
    [Fact]
    public void TheFilterIsPlainAndCapped()
    {
        using var model = NewModel();

        model.Filter = "\u202E" + new string('x', 300);

        model.Filter.Length.ShouldBe(SessionCatalogModel.MaxFilterChars + 1);
        model.Filter.ShouldNotContain("\u202E");
    }

    /// <summary>Selecting a session raises the event once with its id; selecting it again, or an unknown id, changes nothing.</summary>
    [Fact]
    public void SelectionRaisesTheEventOnlyWhenItChanges()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        _catalog.Sessions.Add(Session("b", SessionLifecycle.Running, minutesAgo: 2));
        using var model = NewModel();
        var selected = new List<string?>();
        model.SelectionChanged += (_, args) => selected.Add(args.SessionId);

        model.Select("a").ShouldBeTrue();
        model.Select("a").ShouldBeTrue();
        model.Select("missing").ShouldBeFalse();

        model.SelectedId.ShouldBe("a");
        model.Selected!.Id.ShouldBe("a");
        selected.ShouldBe(["a"]);

        model.Select("b").ShouldBeTrue();
        model.Select(null).ShouldBeTrue();

        selected.ShouldBe(["a", "b", null]);
        model.Selected.ShouldBeNull();
    }

    /// <summary>A selected session that the filter hides stays selected.</summary>
    [Fact]
    public void AFilteredOutSelectionStaysSelected()
    {
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Running, _clock.GetUtcNow(), _clock.GetUtcNow(), title: "Alpha"));
        _catalog.Sessions.Add(Summary("b", SessionLifecycle.Running, _clock.GetUtcNow(), _clock.GetUtcNow().AddSeconds(-1), title: "Beta"));
        using var model = NewModel();
        model.Select("a");

        model.Filter = "beta";

        model.Rows.Select(row => row.Id).ShouldBe(["b"]);
        model.SelectedId.ShouldBe("a");
        model.Selected!.Title.ShouldBe("Alpha");
    }

    /// <summary>A selected session that leaves the catalog clears the selection and says so.</summary>
    [Fact]
    public void ARemovedSelectionClears()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        using var model = NewModel();
        model.Select("a");
        var selected = new List<string?>();
        model.SelectionChanged += (_, args) => selected.Add(args.SessionId);

        _catalog.Remove("a");

        model.SelectedId.ShouldBeNull();
        selected.ShouldBe([null]);
    }

    /// <summary>The model never creates, changes or removes a session of the catalog.</summary>
    [Fact]
    public void TheModelNeverChangesTheCatalog()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        var before = _catalog.Sessions.ToList();
        using var model = NewModel();

        model.Select("a");
        model.Select("missing");
        model.Filter = "x";
        model.Refresh();
        model.RefreshTimes();
        model.SelectCommand.Execute("a");
        model.CloseSelectedCommand.Execute(null);

        _catalog.Sessions.ShouldBe(before);
    }

    /// <summary>The select command takes an id or a row; the close command asks the owner of the engines and is disabled without a selection.</summary>
    [Fact]
    public void CommandsSelectAndRequestClose()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        _catalog.Sessions.Add(Session("b", SessionLifecycle.Running, minutesAgo: 2));
        using var model = NewModel();
        var closed = new List<string?>();
        model.CloseRequested += (_, args) => closed.Add(args.SessionId);
        model.CloseSelectedCommand.CanExecute(null).ShouldBeFalse();

        model.SelectCommand.Execute("b");
        model.SelectedId.ShouldBe("b");
        model.SelectCommand.Execute(model.Rows.Single(row => string.Equals(row.Id, "a", StringComparison.Ordinal)));
        model.SelectedId.ShouldBe("a");

        model.CloseSelectedCommand.CanExecute(null).ShouldBeTrue();
        model.CloseSelectedCommand.Execute(null);

        closed.ShouldBe(["a"]);
    }

    /// <summary>Relative times follow the clock when the model refreshes them, and the periodic refresh runs every 30 seconds until stopped.</summary>
    [Fact]
    public void RelativeTimesFollowTheClockAndThePeriodicRefresh()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Completed, now, now));
        using var model = NewModel();
        model.Rows.Single().LastActivityText.ShouldBe("just now");

        model.StartRefreshing();
        _clock.ActiveTimerCount.ShouldBe(1);
        _clock.Advance(TimeSpan.FromSeconds(29));
        model.Rows.Single().LastActivityText.ShouldBe("just now");

        _clock.Advance(TimeSpan.FromSeconds(1));
        model.Rows.Single().LastActivityText.ShouldBe("30 s ago");

        _clock.Advance(TimeSpan.FromSeconds(30));
        model.Rows.Single().LastActivityText.ShouldBe("1 min ago");

        model.StopRefreshing();
        _clock.ActiveTimerCount.ShouldBe(0);
        _clock.Advance(TimeSpan.FromMinutes(10));
        model.Rows.Single().LastActivityText.ShouldBe("1 min ago");
    }

    /// <summary>A time-only refresh reads no snapshot and announces nothing when no text changed.</summary>
    [Fact]
    public void ATimeRefreshThatChangesNothingIsSilent()
    {
        var now = _clock.GetUtcNow();
        _catalog.Sessions.Add(Summary("a", SessionLifecycle.Completed, now, now));
        using var model = NewModel();
        var snapshots = _catalog.SnapshotCount;
        var announced = PropertyChangeRecorder.Record(model);

        model.RefreshTimes();

        _catalog.SnapshotCount.ShouldBe(snapshots);
        announced.ShouldBeEmpty();
    }

    /// <summary>After disposal the model ignores the catalog and stops its timer.</summary>
    [Fact]
    public void DisposeUnsubscribesAndStopsTheTimer()
    {
        _catalog.Sessions.Add(Session("a", SessionLifecycle.Running, minutesAgo: 1));
        var model = NewModel();
        model.StartRefreshing();

        model.Dispose();
        var snapshots = _catalog.SnapshotCount;
        _catalog.Put(Session("b", SessionLifecycle.Running, minutesAgo: 0));

        _catalog.SnapshotCount.ShouldBe(snapshots);
        _clock.ActiveTimerCount.ShouldBe(0);
        model.Rows.Select(row => row.Id).ShouldBe(["a"]);
    }

    private SessionCatalogModel NewModel(SessionCatalogOptions? options = null) => new(_catalog, _clock, options);

    private SessionSummary Session(string id, SessionLifecycle lifecycle, int minutesAgo)
    {
        var now = _clock.GetUtcNow();
        return Summary(id, lifecycle, now.AddMinutes(-minutesAgo - 5), now.AddMinutes(-minutesAgo));
    }
}
