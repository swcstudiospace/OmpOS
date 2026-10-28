using Autonogrammer.Contracts.Agents;
using Autonogrammer.Native.Agents;
using Autonogrammer.Presentation.Kanban;
using Shouldly;
using Xunit;
using static Autonogrammer.Native.Tests.AgentLoop.AgentLoopFixtures;

namespace Autonogrammer.Native.Tests.AgentLoop;

/// <summary>
/// The head's Kanban sources: the board the head hosts over the same sessions as the Agent Panel, the Substrate fields of a card that
/// are reported only when the Substrate gave them (never invented), and the timer that reads them again.
/// </summary>
public sealed class KanbanHeadTests
{
    /// <summary>The head hosts one board over the sessions of the panel: it loads first, ends loading when the registry is read, shows one card per chat and per terminal, and each card can steer.</summary>
    [Fact]
    public async Task TheHeadHostsABoardWithOneSteerableCardPerSession()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        head.Board.State.ShouldBe(BoardState.Loading);

        await head.InitializeAsync(ct).ConfigureAwait(true);

        head.Board.State.ShouldBe(BoardState.Empty);
        var chat = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        var terminal = await head.Host.CreateTerminalSessionAsync(ct).ConfigureAwait(true);
        head.Catalog.Refresh();
        head.Board.State.ShouldBe(BoardState.Ready);
        head.Board.Cards.Count.ShouldBe(2);
        head.Board.CardOf(chat).ShouldNotBeNull().Kind.ShouldBe(SessionKind.Chat);
        head.Board.CardOf(terminal).ShouldNotBeNull().Kind.ShouldBe(SessionKind.Terminal);
        head.Board.CardModelOf(chat).ShouldNotBeNull().CanSteer.ShouldBeTrue();
        head.Board.CardModelOf(terminal).ShouldNotBeNull().CanSteer.ShouldBeTrue();
    }

    /// <summary>A session whose Substrate is not active shows every Substrate field as unavailable: the board is told the source has nothing, never a guess.</summary>
    [Fact]
    public async Task WithoutASubstrateTheCardFieldsAreUnavailableAndNothingIsInvented()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        var head = AgentHead.Create(Options(tree, new ScriptedOmp()));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Catalog.Refresh();

        var details = await new HeadKanbanDetailsSource(head.Host).LoadAsync(id, ct).ConfigureAwait(true);
        await head.Board.RefreshDetailsAsync(ct).ConfigureAwait(true);

        details.ShouldBe(new KanbanCardDetails(null, null, null, null, Available: false));
        var card = head.Board.CardModelOf(id).ShouldNotBeNull();
        card.IsSubstrateAvailable.ShouldBeFalse();
        card.TaskText.ShouldContain("unavailable", Case.Sensitive);
        card.LeaseText.ShouldContain("unavailable", Case.Sensitive);
        (await new HeadKanbanDetailsSource(head.Host).LoadAsync("s-never-created", ct).ConfigureAwait(true)).ShouldBeNull();
    }

    /// <summary>
    /// The head reads the Substrate fields again on a 30 second timer owned by the head, and the timer ends with the head. The stub Substrate is reachable but holds
    /// no task, so the read is seen in the requests it received and the card then shows the Substrate as available with every field empty.
    /// </summary>
    [Fact]
    public async Task TheDetailsTimerReadsTheSubstrateAgainAndEndsWithTheHead()
    {
        var ct = TestContext.Current.CancellationToken;
        using var tree = new TempTree();
        tree.WriteReadyPlugins();
        using var substrate = new StubSubstrateHandler();
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero));
        var head = AgentHead.Create(Options(tree, new ScriptedOmp(), Substrate("http://127.0.0.1:7410"), substrate, clock: clock, detailsRefresh: AgentHead.DefaultDetailsRefreshInterval));
        await using var headLifetime = head.ConfigureAwait(true);
        await head.InitializeAsync(ct).ConfigureAwait(true);
        var id = await head.Host.CreateSessionAsync(ct).ConfigureAwait(true);
        head.Catalog.Refresh();
        var card = head.Board.CardModelOf(id).ShouldNotBeNull();
        card.Details.ShouldBeNull("nothing has read the card's Substrate fields yet");

        // The catalog's own refresh timer has the same period and is created first, in InitializeAsync; the head's timer is created last.
        var timer = clock.Timers.Last(static candidate => candidate.Period == AgentHead.DefaultDetailsRefreshInterval);
        var before = substrate.Requests.Count;

        timer.Fire();
        await EventuallyAsync(() => substrate.Requests.Count > before, "the Substrate to be read after the timer fired", ct).ConfigureAwait(true);

        await EventuallyAsync(() => card.Details is not null, "the read to reach the card", ct).ConfigureAwait(true);
        card.Details.ShouldBe(new KanbanCardDetails(null, null, null, null, Available: true), "the stub Substrate names no task, graph, lessons or lease, so no field is invented");
        await head.DisposeAsync().ConfigureAwait(true);
        timer.Disposed.ShouldBeTrue();
    }

    /// <summary>A card shows a task title only when the Substrate named a task, the lessons count only when the lessons were read, a graph only when there is one, and never a lease holder: the task context carries none.</summary>
    [Fact]
    public void TheMappingReportsOnlyWhatTheSubstrateGave()
    {
        var lessons = new[] { Note("l-1"), Note("l-2") };

        var named = HeadKanbanDetailsSource.Map(Context("Wire the card", "task-1", "ut-mvp0bh59-2dd6ef4c", lessons, []));
        var phaseOnly = HeadKanbanDetailsSource.Map(Context("Phase 3: Foo", null, null, [], []));
        var lessonsUnread = HeadKanbanDetailsSource.Map(Context("Wire the card", "task-1", null, [], ["The Substrate lessons could not be read."]));
        var unreachable = HeadKanbanDetailsSource.Map(Context("Untitled task", null, null, [], ["Substrate context is unavailable: the reader failed (HttpRequestException)."]));

        named.ShouldBe(new KanbanCardDetails("Wire the card", "ut-mvp0bh59-2dd6ef4c", 2, null, Available: true));
        phaseOnly.ShouldBe(new KanbanCardDetails(null, null, 0, null, Available: true));
        lessonsUnread.ShouldBe(new KanbanCardDetails("Wire the card", null, null, null, Available: true));
        unreachable.ShouldBe(new KanbanCardDetails(null, null, null, null, Available: false));
    }

    private const string Secret = "tok-7c1e-never-print-me";

    private static Func<string, string?> Substrate(string address) => name => name switch
    {
        AgentLoopEnvironment.SubstrateUrlVariable => address,
        AgentLoopEnvironment.DefaultSubstrateTokenVariable => Secret,
        _ => null,
    };

    private static ContextNote Note(string id) => new() { Id = id, Text = "a lesson", Origin = "substrate" };

    private static TaskContext Context(string title, string? taskId, string? graphId, IReadOnlyList<ContextNote> lessons, IReadOnlyList<string> warnings) => new()
    {
        Title = title,
        TaskId = taskId,
        GraphId = graphId,
        SharedMemory = [],
        Lessons = lessons,
        Warnings = warnings,
    };
}
