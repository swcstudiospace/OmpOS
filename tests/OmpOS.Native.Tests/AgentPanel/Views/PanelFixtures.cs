using OmpOS.Contracts.Agents;
using OmpOS.Presentation.AgentPanel;
using OmpOS.Presentation.Composer;
using OmpOS.Presentation.Kanban;
using OmpOS.Presentation.Runs;
using OmpOS.Presentation.State;

namespace OmpOS.Native.Tests.AgentPanel.Views;

/// <summary>Builders for the presentation models the Agent Panel view tests draw. Every model is real; only its sources are scripted.</summary>
internal static class PanelFixtures
{
    /// <summary>The session the stage event helpers use unless told otherwise.</summary>
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

    /// <summary>Builds a catalog model over a fake registry. The caller disposes the model.</summary>
    internal static SessionCatalogModel Catalog(FakeSessionCatalog fake, ManualTimeProvider clock, SessionCatalogOptions? options = null) =>
        new(fake, clock, options);

    /// <summary>Builds the rail row of one session by reading it through a real <see cref="SessionCatalogModel"/>.</summary>
    internal static AgentRow Row(
        string id,
        SessionLifecycle lifecycle,
        ManualTimeProvider clock,
        string? title = null,
        SessionCatalogOptions? options = null,
        string? taskId = null,
        string? graphId = null)
    {
        var fake = new FakeSessionCatalog();
        var now = clock.GetUtcNow();
        fake.Sessions.Add(Summary(id, lifecycle, now.AddMinutes(-5), now.AddSeconds(-2), title, taskId, graphId));
        using var model = new SessionCatalogModel(fake, clock, options);
        return model.Rows.Single();
    }

    /// <summary>Builds the view model of one session, wired to scripted sources.</summary>
    internal static AgentSessionViewModel Session(
        AgentRow row,
        ManualTimeProvider clock,
        ScriptedRunInput? input = null,
        RunInputScheduler? scheduler = null,
        ITaskContextSource? context = null,
        Func<StageSource, CancellationToken, Task>? recheck = null,
        Func<CancellationToken, Task>? resume = null,
        Func<CancellationToken, Task>? reopen = null,
        Func<SteeringEntry, CancellationToken, Task>? recorder = null)
    {
        var composer = new ComposerViewModel(_ => Task.FromResult(true), () => Task.CompletedTask);
        return new AgentSessionViewModel(new AgentSessionOptions
        {
            Row = row,
            Session = new SessionViewModel(),
            Composer = composer,
            Scheduler = scheduler ?? new RunInputScheduler(input ?? new ScriptedRunInput()),
            Scope = Scope(row.Id),
            TimeProvider = clock,
            TaskContextSource = context,
            StageRecheck = recheck,
            Resume = resume,
            Reopen = reopen,
            SteeringRecorder = recorder,
        });
    }

    /// <summary>Builds a scheduler with a run already active, so steering and follow-ups are offered.</summary>
    internal static RunInputScheduler ActiveScheduler(ScriptedRunInput input)
    {
        var scheduler = new RunInputScheduler(input);
        scheduler.RunStarted();
        return scheduler;
    }

    /// <summary>Returns the live status option that makes a session wait for a tool approval.</summary>
    internal static SessionCatalogOptions WithLive(RuntimeStatus status) => new() { LiveStatus = _ => status };
}
