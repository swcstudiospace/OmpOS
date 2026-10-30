using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Sessions.Postgres.Tests;

/// <summary>Builders for the sessions, events and approvals the PostgreSQL tests store.</summary>
internal static class SessionSamples
{
    /// <summary>A fixed instant, whole seconds, so timestamps survive the round trip exactly.</summary>
    internal static readonly DateTimeOffset Epoch = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Builds an idle session with no messages and no events.</summary>
    /// <param name="id">The session identifier.</param>
    /// <returns>The session.</returns>
    internal static Session Sample(string id) => new()
    {
        Id = id,
        OwnerId = "dev",
        CreatedAt = Epoch,
        UpdatedAt = Epoch,
        Status = SessionStatus.Idle,
        SandboxId = null,
        Messages = [],
        Events = [],
    };

    /// <summary>Builds an event for a session. Its Seq is assigned by the repository on append.</summary>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="payload">The event payload.</param>
    /// <returns>The event.</returns>
    internal static StreamEvent Event(string sessionId, StreamEventPayload payload) => new()
    {
        Seq = 0,
        SessionId = sessionId,
        CorrelationId = $"{sessionId}/turn-1",
        Timestamp = Epoch,
        Payload = payload,
    };

    /// <summary>Builds a pending tool-approval request whose policy grants one tool.</summary>
    /// <param name="sessionId">The owning session identifier.</param>
    /// <param name="requestId">The request identifier.</param>
    /// <returns>The pending record.</returns>
    internal static ToolApprovalRecord Approval(string sessionId, string requestId) => new()
    {
        SessionId = sessionId,
        RequestId = requestId,
        ToolName = "Bash",
        GrantableTools = ["Bash"],
        RequestedAt = Epoch,
        Decision = null,
        DecidedAt = null,
    };

    /// <summary>Appends text-delta events whose text is their index, starting at <paramref name="start"/>.</summary>
    /// <param name="repository">The repository to append through.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="start">The first index used as delta text.</param>
    /// <param name="count">The number of events to append.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when every append has finished.</returns>
    internal static async Task AppendDeltasAsync(ISessionRepository repository, string sessionId, int start, int count, CancellationToken cancellationToken)
    {
        for (var index = 0; index < count; index++)
        {
            await repository.AppendAsync(sessionId, Event(sessionId, new TextDeltaPayload { Delta = $"{start + index}" }), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Lists the events from a Seq onward.</summary>
    /// <param name="repository">The repository to read.</param>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="fromSeq">The inclusive lower Seq bound.</param>
    /// <param name="cancellationToken">Cancels the enumeration.</param>
    /// <returns>The events in Seq order.</returns>
    internal static async Task<List<StreamEvent>> ListAsync(ISessionRepository repository, string sessionId, long fromSeq, CancellationToken cancellationToken)
    {
        var events = new List<StreamEvent>();
        await foreach (var evt in repository.ListFromSeqAsync(sessionId, fromSeq, cancellationToken).ConfigureAwait(false))
        {
            events.Add(evt);
        }

        return events;
    }
}
