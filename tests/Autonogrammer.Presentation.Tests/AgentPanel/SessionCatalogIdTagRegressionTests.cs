using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.AgentPanel;
using Shouldly;
using Xunit;
using static Autonogrammer.Presentation.Tests.AgentPanel.AgentPanelTestData;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>
/// The Agents rail shows task and Graph identifiers up to the same 64-character cap the Context tab and stage rows use, and its
/// Graph tag reads the tail of the whole identifier rather than the part that survived the cap.
/// </summary>
public sealed class SessionCatalogIdTagRegressionTests
{
    private const int IdCap = 64;
    private const char Cut = '\u2026';
    private const string LongGraphId = "ut-mvp0bh59-2dd6ef4c-long-graph-identifier-with-extra-characters-0123456789";

    /// <summary>A Graph id over the old 40-character rail cap keeps its real tail in the tag.</summary>
    [Fact]
    public void TheGraphTagReadsTheTailOfAGraphIdLongerThanTheRailUsedToCap()
    {
        var clock = new ManualTimeProvider();
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        catalog.Sessions.Add(Summary("graph", SessionLifecycle.Running, now, now, graphId: LongGraphId));
        using var model = new SessionCatalogModel(catalog, clock);

        model.Rows.Single().TaskTag.ShouldBe("Graph 23456789");
    }

    /// <summary>A task id up to the shared cap is shown whole on the rail, not cut at 40 characters.</summary>
    [Fact]
    public void TheTaskTagShowsATaskIdUpToTheSharedCapWhole()
    {
        var clock = new ManualTimeProvider();
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        var taskId = new string('t', IdCap);
        catalog.Sessions.Add(Summary("task", SessionLifecycle.Running, now, now, taskId: taskId));
        using var model = new SessionCatalogModel(catalog, clock);

        model.Rows.Single().TaskTag.ShouldBe($"Task {taskId}");
        model.Rows.Single().TaskId.ShouldBe(taskId);
    }

    /// <summary>An id over the shared cap is cut at that cap, and the Graph tag still reads the tail of the whole id.</summary>
    [Fact]
    public void AnIdOverTheSharedCapIsCutAtTheCapWhileTheGraphTagKeepsItsTail()
    {
        var clock = new ManualTimeProvider();
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        var longTask = new string('t', IdCap + 10);
        var longGraph = new string('g', IdCap + 10) + "TAILTAIL";
        catalog.Sessions.Add(Summary("graph", SessionLifecycle.Running, now, now, graphId: longGraph));
        catalog.Sessions.Add(Summary("task", SessionLifecycle.Running, now, now.AddSeconds(-1), taskId: longTask, graphId: longGraph));
        using var model = new SessionCatalogModel(catalog, clock);

        var graphRow = model.Rows.Single(row => string.Equals(row.Id, "graph", StringComparison.Ordinal));
        graphRow.TaskTag.ShouldBe("Graph TAILTAIL");
        graphRow.GraphId.ShouldBe(new string('g', IdCap) + Cut);

        var taskRow = model.Rows.Single(row => string.Equals(row.Id, "task", StringComparison.Ordinal));
        taskRow.TaskTag.ShouldBe($"Task {new string('t', IdCap)}{Cut}");
    }
}
