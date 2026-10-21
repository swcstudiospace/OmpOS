using System.Collections.Concurrent;
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
/// guarded completion. Every mutation of a session runs under that session's exclusive lock, so the check-and-set
/// in <see cref="SubmitPromptAsync"/> is atomic even under concurrent submissions, including submissions made through
/// different service instances or processes that share one store.
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
/// to <see cref="SessionStatus.AwaitingToolApproval"/> and stores the request under its identifier in the session's store.
/// <see cref="ResolveToolApprovalAsync"/> is the single entry point for a decision; it moves the session back to
/// <see cref="SessionStatus.Running"/> in both outcomes. An approval may allow only a tool the request's ceiling permits
/// (by default none), and a request that no decision reaches within the wait timeout is denied by
/// <see cref="WaitForDecisionAsync"/>. There is no direct transition from <see cref="SessionStatus.AwaitingToolApproval"/>
/// to a terminal state: <see cref="RecordCompletionAsync"/> denies the pending request first, which returns the session to
/// <see cref="SessionStatus.Running"/>, and then settles it.
/// </para>
/// <para>
/// <b>Steer and follow-up policy.</b> Steering and follow-up are distinct intents and are exempt from
/// <see cref="ErrorCode.PromptInFlight"/>. Both are recorded in the session's messages and log at submission time
/// and are queued until the active prompt settles: a steer applies to the prompt in flight and so requires one;
/// a follow-up is queued behind it and is dispatched by the caller once the active prompt settles, or immediately
/// when the session is idle. This service records queued items; it does not dispatch them.
/// </para>
/// <para>
/// <b>Mutual exclusion.</b> Each operation on a session first takes that session's in-process stripe: a fixed array of 64
/// <see cref="SemaphoreSlim"/> stripes selected by a stable hash of the session identifier, so gate memory is bounded no matter
/// how many sessions are served. It then takes the store's exclusive session lock (<see cref="ISessionRepository.LockSessionAsync"/>),
/// which also serializes operations on the same session across processes. Every load-check-save sequence runs under both,
/// in that order. The lock is not reentrant, so no operation calls another one while holding it.
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

    /// <summary>How often a waiting decision re-reads the store when no decision was signalled in this process.</summary>
    private static readonly TimeSpan DecisionPollInterval = TimeSpan.FromMilliseconds(200);

    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim[] _stripes;

    /// <summary>
    /// Wake-up fast path for waiters in this process. A waiter registers here before it reads the store; a decision recorded
    /// in this process removes and completes the registration. Decisions are never read from here: the store is the record.
    /// </summary>
    private readonly ConcurrentDictionary<(string SessionId, string RequestId), TaskCompletionSource> _decisionSignals = new();

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

        var lease = await EnterAsync(sessionId, cancellationToken).ConfigureAwait(false);
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
            await lease.ExitAsync().ConfigureAwait(false);
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
    /// A session awaiting tool approval has its pending request denied first, which returns it to running, and then settles.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="final">The terminal status: <see cref="SessionStatus.Completed"/>, <see cref="SessionStatus.Failed"/> or <see cref="SessionStatus.Cancelled"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="stopReason">The harness stop reason, for example <c>end_turn</c>, written to the completed event; <see langword="null"/> writes <c>completed</c>. Ignored unless <paramref name="final"/> is <see cref="SessionStatus.Completed"/>.</param>
    /// <param name="failureDetail">Detail appended to a failed turn's terminal error message as <c>" Detail: "</c> plus the trimmed detail. Ignored unless <paramref name="final"/> is <see cref="SessionStatus.Failed"/>; null, empty or whitespace keeps the message <see cref="FailedTurnMessage"/>.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is neither running nor awaiting tool approval, or <paramref name="final"/> is not terminal.
    /// </exception>
    public async Task RecordCompletionAsync(string sessionId, SessionStatus final, CancellationToken cancellationToken, string? stopReason = null, string? failureDetail = null)
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

        var lease = await EnterAsync(sessionId, cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status == SessionStatus.AwaitingToolApproval)
            {
                // The prompt ends while it awaits a decision: deny the pending request first. The denial returns the session to Running.
                await DenyPendingApprovalsUnderLockAsync(session).ConfigureAwait(false);
                session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            }

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
                await AppendAsync(sessionId, CorrelationIdOf(sessionId, session), now, TerminalPayload(final, stopReason, failureDetail), cancellationToken).ConfigureAwait(false);
            }

            // The save carries the snapshot taken before the append. The repository keeps its longer stored log on a
            // stale save (SessionRepositoryContractTests), so the terminal event appended above survives this write.
            var settled = session with { Status = final, UpdatedAt = now };
            await _repository.SaveAsync(settled, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await lease.ExitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pauses a running prompt for a tool decision, moving the session from <see cref="SessionStatus.Running"/> to
    /// <see cref="SessionStatus.AwaitingToolApproval"/>, recording the request in the event log and storing it as
    /// pending under <paramref name="requestId"/>.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier a decision is addressed to. Unique within the session for its whole life.</param>
    /// <param name="toolName">The tool the request concerns. A decision may allow it only when the ceiling permits it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <param name="grantableTools">
    /// The policy ceiling: the tools a decision may allow. Null is the default ceiling, which permits none (DenyAll).
    /// </param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not running or the request identifier was already used in the session.
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

        var lease = await EnterAsync(sessionId, cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.Running)
            {
                throw InvalidTransition($"Session '{sessionId}' cannot request tool approval from {session.Status}; only a running prompt can pause for approval.");
            }

            var now = _timeProvider.GetUtcNow();
            var ceiling = (grantableTools ?? FrozenSet<string>.Empty).Order(StringComparer.Ordinal).ToArray();
            var request = new ToolApprovalRecord
            {
                SessionId = sessionId,
                RequestId = requestId,
                ToolName = toolName,
                GrantableTools = ceiling,
                RequestedAt = now,
            };
            if (!await _repository.TryAddToolApprovalAsync(sessionId, request, cancellationToken).ConfigureAwait(false))
            {
                throw InvalidTransition($"Tool approval '{requestId}' already exists in session '{sessionId}'; a request identifier is used once per session.");
            }

            var waiting = session with { Status = SessionStatus.AwaitingToolApproval, UpdatedAt = now };
            await _repository.SaveAsync(waiting, cancellationToken).ConfigureAwait(false);

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
            await lease.ExitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Settles a pending tool approval. This is the single production entry point for a decision: the endpoint calls it, and
    /// so does the timeout that denies a request nobody decided. An approval resumes the prompt; a denial records the denial.
    /// Both leave the session <see cref="SessionStatus.Running"/>, from which the caller settles the turn with
    /// <see cref="RecordCompletionAsync"/>. Any waiter on the request, in this process or another, receives the decision.
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

        var lease = await EnterAsync(sessionId, cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.AwaitingToolApproval)
            {
                throw InvalidTransition($"Session '{sessionId}' cannot resolve tool approval from {session.Status}; only a session awaiting tool approval can resolve it.");
            }

            var pending = await _repository.GetToolApprovalAsync(sessionId, requestId, cancellationToken).ConfigureAwait(false);
            if (pending is null or { Decision: not null })
            {
                throw InvalidTransition($"No tool approval '{requestId}' is pending in session '{sessionId}'; it is unknown or already decided.");
            }

            if (approved && !pending.GrantableTools.Contains(pending.ToolName, StringComparer.Ordinal))
            {
                throw new UnauthorizedAccessException($"The policy ceiling does not permit approving tool '{pending.ToolName}'.");
            }

            await SettleAsync(session, requestId, approved ? PermissionDecision.Allow : PermissionDecision.Deny, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await lease.ExitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Waits for the decision on a tool approval. A decision already recorded is returned at once. A request that no decision
    /// reaches within <paramref name="timeout"/> is denied under the session lock, and so is a cancelled wait, before the
    /// cancellation propagates. Decisions are read from the store, so the wait observes a decision recorded by any process.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier of the request.</param>
    /// <param name="timeout">How long to wait for a decision. Must be positive.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The recorded decision. A timed-out wait returns <see cref="PermissionDecision.Deny"/> unless another decision was recorded first.</returns>
    /// <exception cref="SessionException"><see cref="ErrorCode.InvalidTransition"/> when no request with that identifier exists.</exception>
    /// <exception cref="OperationCanceledException">The wait was cancelled. The request is denied first.</exception>
    public async Task<PermissionDecision> WaitForDecisionAsync(string sessionId, string requestId, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "The decision timeout must be positive.");
        }

        using var lapse = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        lapse.CancelAfter(timeout);
        try
        {
            return await AwaitDecisionAsync(sessionId, requestId, lapse.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lapse.IsCancellationRequested)
        {
            // Deny first, so no approval stays pending after the wait ends. A cancelled wait then rethrows the original cancellation.
            var settled = await DenyLapsedAsync(sessionId, requestId).ConfigureAwait(false);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            return settled;
        }
        finally
        {
            _decisionSignals.TryRemove((sessionId, requestId), out _);
        }
    }

    /// <summary>
    /// Polls the store for the request's decision. A decision recorded in this process wakes the poll early through
    /// <see cref="_decisionSignals"/>; a decision recorded in another process is seen at the next poll.
    /// </summary>
    private async Task<PermissionDecision> AwaitDecisionAsync(string sessionId, string requestId, CancellationToken cancellationToken)
    {
        var key = (sessionId, requestId);
        while (true)
        {
            // Register before reading, so a decision recorded after the read still wakes this wait.
            var signal = _decisionSignals.GetOrAdd(key, static _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));

            var record = await _repository.GetToolApprovalAsync(sessionId, requestId, cancellationToken).ConfigureAwait(false);
            if (record is null)
            {
                throw InvalidTransition($"No tool approval '{requestId}' is pending in session '{sessionId}'.");
            }

            if (record.Decision is { } decision)
            {
                return decision;
            }

            await Task.WhenAny(signal.Task, Task.Delay(DecisionPollInterval, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    /// <summary>
    /// Denies a request whose wait lapsed or was cancelled, under the session lock, unless a decision was recorded first.
    /// Returns the request's settled decision.
    /// </summary>
    private async Task<PermissionDecision> DenyLapsedAsync(string sessionId, string requestId)
    {
        var lease = await EnterAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        try
        {
            return await DenyLapsedUnderLockAsync(sessionId, requestId).ConfigureAwait(false);
        }
        finally
        {
            await lease.ExitAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The body of <see cref="DenyLapsedAsync"/> for a caller that already holds the session lock. The lock is not reentrant:
    /// calling <see cref="DenyLapsedAsync"/> from inside a locked operation would wait for its own lock.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="requestId">The identifier of the request.</param>
    /// <returns>The request's settled decision.</returns>
    private async Task<PermissionDecision> DenyLapsedUnderLockAsync(string sessionId, string requestId)
    {
        var record = await _repository.GetToolApprovalAsync(sessionId, requestId, CancellationToken.None).ConfigureAwait(false);
        if (record is null)
        {
            throw InvalidTransition($"No tool approval '{requestId}' is pending in session '{sessionId}'.");
        }

        if (record.Decision is { } settled)
        {
            return settled;
        }

        var session = await LoadAsync(sessionId, CancellationToken.None).ConfigureAwait(false);
        await SettleAsync(session, requestId, PermissionDecision.Deny, CancellationToken.None).ConfigureAwait(false);
        return PermissionDecision.Deny;
    }

    /// <summary>
    /// Denies every tool approval of a session that is still pending. Runs under the session lock the caller holds. The
    /// requests come from the session's event log, the only place that lists them, so no pending request is missed. Each
    /// denial returns the session to Running.
    /// </summary>
    /// <param name="session">The session as loaded under the lock.</param>
    private async Task DenyPendingApprovalsUnderLockAsync(Session session)
    {
        foreach (var requestId in RequestedApprovalIds(session))
        {
            var record = await _repository.GetToolApprovalAsync(session.Id, requestId, CancellationToken.None).ConfigureAwait(false);
            if (record is { Decision: null })
            {
                await DenyLapsedUnderLockAsync(session.Id, requestId).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Lists the distinct request identifiers of the tool approvals requested in a session's event log, in log order.</summary>
    /// <param name="session">The session, including its event log.</param>
    /// <returns>The request identifiers.</returns>
    private static IEnumerable<string> RequestedApprovalIds(Session session)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var evt in session.Events)
        {
            if (evt.Payload is UnknownPayload { OriginalKind: ToolApprovalRequestedKind } payload
                && payload.Raw.ValueKind == JsonValueKind.Object
                && payload.Raw.TryGetProperty("requestId", out var id)
                && id.ValueKind == JsonValueKind.String
                && id.GetString() is { } requestId
                && seen.Add(requestId))
            {
                yield return requestId;
            }
        }
    }

    /// <summary>
    /// Records a decision on a pending tool approval and resumes the session. Runs under the session lock, on a session loaded
    /// inside that lock. The decision is recorded in the store first, so the first decision wins across processes.
    /// </summary>
    private async Task SettleAsync(Session session, string requestId, PermissionDecision decision, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var decided = await _repository.TryDecideToolApprovalAsync(session.Id, requestId, decision, now, cancellationToken).ConfigureAwait(false);
        if (!decided)
        {
            throw InvalidTransition($"Tool approval '{requestId}' in session '{session.Id}' was already decided.");
        }

        var resumed = session with { Status = SessionStatus.Running, UpdatedAt = now };
        await _repository.SaveAsync(resumed, cancellationToken).ConfigureAwait(false);

        var approved = decision == PermissionDecision.Allow;
        var kind = approved ? ToolApprovalApprovedKind : ToolApprovalDeniedKind;
        await AppendAsync(
            session.Id,
            CorrelationIdOf(session.Id, resumed),
            now,
            new UnknownPayload
            {
                OriginalKind = kind,
                Raw = HarnessEventProjection.BuildRaw(kind, ("requestId", requestId), ("approved", approved ? "true" : "false")),
            },
            cancellationToken).ConfigureAwait(false);

        if (_decisionSignals.TryRemove((session.Id, requestId), out var signal))
        {
            signal.TrySetResult();
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

        var lease = await EnterAsync(sessionId, cancellationToken).ConfigureAwait(false);
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
            await lease.ExitAsync().ConfigureAwait(false);
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

    /// <summary>
    /// Takes the session's in-process stripe, then its store lock. Every load-check-save sequence on a session runs between
    /// this call and <see cref="SessionLease.ExitAsync"/>, in a try block whose finally exits the lease.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the wait for either lock.</param>
    /// <returns>The lease to exit when the operation ends.</returns>
    private async Task<SessionLease> EnterAsync(string sessionId, CancellationToken cancellationToken)
    {
        var stripe = GateFor(sessionId);
        await stripe.WaitAsync(cancellationToken).ConfigureAwait(false);

        var locked = false;
        try
        {
            var storeLock = await _repository.LockSessionAsync(sessionId, cancellationToken).ConfigureAwait(false);
            locked = true;
            return new SessionLease(stripe, storeLock);
        }
        finally
        {
            if (!locked)
            {
                stripe.Release();
            }
        }
    }

    /// <summary>Selects the bounded lock stripe guarding <paramref name="sessionId"/>.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The stripe every operation on that session takes, so same-session check-and-set stays atomic within this process.</returns>
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
    /// <param name="failureDetail">Detail appended to the failed turn's message as <c>" Detail: "</c> plus the trimmed detail; null, empty or whitespace keeps the message unchanged.</param>
    /// <returns>The payload of the turn's terminal event.</returns>
    private static StreamEventPayload TerminalPayload(SessionStatus final, string? stopReason, string? failureDetail) => final switch
    {
        SessionStatus.Completed => new CompletedPayload { Reason = string.IsNullOrWhiteSpace(stopReason) ? CompletedReason : stopReason },
        SessionStatus.Failed => new ErrorPayload { Message = string.IsNullOrWhiteSpace(failureDetail) ? FailedTurnMessage : $"{FailedTurnMessage} Detail: {failureDetail.Trim()}", Retryable = false },
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

    /// <summary>
    /// The in-process stripe and the store's session lock held for one operation. Exiting releases the store lock first,
    /// then the stripe, so the stripe is never released while the store still holds the session.
    /// </summary>
    private sealed class SessionLease(SemaphoreSlim stripe, IAsyncDisposable storeLock)
    {
        /// <summary>Releases the store lock, then the in-process stripe.</summary>
        public async Task ExitAsync()
        {
            try
            {
                await storeLock.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                stripe.Release();
            }
        }
    }
}
