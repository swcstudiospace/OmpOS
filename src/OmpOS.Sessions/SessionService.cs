using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;
using OmpOS.Harness.Abstractions;

namespace OmpOS.Sessions;

/// <summary>
/// Session lifecycle rules: one active prompt per session (SESS-02), a steer/follow-up queueing policy, and
/// guarded completion. Every mutation of a session runs under that session's stripe, so the check-and-set
/// in <see cref="SubmitPromptAsync"/> is atomic even under concurrent submissions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Prompt admission.</b> <see cref="SubmitPromptAsync"/> is rejected with <see cref="ErrorCode.PromptInFlight"/>
/// (carried by <see cref="SessionException"/>) while the session is <see cref="SessionStatus.Running"/> or
/// <see cref="SessionStatus.AwaitingToolApproval"/>. A rejected submission is never queued.
/// </para>
/// <para>
/// <b>Tool approval lifecycle.</b> A running prompt pauses for a tool decision via
/// <see cref="RequestToolApprovalAsync"/>, which moves the session from <see cref="SessionStatus.Running"/>
/// to <see cref="SessionStatus.AwaitingToolApproval"/> and registers the request under its identifier.
/// <see cref="ResolveToolApprovalAsync"/> is the single entry point for a decision; it moves the session back to
/// <see cref="SessionStatus.Running"/> in both outcomes. An approval may allow only a tool the request's ceiling permits
/// (by default none), and a request that no decision reaches within the wait timeout is denied by
/// <see cref="WaitForDecisionAsync"/>. There is no direct transition from
/// <see cref="SessionStatus.AwaitingToolApproval"/> to a terminal state.
/// </para>
/// <para>
/// <b>Steer and follow-up policy.</b> Steering and follow-up are distinct intents and are exempt from
/// <see cref="ErrorCode.PromptInFlight"/>. Both are recorded in the session's messages and log at submission time
/// and are queued until the active prompt settles: a steer applies to the prompt in flight and so requires one;
/// a follow-up is queued behind it and is dispatched by the caller once the active prompt settles, or immediately
/// when the session is idle. This service records queued items; it does not dispatch them.
/// </para>
/// <para>
/// <b>Mutual exclusion.</b> Per-session gates are a fixed array of 64 <see cref="SemaphoreSlim"/> stripes
/// selected by a stable hash of the session identifier, so gate memory is bounded no matter how many sessions
/// are served. Operations on the same session always take the same stripe and stay atomic; operations on
/// different sessions sharing a stripe merely contend.
/// </para>
/// </remarks>
public sealed class SessionService
{
    /// <summary>The message of a failed turn's terminal error event when the harness supplied no message of its own.</summary>
    public const string FailedTurnMessage = "Prompt execution failed.";
    private const string UserRole = "user";
    private const string SteerRole = "steer";
    private const string FollowUpRole = "follow_up";
    private const string PromptAcceptedKind = "prompt_accepted";
    private const string SteerKind = "steer";
    private const string FollowUpKind = "follow_up";
    private const string ToolApprovalRequestedKind = "tool_approval_requested";
    private const string ToolApprovalApprovedKind = "tool_approval_approved";
    private const string ToolApprovalDeniedKind = "tool_approval_denied";
    private const string CompletedReason = "completed";
    private const string CancelledReason = "cancelled";

    /// <summary>The number of lock stripes. Bounded: one <see cref="SemaphoreSlim"/> per stripe, never per session.</summary>
    private const int StripeCount = 64;

    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim[] _stripes;
    private readonly System.Threading.Lock _approvalsGate = new();
    private readonly Dictionary<(string SessionId, string RequestId), PendingApproval> _pendingApprovals = [];

    /// <summary>Initializes a new instance of the <see cref="SessionService"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="timeProvider">The clock used for session and message timestamps.</param>
    public SessionService(ISessionRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;
        _stripes = new SemaphoreSlim[StripeCount];
        for (var i = 0; i < _stripes.Length; i++)
        {
            _stripes[i] = new SemaphoreSlim(1, 1);
        }
    }

    /// <summary>Creates an idle session owned by <paramref name="ownerId"/>.</summary>
    /// <param name="ownerId">The owning principal identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored session.</returns>
    public async Task<Session> CreateAsync(string ownerId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);

        var now = _timeProvider.GetUtcNow();
        var session = new Session
        {
            Id = Guid.NewGuid().ToString("N"),
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now,
            Status = SessionStatus.Idle,
            Messages = [],
            Events = [],
        };

        await _repository.SaveAsync(session, cancellationToken).ConfigureAwait(false);
        return session;
    }

    /// <summary>Starts a prompt on an idle session and records its acceptance.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="prompt">The user prompt text.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="intentKind">The parser intent-kind name recorded on the acceptance event, or null to omit it.</param>
    /// <param name="diagnosticCodes">The parser diagnostic codes recorded on the acceptance event, or null to omit them.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.PromptInFlight"/> when a prompt is already running or awaiting tool approval.
    /// </exception>
    public async Task SubmitPromptAsync(string sessionId, string prompt, CancellationToken cancellationToken, string? intentKind = null, IReadOnlyList<string>? diagnosticCodes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (IsPromptActive(session.Status))
            {
                throw new SessionException(new OmpOSError
                {
                    Code = ErrorCode.PromptInFlight,
                    Detail = $"Session '{sessionId}' already has a prompt in flight ({session.Status}).",
                });
            }

            var now = _timeProvider.GetUtcNow();
            var running = session with
            {
                Status = SessionStatus.Running,
                UpdatedAt = now,
                Messages = [.. session.Messages, new Message { Role = UserRole, Content = prompt, Timestamp = now }],
            };
            await _repository.SaveAsync(running, cancellationToken).ConfigureAwait(false);

            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, running),
                now,
                new UnknownPayload
                {
                    OriginalKind = PromptAcceptedKind,
                    Raw = BuildPromptAcceptedRaw(prompt, intentKind, diagnosticCodes),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Builds the <c>prompt_accepted</c> event payload with the prompt plus the parser envelope summary.</summary>
    /// <param name="prompt">The accepted prompt text.</param>
    /// <param name="intentKind">The parser intent-kind name, or null to omit it.</param>
    /// <param name="diagnosticCodes">The parser diagnostic codes, or null to omit them.</param>
    /// <returns>The payload object with <c>kind</c>, <c>prompt</c>, and, when present, <c>intentKind</c> and <c>diagnosticCodes</c>.</returns>
    private static JsonElement BuildPromptAcceptedRaw(string prompt, string? intentKind, IReadOnlyList<string>? diagnosticCodes)
    {
        var obj = new JsonObject { ["kind"] = PromptAcceptedKind, ["prompt"] = prompt };
        if (!string.IsNullOrEmpty(intentKind))
        {
            obj["intentKind"] = intentKind;
        }

        if (diagnosticCodes is not null)
        {
            var codes = new JsonArray();
            foreach (var code in diagnosticCodes)
            {
                codes.Add((JsonNode?)code);
            }

            obj["diagnosticCodes"] = codes;
        }

        using var doc = JsonDocument.Parse(obj.ToJsonString());
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// Records a steering message for the active prompt. Exempt from <see cref="ErrorCode.PromptInFlight"/>.
    /// </summary>
    /// <remarks>
    /// Requires an active prompt (<see cref="SessionStatus.Running"/> or <see cref="SessionStatus.AwaitingToolApproval"/>).
    /// The steer is queued until the harness delivers it to the prompt in flight.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="text">The steering text.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when no prompt is active to steer.
    /// </exception>
    public Task SubmitSteerAsync(string sessionId, string text, CancellationToken cancellationToken) =>
        SubmitQueuedAsync(sessionId, text, SteerRole, SteerKind, requireActivePrompt: true, cancellationToken);

    /// <summary>
    /// Records a follow-up message. Exempt from <see cref="ErrorCode.PromptInFlight"/>.
    /// </summary>
    /// <remarks>
    /// Accepted in any state. While a prompt is active the follow-up is queued until that prompt settles; the caller
    /// dispatches it then. On an idle session it is queued for immediate dispatch.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="text">The follow-up text.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException"><see cref="ErrorCode.SessionNotFound"/> when the session does not exist.</exception>
    public Task SubmitFollowUpAsync(string sessionId, string text, CancellationToken cancellationToken) =>
        SubmitQueuedAsync(sessionId, text, FollowUpRole, FollowUpKind, requireActivePrompt: false, cancellationToken);

    /// <summary>
    /// Settles the active prompt with a terminal status. The turn's terminal event is appended before the settled
    /// status is saved, so a subscriber that sees the settled status always finds that event ahead of its cursor.
    /// A turn that already carries a terminal event (an error streamed during the turn) gets no second one.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="final">The terminal status: <see cref="SessionStatus.Completed"/>, <see cref="SessionStatus.Failed"/> or <see cref="SessionStatus.Cancelled"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="stopReason">The harness stop reason, for example <c>end_turn</c>, written to the completed event; <see langword="null"/> writes <c>completed</c>. Ignored unless <paramref name="final"/> is <see cref="SessionStatus.Completed"/>.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not running or <paramref name="final"/> is not terminal.
    /// </exception>
    public async Task RecordCompletionAsync(string sessionId, SessionStatus final, CancellationToken cancellationToken, string? stopReason = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        if (final is not (SessionStatus.Completed or SessionStatus.Failed or SessionStatus.Cancelled))
        {
            throw new SessionException(new OmpOSError
            {
                Code = ErrorCode.InvalidTransition,
                Detail = $"Completion requires a terminal status; got {final}.",
            });
        }

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.Running)
            {
                throw new SessionException(new OmpOSError
                {
                    Code = ErrorCode.InvalidTransition,
                    Detail = $"Session '{sessionId}' cannot complete from {session.Status}; only a running prompt can settle.",
                });
            }

            var now = _timeProvider.GetUtcNow();
            if (!TurnHasTerminalEvent(session))
            {
                await AppendAsync(sessionId, CorrelationIdOf(sessionId, session), now, TerminalPayload(final, stopReason), cancellationToken).ConfigureAwait(false);
            }

            // The save carries the snapshot taken before the append. The repository keeps its longer stored log on a
            // stale save (SessionRepositoryContractTests), so the terminal event appended above survives this write.
            var settled = session with { Status = final, UpdatedAt = now };
            await _repository.SaveAsync(settled, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Pauses a running prompt for a tool decision, moving the session from <see cref="SessionStatus.Running"/> to
    /// <see cref="SessionStatus.AwaitingToolApproval"/>, recording the request in the event log and registering it as
    /// pending under <paramref name="requestId"/>.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier a decision is addressed to. Unique among the session's pending requests.</param>
    /// <param name="toolName">The tool the request concerns. A decision may allow it only when the ceiling permits it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="grantableTools">
    /// The policy ceiling: the tools a decision may allow. Null is the default ceiling, which permits none (DenyAll).
    /// </param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not running or the request identifier is already pending.
    /// </exception>
    public async Task RequestToolApprovalAsync(
        string sessionId,
        string requestId,
        string toolName,
        CancellationToken cancellationToken,
        IReadOnlySet<string>? grantableTools = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.Running)
            {
                throw InvalidTransition($"Session '{sessionId}' cannot request tool approval from {session.Status}; only a running prompt can pause for approval.");
            }

            if (FindPending(sessionId, requestId) is not null)
            {
                throw InvalidTransition($"Tool approval '{requestId}' is already pending in session '{sessionId}'.");
            }

            var now = _timeProvider.GetUtcNow();
            var waiting = session with { Status = SessionStatus.AwaitingToolApproval, UpdatedAt = now };
            await _repository.SaveAsync(waiting, cancellationToken).ConfigureAwait(false);
            AddPending(sessionId, requestId, new PendingApproval(toolName, grantableTools ?? FrozenSet<string>.Empty));

            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, waiting),
                now,
                new UnknownPayload
                {
                    OriginalKind = ToolApprovalRequestedKind,
                    Raw = HarnessEventProjection.BuildRaw(ToolApprovalRequestedKind, ("requestId", requestId), ("toolName", toolName)),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Settles a pending tool approval. This is the single production entry point for a decision: the endpoint calls it, and
    /// so does the timeout that denies a request nobody decided. An approval resumes the prompt; a denial records the denial.
    /// Both leave the session <see cref="SessionStatus.Running"/>, from which the caller settles the turn with
    /// <see cref="RecordCompletionAsync"/>. Any waiter on the request receives the decision.
    /// </summary>
    /// <remarks>
    /// An approval may allow only what the request's ceiling permits. A refused approval leaves the request pending, so a
    /// denial can still arrive.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier of the pending request.</param>
    /// <param name="approved"><see langword="true"/> to allow the tool call, <see langword="false"/> to deny it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not awaiting tool approval, or no request with that identifier
    /// is pending (it is unknown, or it was already decided).
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">The approval exceeds the request's ceiling. The request stays pending.</exception>
    public async Task ResolveToolApprovalAsync(string sessionId, string requestId, bool approved, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.AwaitingToolApproval)
            {
                throw InvalidTransition($"Session '{sessionId}' cannot resolve tool approval from {session.Status}; only a session awaiting tool approval can resolve it.");
            }

            var pending = FindPending(sessionId, requestId)
                ?? throw InvalidTransition($"No tool approval '{requestId}' is pending in session '{sessionId}'; it is unknown or already decided.");
            if (approved && !pending.GrantableTools.Contains(pending.ToolName))
            {
                throw new UnauthorizedAccessException($"The policy ceiling does not permit approving tool '{pending.ToolName}'.");
            }

            var now = _timeProvider.GetUtcNow();
            var resumed = session with { Status = SessionStatus.Running, UpdatedAt = now };
            await _repository.SaveAsync(resumed, cancellationToken).ConfigureAwait(false);
            RemovePending(sessionId, requestId);
            pending.Decision.TrySetResult(approved ? PermissionDecision.Allow : PermissionDecision.Deny);

            var kind = approved ? ToolApprovalApprovedKind : ToolApprovalDeniedKind;
            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, resumed),
                now,
                new UnknownPayload
                {
                    OriginalKind = kind,
                    Raw = HarnessEventProjection.BuildRaw(kind, ("requestId", requestId), ("approved", approved ? "true" : "false")),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Waits for the decision on a pending tool approval. A request that no decision reaches within <paramref name="timeout"/>
    /// is denied through <see cref="ResolveToolApprovalAsync"/>. A cancelled wait denies it too, before the cancellation propagates.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier of the pending request.</param>
    /// <param name="timeout">How long to wait for a decision. Must be positive.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The decision. A timed-out or cancelled wait returns <see cref="PermissionDecision.Deny"/> only for a timeout.</returns>
    /// <exception cref="SessionException"><see cref="ErrorCode.InvalidTransition"/> when no request with that identifier is pending.</exception>
    public async Task<PermissionDecision> WaitForDecisionAsync(string sessionId, string requestId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The decision timeout must be positive.");
        }

        var pending = FindPending(sessionId, requestId)
            ?? throw InvalidTransition($"No tool approval '{requestId}' is pending in session '{sessionId}'.");
        try
        {
            return await pending.Decision.Task.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            return await DenyLapsedAsync(sessionId, requestId, pending).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await DenyLapsedAsync(sessionId, requestId, pending).ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Denies a request whose wait lapsed or was cancelled, unless a decision landed first, and returns the settled decision.
    /// </summary>
    private async Task<PermissionDecision> DenyLapsedAsync(string sessionId, string requestId, PendingApproval pending)
    {
        try
        {
            await ResolveToolApprovalAsync(sessionId, requestId, approved: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (SessionException) when (pending.Decision.Task.IsCompleted)
        {
            // A decision landed first and already settled the request.
        }

        // Every path above returns only after the decision was set, so this await completes without waiting.
        return await pending.Decision.Task.WaitAsync(CancellationToken.None).ConfigureAwait(false);
    }

    private PendingApproval? FindPending(string sessionId, string requestId)
    {
        lock (_approvalsGate)
        {
            return _pendingApprovals.TryGetValue((sessionId, requestId), out var pending) ? pending : null;
        }
    }

    private void AddPending(string sessionId, string requestId, PendingApproval pending)
    {
        lock (_approvalsGate)
        {
            _pendingApprovals[(sessionId, requestId)] = pending;
        }
    }

    private void RemovePending(string sessionId, string requestId)
    {
        lock (_approvalsGate)
        {
            _pendingApprovals.Remove((sessionId, requestId));
        }
    }

    private static SessionException InvalidTransition(string detail) =>
        new(new OmpOSError { Code = ErrorCode.InvalidTransition, Detail = detail });

    private async Task SubmitQueuedAsync(
        string sessionId,
        string text,
        string role,
        string kind,
        bool requireActivePrompt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (requireActivePrompt && !IsPromptActive(session.Status))
            {
                throw new SessionException(new OmpOSError
                {
                    Code = ErrorCode.InvalidTransition,
                    Detail = $"Session '{sessionId}' has no active prompt to {kind}; it is {session.Status}.",
                });
            }

            var now = _timeProvider.GetUtcNow();
            var updated = session with
            {
                UpdatedAt = now,
                Messages = [.. session.Messages, new Message { Role = role, Content = text, Timestamp = now }],
            };
            await _repository.SaveAsync(updated, cancellationToken).ConfigureAwait(false);

            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, updated),
                now,
                new UnknownPayload { OriginalKind = kind, Raw = HarnessEventProjection.BuildRaw(kind, ("text", text)) },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<Session> LoadAsync(string sessionId, CancellationToken cancellationToken)
    {
        var session = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        return session ?? throw new SessionException(new OmpOSError
        {
            Code = ErrorCode.SessionNotFound,
            Detail = $"Session '{sessionId}' does not exist.",
        });
    }

    /// <summary>Selects the bounded lock stripe guarding <paramref name="sessionId"/>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The stripe every operation on that session takes, so same-session check-and-set stays atomic.</returns>
    private SemaphoreSlim GateFor(string sessionId) => _stripes[StripeIndex(sessionId)];

    /// <summary>Maps a session identifier to a stripe index with a stable FNV-1a hash.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>A stripe index in <c>[0, StripeCount)</c>.</returns>
    private static int StripeIndex(string sessionId)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var c in sessionId)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return (int)(hash % StripeCount);
        }
    }

    private async Task AppendAsync(
        string sessionId,
        string correlationId,
        DateTimeOffset timestamp,
        StreamEventPayload payload,
        CancellationToken cancellationToken)
    {
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = sessionId,
            CorrelationId = correlationId,
            Timestamp = timestamp,
            Payload = payload,
        };
        await _repository.AppendAsync(sessionId, evt, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The correlation identifier of the current turn: the session identifier plus the number of user prompts so far.
    /// Derived from stored state, so the start and the completion of one prompt share it without extra storage.
    /// </summary>
    private static string CorrelationIdOf(string sessionId, Session session)
    {
        var turn = session.Messages.Count(message => string.Equals(message.Role, UserRole, StringComparison.Ordinal));
        return $"{sessionId}/turn-{turn}";
    }

    private static bool IsPromptActive(SessionStatus status) =>
        status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;

    /// <summary>The terminal event that settles a turn: a completed event for success or cancel, an error event for failure.</summary>
    /// <param name="final">The terminal status.</param>
    /// <param name="stopReason">The harness stop reason, or null for <c>completed</c>.</param>
    /// <returns>The payload of the turn's terminal event.</returns>
    private static StreamEventPayload TerminalPayload(SessionStatus final, string? stopReason) => final switch
    {
        SessionStatus.Completed => new CompletedPayload { Reason = string.IsNullOrWhiteSpace(stopReason) ? CompletedReason : stopReason },
        SessionStatus.Failed => new ErrorPayload { Message = FailedTurnMessage, Retryable = false },
        _ => new CompletedPayload { Reason = CancelledReason },
    };

    /// <summary>
    /// Whether the active turn already carries its terminal event. Scans back to the turn's acceptance event, so a
    /// terminal event appended earlier in the same turn is found.
    /// </summary>
    /// <param name="session">The session, including its event log.</param>
    /// <returns>True when a completed or error event follows the turn's acceptance event.</returns>
    private static bool TurnHasTerminalEvent(Session session)
    {
        for (var i = session.Events.Count - 1; i >= 0; i--)
        {
            switch (session.Events[i].Payload)
            {
                case CompletedPayload or ErrorPayload:
                    return true;
                case UnknownPayload { OriginalKind: PromptAcceptedKind }:
                    return false;
            }
        }

        return false;
    }

    /// <summary>A tool approval awaiting its decision. Held in memory; the event log records the request and its outcome.</summary>
    private sealed class PendingApproval(string toolName, IReadOnlySet<string> grantableTools)
    {
        /// <summary>Gets the tool the request concerns.</summary>
        public string ToolName { get; } = toolName;

        /// <summary>Gets the tools a decision may allow: the request's policy ceiling.</summary>
        public IReadOnlySet<string> GrantableTools { get; } = grantableTools;

        /// <summary>Gets the decision. Completed exactly once, when the request is settled.</summary>
        public TaskCompletionSource<PermissionDecision> Decision { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
