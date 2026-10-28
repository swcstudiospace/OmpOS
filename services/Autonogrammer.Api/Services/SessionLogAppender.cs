using Autonogrammer.Api.Sse;
using Autonogrammer.Contracts.Streaming;
using Autonogrammer.Sessions;

namespace Autonogrammer.Api.Services;

/// <summary>Appends an event of this contract version (a stage or a configuration update) to a session's log and wakes its streams.</summary>
internal sealed class SessionLogAppender
{
    private readonly ISessionRepository _repository;
    private readonly SessionChangePublisher _publisher;
    private readonly TimeProvider _time;

    /// <summary>Initializes a new instance of the <see cref="SessionLogAppender"/> class.</summary>
    /// <param name="repository">The session store.</param>
    /// <param name="publisher">Announces the appended event to this process and the other replicas.</param>
    /// <param name="time">The clock.</param>
    public SessionLogAppender(ISessionRepository repository, SessionChangePublisher publisher, TimeProvider time)
    {
        _repository = repository;
        _publisher = publisher;
        _time = time;
    }

    /// <summary>Appends a payload to the log of a session.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="payload">The payload.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>True when appended; false when the session does not exist.</returns>
    public async Task<bool> AppendAsync(string sessionId, StreamEventPayload payload, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(payload);
        var session = await _repository.GetAsync(sessionId, cancellationToken).ConfigureAwait(false);
        if (session is null)
        {
            return false;
        }

        var turn = session.Messages.Count(static message => string.Equals(message.Role, "user", StringComparison.Ordinal));
        var evt = new StreamEvent
        {
            Seq = 0,
            SessionId = sessionId,
            CorrelationId = $"{sessionId}/turn-{turn}",
            Timestamp = _time.GetUtcNow(),
            Payload = payload,
        };
        await _repository.AppendAsync(sessionId, evt, cancellationToken).ConfigureAwait(false);
        await _publisher.NotifyAsync(sessionId).ConfigureAwait(false);
        return true;
    }
}
