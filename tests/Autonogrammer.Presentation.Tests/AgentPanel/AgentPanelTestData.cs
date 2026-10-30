using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Presentation.AgentPanel;

namespace Autonogrammer.Presentation.Tests.AgentPanel;

/// <summary>Builders for the contract records the Agent Panel tests feed to the models.</summary>
internal static class AgentPanelTestData
{
    /// <summary>The session every stage event helper uses unless told otherwise.</summary>
    internal const string SessionId = "session-1";

    /// <summary>Builds a session summary.</summary>
    internal static SessionSummary Summary(
        string id,
        SessionLifecycle lifecycle,
        DateTimeOffset created,
        DateTimeOffset lastActivity,
        string? title = null,
        string? taskId = null,
        string? graphId = null) => new()
        {
            Id = id,
            Title = title ?? $"Agent {id}",
            Lifecycle = lifecycle,
            CreatedAt = created,
            LastActivity = lastActivity,
            TaskId = taskId,
            GraphId = graphId,
        };

    /// <summary>Builds an observation scope for a session.</summary>
    internal static StageObservationScope Scope(string sessionId = SessionId) => new()
    {
        SessionId = sessionId,
        WorkspaceRoot = "/workspace",
        SessionStateDirectory = "/state/" + sessionId,
    };

    /// <summary>Builds a stage event.</summary>
    internal static StageEvent Stage(
        StageSource source,
        string stageId,
        StageStatus status,
        DateTimeOffset at,
        string? title = null,
        string? detail = null,
        string? parent = null,
        string? graphId = null,
        string sessionId = SessionId) => new()
        {
            SessionId = sessionId,
            Source = source,
            StageId = stageId,
            Title = title ?? stageId,
            Status = status,
            Timestamp = at,
            Detail = detail,
            ParentStageId = parent,
            GraphId = graphId,
        };

    /// <summary>Builds a task context with no warnings unless given.</summary>
    internal static TaskContext Context(
        string title = "Ship the panel",
        string? taskId = "task-7",
        IReadOnlyList<ContextNote>? memory = null,
        IReadOnlyList<ContextNote>? lessons = null,
        IReadOnlyList<string>? warnings = null,
        GsdPhaseInfo? phase = null,
        string? kanban = "In progress") => new()
        {
            Title = title,
            TaskId = taskId,
            KanbanStatus = kanban,
            GraphId = "ut-mvp0bh59-2dd6ef4c",
            Phase = phase,
            SharedMemory = memory ?? [],
            Lessons = lessons ?? [],
            Warnings = warnings ?? [],
        };

    /// <summary>Builds a context note.</summary>
    internal static ContextNote Note(string id, string text, string origin = "project", DateTimeOffset? recordedAt = null) => new()
    {
        Id = id,
        Text = text,
        Origin = origin,
        RecordedAt = recordedAt,
    };

    /// <summary>Builds the rail row of one session by reading it through a real <see cref="SessionCatalogModel"/>.</summary>
    internal static AgentRow Row(
        string id,
        SessionLifecycle lifecycle,
        TimeProvider clock,
        string? title = null,
        SessionCatalogOptions? options = null)
    {
        var catalog = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        catalog.Sessions.Add(Summary(id, lifecycle, now.AddMinutes(-5), now.AddSeconds(-2), title));
        using var model = new SessionCatalogModel(catalog, clock, options);
        return model.Rows.Single();
    }

    /// <summary>Builds a stream event of the test session.</summary>
    internal static StreamEvent StreamOf(StreamEventPayload payload) => new()
    {
        Seq = 1,
        SessionId = SessionId,
        CorrelationId = SessionId + "/turn-1",
        Timestamp = new DateTimeOffset(2026, 10, 26, 9, 0, 0, TimeSpan.Zero),
        Payload = payload,
    };
}
