using System.Text.Json;
using System.Text.Json.Nodes;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

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
/// to <see cref="SessionStatus.AwaitingToolApproval"/>. <see cref="ResolveToolApprovalAsync"/> moves it back
/// to <see cref="SessionStatus.Running"/> in both outcomes: an approval resumes the prompt, and a denial
/// records the denial and likewise returns the session to <see cref="SessionStatus.Running"/>, from which the
/// caller settles the turn with <see cref="RecordCompletionAsync"/>. There is no direct transition from
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
    private const string UserRole = "user";
    private const string SteerRole = "steer";
    private const string FollowUpRole = "follow_up";
    private const string PromptAcceptedKind = "prompt_accepted";
    private const string SteerKind = "steer";
    private const string FollowUpKind = "follow_up";
    private const string ToolApprovalRequestedKind = "tool_approval_requested";
    private const string ToolApprovalApprovedKind = "tool_approval_approved";
    private const string ToolApprovalDeniedKind = "tool_approval_denied";

    /// <summary>The number of lock stripes. Bounded: one <see cref="SemaphoreSlim"/> per stripe, never per session.</summary>
    private const int StripeCount = 64;

    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim[] _stripes;

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

    /// <summary>Settles the active prompt with a terminal status and records the completion event.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="final">The terminal status: <see cref="SessionStatus.Completed"/>, <see cref="SessionStatus.Failed"/> or <see cref="SessionStatus.Cancelled"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not running or <paramref name="final"/> is not terminal.
    /// </exception>
    public async Task RecordCompletionAsync(string sessionId, SessionStatus final, CancellationToken cancellationToken)
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
            var settled = session with { Status = final, UpdatedAt = now };
            await _repository.SaveAsync(settled, cancellationToken).ConfigureAwait(false);

            var reason = final switch
            {
                SessionStatus.Completed => "completed",
                SessionStatus.Failed => "failed",
                _ => "cancelled",
            };
            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, settled),
                now,
                new CompletedPayload { Reason = reason },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Pauses a running prompt for a tool decision, moving the session from
    /// <see cref="SessionStatus.Running"/> to <see cref="SessionStatus.AwaitingToolApproval"/>
    /// and recording the request in the event log.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not running.
    /// </exception>
    internal async Task RequestToolApprovalAsync(string sessionId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

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
                    Detail = $"Session '{sessionId}' cannot request tool approval from {session.Status}; only a running prompt can pause for approval.",
                });
            }

            var now = _timeProvider.GetUtcNow();
            var waiting = session with { Status = SessionStatus.AwaitingToolApproval, UpdatedAt = now };
            await _repository.SaveAsync(waiting, cancellationToken).ConfigureAwait(false);

            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, waiting),
                now,
                new UnknownPayload
                {
                    OriginalKind = ToolApprovalRequestedKind,
                    Raw = HarnessEventProjection.BuildRaw(ToolApprovalRequestedKind),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Resolves a pending tool decision, moving the session from
    /// <see cref="SessionStatus.AwaitingToolApproval"/> back to <see cref="SessionStatus.Running"/>
    /// and recording the decision in the event log.
    /// </summary>
    /// <remarks>
    /// Both outcomes return the session to <see cref="SessionStatus.Running"/>: an approval resumes the
    /// prompt, and a denial records the denial and likewise leaves the session running, from which the
    /// caller settles the turn with <see cref="RecordCompletionAsync"/>. There is no direct transition
    /// from <see cref="SessionStatus.AwaitingToolApproval"/> to a terminal state.
    /// </remarks>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="approved"><see langword="true"/> to approve the tool call, <see langword="false"/> to deny it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.InvalidTransition"/> when the session is not awaiting tool approval.
    /// </exception>
    internal async Task ResolveToolApprovalAsync(string sessionId, bool approved, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);

        var gate = GateFor(sessionId);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = await LoadAsync(sessionId, cancellationToken).ConfigureAwait(false);
            if (session.Status != SessionStatus.AwaitingToolApproval)
            {
                throw new SessionException(new OmpOSError
                {
                    Code = ErrorCode.InvalidTransition,
                    Detail = $"Session '{sessionId}' cannot resolve tool approval from {session.Status}; only a session awaiting tool approval can resolve it.",
                });
            }

            var now = _timeProvider.GetUtcNow();
            var resumed = session with { Status = SessionStatus.Running, UpdatedAt = now };
            await _repository.SaveAsync(resumed, cancellationToken).ConfigureAwait(false);

            var kind = approved ? ToolApprovalApprovedKind : ToolApprovalDeniedKind;
            await AppendAsync(
                sessionId,
                CorrelationIdOf(sessionId, resumed),
                now,
                new UnknownPayload
                {
                    OriginalKind = kind,
                    Raw = HarnessEventProjection.BuildRaw(kind, ("approved", approved ? "true" : "false")),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

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
}
