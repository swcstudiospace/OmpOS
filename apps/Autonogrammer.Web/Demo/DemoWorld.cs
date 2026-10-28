using System.Globalization;
using System.Text.Json;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Web.Demo;

/// <summary>
/// The in-memory state behind the Demo handler: the sessions of the workspace <c>acme-shop</c> and every operation of the API on them. A
/// mutating call changes this state, so the UI reacts the way it does against the real API. Scripted turns run on the injected
/// <see cref="TimeProvider"/>. Nothing leaves the browser.
/// </summary>
internal sealed class DemoWorld : IDisposable
{
    /// <summary>The owner of every Demo session.</summary>
    internal const string OwnerId = "demo-visitor";

    /// <summary>The name of the Demo workspace.</summary>
    internal const string WorkspaceName = "acme-shop";

    private static readonly (string Key, string Title, bool AcceptsDrop)[] Columns =
    [
        ("backlog", "Backlog", true),
        ("queued", "Queued", false),
        ("running", "Running", false),
        ("awaiting-input", "Awaiting input", false),
        ("needs-attention", "Needs attention", false),
        ("paused", "Paused", false),
        ("completed", "Completed", false),
        ("failed", "Failed", false),
        ("unreachable", "Unreachable", false),
        ("archived", "Archived", true),
    ];

    private readonly Dictionary<string, DemoSession> _sessions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CancellationTokenSource> _runs = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _lifetime = new();
    private bool _runningStarted;
    private int _created;

    /// <summary>Initializes a new instance of the <see cref="DemoWorld"/> class with the six Demo sessions.</summary>
    /// <param name="time">The clock of the scripted cadence and of every timestamp.</param>
    /// <param name="options">The timing of the scripts, or null for the defaults.</param>
    internal DemoWorld(TimeProvider time, DemoOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(time);
        Time = time;
        Options = options ?? new DemoOptions();
        DemoFixtures.Populate(this, time.GetUtcNow());
    }

    /// <summary>Gets the clock.</summary>
    internal TimeProvider Time { get; }

    /// <summary>Gets the timing of the scripts.</summary>
    internal DemoOptions Options { get; }

    /// <summary>Gets the user-level event bus (<c>GET /events</c>).</summary>
    internal DemoUserBus Bus { get; } = new();

    /// <summary>Gets the licence and source offer. The revision is <c>demo</c>.</summary>
    internal SourceInfo Source { get; } = new()
    {
        Licence = "AGPL-3.0-or-later",
        LicenceUrl = new Uri("https://www.gnu.org/licenses/agpl-3.0.html"),
        SourceUrl = new Uri("https://github.com/swcstudiospace/OmpOS"),
        Revision = "demo",
        Version = "0.15.0-demo",
    };

    /// <summary>Gets the workspace: the name and a plugin check that reports the four plugins connected.</summary>
    internal WorkspaceInfo Workspace { get; } = new()
    {
        Name = WorkspaceName,
        FolderLabel = WorkspaceName,
        PluginCheck = new PluginCheckResult { Status = PluginCheckStatus.Ok, Summary = "Ultrathink, Swarm, Substrate and GSD-Core are connected (Demo)." },
    };

    /// <summary>Adds a session. Used by the fixtures.</summary>
    /// <param name="session">The session.</param>
    internal void Add(DemoSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _sessions[session.Id] = session;
    }

    /// <summary>Lists the sessions, most recently active first.</summary>
    /// <returns>The catalogue rows.</returns>
    internal IReadOnlyList<SessionCatalogEntry> ListEntries() =>
        [.. _sessions.Values.OrderByDescending(static s => s.LastActivityAt).ThenBy(static s => s.Id, StringComparer.Ordinal).Select(static s => s.ToEntry())];

    /// <summary>Gets a session. Reading the "running" session for the first time starts its scripted turn.</summary>
    /// <param name="id">The session.</param>
    /// <returns>The stored session.</returns>
    internal Session Get(string id)
    {
        var session = Find(id);
        NoteRead(id);
        return session.ToSession();
    }

    /// <summary>Tells the world a client reads a session, so the scripted turn of the "running" session starts when its first reader arrives.</summary>
    /// <param name="id">The session.</param>
    internal void NoteRead(string id)
    {
        if (string.Equals(id, DemoFixtures.RunningId, StringComparison.Ordinal) && Options.AutoStartRunningSession)
        {
            _ = StartRunningSession();
        }
    }

    /// <summary>Starts the scripted turn of the "running" session, once.</summary>
    /// <returns>True when the turn started now.</returns>
    internal bool StartRunningSession()
    {
        if (_runningStarted || !_sessions.TryGetValue(DemoFixtures.RunningId, out var session) || session.RunInFlight)
        {
            return false;
        }

        _runningStarted = true;
        StartRun(session, DemoScripts.CheckoutFix());
        return true;
    }

    /// <summary>Finds a session.</summary>
    /// <param name="id">The session.</param>
    /// <returns>The session.</returns>
    /// <exception cref="DemoProblemException">There is no such session (404).</exception>
    internal DemoSession Find(string id) =>
        _sessions.TryGetValue(id, out var session) ? session : throw DemoProblemException.SessionNotFound(id);

    /// <summary>Creates a session (<c>POST /sessions</c>).</summary>
    /// <returns>The new session.</returns>
    internal Session Create()
    {
        var number = ++_created;
        var now = Time.GetUtcNow();
        var id = string.Create(CultureInfo.InvariantCulture, $"d3a{number:x5}-0000-4000-8000-{number:x12}");
        var session = new DemoSession { Id = id, CreatedAt = now, UpdatedAt = now, Status = SessionStatus.Idle };
        session.Stages.AddRange(DemoCatalogue.Stages(id, StageStatus.Pending, "Waiting for a prompt", now));
        session.Options.AddRange(DemoCatalogue.Options());
        _sessions[id] = session;
        PublishChanged(session, CatalogChangeKind.Created);
        return session.ToSession();
    }

    /// <summary>Accepts a prompt and starts the scripted reply (<c>POST /sessions/{id}/prompts</c>).</summary>
    /// <param name="id">The session.</param>
    /// <param name="prompt">The prompt. It is stored in the log, like the real API does, and never leaves the page.</param>
    /// <returns>The 202 body.</returns>
    internal DemoPromptAccepted SubmitPrompt(string id, string prompt)
    {
        var session = Find(id);
        if (session.Closed)
        {
            throw new DemoProblemException(409, "session_closed", "Session is closed.", "Reopen the session to send a prompt.");
        }

        if (session.Kind == CatalogSessionKind.Terminal)
        {
            throw new DemoProblemException(409, "session_not_promptable", "This session takes no prompts.", "An omp terminal is driven in its terminal.");
        }

        if (session.IsActive)
        {
            throw new DemoProblemException(409, null, "Prompt already in flight.", $"Session '{id}' is running.");
        }

        var now = Time.GetUtcNow();
        session.Messages.Add(new Message { Role = "user", Content = prompt, Timestamp = now });
        _ = session.Append(DemoEvents.Prompt(prompt), now);
        StartRun(session, DemoScripts.GenericReply());
        return new DemoPromptAccepted { SessionId = id, Status = "running", IntentKind = "prompt", DiagnosticCodes = [] };
    }

    /// <summary>Delivers a steering message to the running turn (<c>POST /sessions/{id}/steer</c>).</summary>
    /// <param name="id">The session.</param>
    /// <param name="text">The message. Only its length is stored.</param>
    /// <param name="mode">Send now, or queue.</param>
    /// <returns>How the message was delivered.</returns>
    internal SteerResponse Steer(string id, string text, SteerMode mode)
    {
        var session = Find(id);
        if (session.Closed)
        {
            throw new DemoProblemException(409, "session_closed", "Session is closed.", "Reopen the session to steer it.");
        }

        if (!session.IsActive)
        {
            throw new DemoProblemException(409, "steering_unavailable", "No turn is running.", "Steering needs a running turn. Send a prompt instead.");
        }

        var now = Time.GetUtcNow();
        if (mode == SteerMode.Queue)
        {
            _ = session.Append(DemoEvents.Steering(DemoEvents.FollowUp, text.Length), now);
            return new SteerResponse { Delivery = SteerDelivery.Queued };
        }

        _ = session.Append(DemoEvents.Steering(DemoEvents.Steer, text.Length), now);
        if (session.RunInFlight)
        {
            session.PendingSteers++;
            return new SteerResponse { Delivery = SteerDelivery.Delivered };
        }

        return new SteerResponse { Delivery = SteerDelivery.Held, Reason = "The agent waits for an approval, so the message is held until the turn continues." };
    }

    /// <summary>Lists the pending tool approvals (<c>GET /sessions/{id}/tool-approvals</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The pending requests.</returns>
    internal PendingToolApprovalsResponse Approvals(string id) =>
        new() { SessionId = id, Approvals = [.. Find(id).Approvals] };

    /// <summary>Decides a pending tool approval and lets the scripted turn continue (<c>POST /sessions/{id}/tool-approvals/{requestId}</c>).</summary>
    /// <param name="id">The session.</param>
    /// <param name="requestId">The request.</param>
    /// <param name="allow">True to allow.</param>
    internal void Decide(string id, string requestId, bool allow)
    {
        var session = Find(id);
        var pending = session.Approvals.FirstOrDefault(approval => string.Equals(approval.RequestId, requestId, StringComparison.Ordinal))
            ?? throw new DemoProblemException(409, null, "Tool approval not pending.", "That request is not waiting for a decision.");
        _ = session.Approvals.Remove(pending);
        _ = session.Append(DemoEvents.ApprovalDecided(requestId, allow), Time.GetUtcNow());
        StartRun(session, allow ? DemoScripts.AfterApproval() : DemoScripts.AfterDenial());
    }

    /// <summary>Gets the stage timeline (<c>GET /sessions/{id}/stages</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The stages.</returns>
    internal SessionStagesResponse Stages(string id)
    {
        var session = Find(id);
        return session.Stages.Count == 0
            ? new SessionStagesResponse { SessionId = id, Available = false, Reason = "An omp terminal runs its own engine and reports no stages.", Stages = [] }
            : new SessionStagesResponse { SessionId = id, Available = true, Stages = [.. session.Stages] };
    }

    /// <summary>Gets the task context (<c>GET /sessions/{id}/context</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The context, or an unavailable answer with a reason.</returns>
    internal SessionContextResponse Context(string id)
    {
        var session = Find(id);
        return session.Context is { } context
            ? new SessionContextResponse { SessionId = id, Available = true, Context = context }
            : new SessionContextResponse { SessionId = id, Available = false, Reason = "No task is bound to this Demo session." };
    }

    /// <summary>Gets the engine options (<c>GET /sessions/{id}/config-options</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The options.</returns>
    internal SessionConfigOptionsResponse ConfigOptions(string id)
    {
        var session = Find(id);
        return session.Kind == CatalogSessionKind.Terminal
            ? new SessionConfigOptionsResponse { SessionId = id, Available = false, Live = false, Reason = "An omp terminal owns its model, mode and thinking level.", Options = [] }
            : new SessionConfigOptionsResponse { SessionId = id, Available = true, Live = !session.Closed, Options = [.. session.Options] };
    }

    /// <summary>Changes one option (<c>PUT /sessions/{id}/config-options/{optionId}</c>). Like the real API it refuses while a turn runs.</summary>
    /// <param name="id">The session.</param>
    /// <param name="optionId">The option.</param>
    /// <param name="value">The new value.</param>
    /// <returns>The options after the change.</returns>
    internal SessionConfigOptionsResponse SetConfigOption(string id, string optionId, string value)
    {
        var session = Find(id);
        if (session.Kind == CatalogSessionKind.Terminal || session.Closed)
        {
            throw new DemoProblemException(409, "config_options_unavailable", "Options cannot be changed now.", "No engine session is open for this session.");
        }

        if (session.RunInFlight || session.IsActive)
        {
            throw new DemoProblemException(409, "config_option_busy", "The engine is busy.", "Options can change when the turn has ended.");
        }

        var index = session.Options.FindIndex(option => string.Equals(option.Id, optionId, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new DemoProblemException(404, "config_option_not_found", "Option not found.", "The engine offers no such option.");
        }

        if (!session.Options[index].Values.Any(candidate => string.Equals(candidate.Value, value, StringComparison.Ordinal)))
        {
            throw new DemoProblemException(400, "invalid_config_value", "Invalid option change.", "The engine offers no such value.");
        }

        session.Options[index] = session.Options[index] with { CurrentValue = value };
        _ = session.Append(DemoEvents.OptionChanged(optionId, value), Time.GetUtcNow());
        return ConfigOptions(id);
    }

    /// <summary>Lists the engine commands (<c>GET /sessions/{id}/commands</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The commands.</returns>
    internal SessionCommandsResponse Commands(string id)
    {
        var session = Find(id);
        return session.Kind == CatalogSessionKind.Terminal
            ? new SessionCommandsResponse { SessionId = id, Available = false, Live = false, Reason = "An omp terminal shows its own commands.", Commands = [] }
            : new SessionCommandsResponse { SessionId = id, Available = true, Live = !session.Closed, Commands = DemoCatalogue.Commands };
    }

    /// <summary>Lists the changed files (<c>GET /sessions/{id}/changes</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The changes.</returns>
    internal SessionChangesResponse Changes(string id) =>
        new() { SessionId = id, WorkspaceAvailable = false, Changes = [.. Find(id).Changes] };

    /// <summary>Accepts or rejects one change (<c>POST /sessions/{id}/changes/{path}/accept|reject</c>). The decision is recorded in memory; no file exists to write.</summary>
    /// <param name="id">The session.</param>
    /// <param name="path">The file path.</param>
    /// <param name="accept">True to accept.</param>
    /// <returns>The decision.</returns>
    internal ChangeDecisionResponse DecideChange(string id, string path, bool accept)
    {
        var session = Find(id);
        var index = session.Changes.FindIndex(change => string.Equals(change.Path, path, StringComparison.Ordinal));
        if (index < 0)
        {
            throw new DemoProblemException(404, null, "Change not found.", "The session did not change that file.");
        }

        var status = accept ? ChangeReviewStatus.Accepted : ChangeReviewStatus.Rejected;
        session.Changes[index] = session.Changes[index] with { Status = status };
        return new ChangeDecisionResponse { Path = path, Status = status, Applied = false, Reason = "Demo: the decision is recorded only; no file was written." };
    }

    /// <summary>Gets the board (<c>GET /board</c>).</summary>
    /// <returns>The ten columns in desktop order.</returns>
    internal BoardResponse Board()
    {
        var cards = _sessions.Values
            .OrderByDescending(static s => s.LastActivityAt)
            .Select(s => new BoardCard
            {
                Id = s.Id,
                Title = "Agent " + (s.Id.Length > 8 ? s.Id[..8] : s.Id),
                RowStatus = s.RowStatus,
                ColumnKey = ColumnOf(s.RowStatus),
                LastActivityAt = s.LastActivityAt,
                IsStale = false,
                Closed = s.Closed,
            })
            .ToList();
        return new BoardResponse
        {
            State = cards.Count == 0 ? BoardStateKind.Empty : BoardStateKind.Ready,
            Columns = [.. Columns.Select(column => new BoardColumn
            {
                Key = column.Key,
                Title = column.Title,
                AcceptsDrop = column.AcceptsDrop,
                Cards = [.. cards.Where(card => string.Equals(card.ColumnKey, column.Key, StringComparison.Ordinal))],
            })],
        };
    }

    /// <summary>Gets the card details (<c>GET /sessions/{id}/card-details</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The details, or an unavailable answer with a reason.</returns>
    internal CardDetailsResponse CardDetails(string id)
    {
        var session = Find(id);
        return session.Context is { } context
            ? new CardDetailsResponse { SessionId = id, Available = true, TaskTitle = context.Title, GraphId = context.GraphId, LessonsCount = context.Lessons.Count, LeaseHolder = session.LeaseHolder }
            : new CardDetailsResponse { SessionId = id, Available = false, Reason = "No task is bound to this Demo session." };
    }

    /// <summary>Closes a session (<c>POST /sessions/{id}/close</c>). A running turn is cancelled.</summary>
    /// <param name="id">The session.</param>
    /// <returns>The new row state.</returns>
    internal SessionLifecycleResponse Close(string id)
    {
        var session = Find(id);
        if (_runs.TryGetValue(id, out var run))
        {
            run.Cancel();
        }

        if (session.IsActive)
        {
            session.Approvals.Clear();
            session.RunInFlight = false;
            SetStatus(session, SessionStatus.Cancelled);
        }

        session.Closed = true;
        PublishChanged(session, CatalogChangeKind.Closed);
        return Lifecycle(session);
    }

    /// <summary>Reopens a closed session (<c>POST /sessions/{id}/reopen</c>).</summary>
    /// <param name="id">The session.</param>
    /// <returns>The new row state.</returns>
    internal SessionLifecycleResponse Reopen(string id)
    {
        var session = Find(id);
        if (!session.Closed)
        {
            throw new DemoProblemException(409, "session_not_closed", "Session is not closed.", "Only a closed session can be reopened.");
        }

        session.Closed = false;
        PublishChanged(session, CatalogChangeKind.Reopened);
        return Lifecycle(session);
    }

    /// <summary>Resumes a lost session (<c>POST /sessions/{id}/resume</c>). No Demo session is lost, so this always answers a conflict, as the real API does for a session that is not lost.</summary>
    /// <param name="id">The session.</param>
    /// <returns>Never returns normally.</returns>
    internal SessionLifecycleResponse Resume(string id)
    {
        var session = Find(id);
        throw session.Closed
            ? new DemoProblemException(409, "session_closed", "Session is closed.", "Reopen the session first.")
            : session.IsActive
                ? new DemoProblemException(409, "session_running", "Session is running.", "A running session is not lost.")
                : new DemoProblemException(409, "session_not_lost", "Session is not lost.", "Only a session whose run was lost can be resumed.");
    }

    /// <summary>Cancels every scripted turn. The world is not usable afterwards.</summary>
    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
    }

    private static string ColumnOf(CatalogRowStatus status) => status switch
    {
        CatalogRowStatus.Queued => "queued",
        CatalogRowStatus.Running => "running",
        CatalogRowStatus.WaitingForYou => "awaiting-input",
        CatalogRowStatus.NeedsApproval => "needs-attention",
        CatalogRowStatus.Paused => "paused",
        CatalogRowStatus.Completed => "completed",
        CatalogRowStatus.Failed => "failed",
        CatalogRowStatus.Lost => "unreachable",
        CatalogRowStatus.Closed => "archived",
        _ => "backlog",
    };

    private static SessionLifecycleResponse Lifecycle(DemoSession session) =>
        new() { Id = session.Id, Closed = session.Closed, RowStatus = session.RowStatus, Group = session.Group };

    private void StartRun(DemoSession session, IReadOnlyList<DemoStep> steps)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _runs[session.Id] = cts;
        session.RunInFlight = true;
        SetStatus(session, SessionStatus.Running);
        SetTurn(session, StageStatus.Running, "Working");
        _ = RunAsync(session, steps, cts);
    }

    private async Task RunAsync(DemoSession session, IReadOnlyList<DemoStep> steps, CancellationTokenSource cts)
    {
        var ended = false;
        try
        {
            foreach (var step in steps)
            {
                await Task.Delay(step.Typed ? Options.TypingDelay : Options.StepDelay, Time, cts.Token).ConfigureAwait(true);
                if (session.PendingSteers > 0)
                {
                    session.PendingSteers = 0;
                    foreach (var note in DemoScripts.SteerAcknowledgement())
                    {
                        await Task.Delay(Options.TypingDelay, Time, cts.Token).ConfigureAwait(true);
                        _ = session.Append(note.Payload, Time.GetUtcNow());
                    }
                }

                _ = session.Append(step.Payload, Time.GetUtcNow());
                step.Effect?.Invoke(session);
                if (DemoEvents.IsTerminal(step.Payload))
                {
                    ended = true;
                    session.RunInFlight = false;
                    var failed = step.Payload is ErrorPayload;
                    SetTurn(session, failed ? StageStatus.Failed : StageStatus.Succeeded, failed ? "Failed" : "Finished");
                    SetStatus(session, failed ? SessionStatus.Failed : SessionStatus.Completed);
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // The session was closed or the Demo was disposed: the status was already set by the caller.
        }
        finally
        {
            if (!ended)
            {
                session.RunInFlight = false;
            }

            _ = _runs.Remove(session.Id);
            cts.Dispose();
        }
    }

    private void SetStatus(DemoSession session, SessionStatus status)
    {
        session.Status = status;
        session.UpdatedAt = Time.GetUtcNow();
        var json = JsonSerializer.Serialize(
            new SessionStatusEvent
            {
                SessionId = session.Id,
                Status = status,
                RowStatus = session.RowStatus,
                Group = session.Group,
                LastActivityAt = session.LastActivityAt,
            },
            CatalogJsonContext.Default.SessionStatusEvent);
        Bus.Publish(CatalogEventKinds.SessionStatus, json);
    }

    private void SetTurn(DemoSession session, StageStatus status, string detail)
    {
        var stage = new StageEvent
        {
            SessionId = session.Id,
            Source = StageSource.Omp,
            StageId = "omp/turn",
            Title = "Agent turn",
            Status = status,
            Timestamp = Time.GetUtcNow(),
            Detail = detail,
        };
        var index = session.Stages.FindIndex(existing => string.Equals(existing.StageId, stage.StageId, StringComparison.Ordinal));
        if (index >= 0)
        {
            session.Stages[index] = stage;
        }
        else
        {
            session.Stages.Insert(0, stage);
        }

        _ = session.Append(DemoEvents.Stage(stage), stage.Timestamp);
        Bus.Publish(CatalogEventKinds.Stage, JsonSerializer.Serialize(stage, CatalogJsonContext.Default.StageEvent));
    }

    private void PublishChanged(DemoSession session, CatalogChangeKind kind) =>
        Bus.Publish(
            CatalogEventKinds.CatalogChanged,
            JsonSerializer.Serialize(
                new CatalogChangedEvent { SessionId = session.Id, Change = kind, RowStatus = session.RowStatus, Group = session.Group },
                CatalogJsonContext.Default.CatalogChangedEvent));
}
