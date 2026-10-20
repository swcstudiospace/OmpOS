using System.Collections.Concurrent;
using OmpOS.Contracts.Errors;
using OmpOS.Contracts.Sessions;
using OmpOS.Contracts.Streaming;

namespace OmpOS.Sessions;

/// <summary>
/// Session lifecycle rules: one active prompt per session (SESS-02), a steer/follow-up queueing policy, and
/// guarded completion. Every mutation of a session runs under that session's gate, so the check-and-set
/// in <see cref="SubmitPromptAsync"/> is atomic even under concurrent submissions.
/// </summary>
/// <remarks>
/// <para>
/// <b>Prompt admission.</b> <see cref="SubmitPromptAsync"/> is rejected with <see cref="ErrorCode.PromptInFlight"/>
/// (carried by <see cref="SessionException"/>) while the session is <see cref="SessionStatus.Running"/> or
/// <see cref="SessionStatus.AwaitingToolApproval"/>. A rejected submission is never queued.
/// </para>
/// <para>
/// <b>Steer and follow-up policy.</b> Steering and follow-up are distinct intents and are exempt from
/// <see cref="ErrorCode.PromptInFlight"/>. Both are recorded in the session's messages and log at submission time
/// and are queued until the active prompt settles: a steer applies to the prompt in flight and so requires one;
/// a follow-up is queued behind it and is dispatched by the caller once the active prompt settles, or immediately
/// when the session is idle. This service records queued items; it does not dispatch them.
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

    private readonly ISessionRepository _repository;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);

    /// <summary>Initializes a new instance of the <see cref="SessionService"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="timeProvider">The clock used for session and message timestamps.</param>
    public SessionService(ISessionRepository repository, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _repository = repository;
        _timeProvider = timeProvider;
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
    /// <exception cref="SessionException">
    /// <see cref="ErrorCode.SessionNotFound"/> when the session does not exist;
    /// <see cref="ErrorCode.PromptInFlight"/> when a prompt is already running or awaiting tool approval.
    /// </exception>
    public async Task SubmitPromptAsync(string sessionId, string prompt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var gate = _gates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
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
                    Raw = HarnessEventProjection.BuildRaw(PromptAcceptedKind, ("prompt", prompt)),
                },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
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

        var gate = _gates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
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

        var gate = _gates.GetOrAdd(sessionId, static _ => new SemaphoreSlim(1, 1));
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
