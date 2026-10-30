using System.Threading.Channels;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Contracts.Catalog;
using Autonogrammer.Contracts.Controls;
using Autonogrammer.Contracts.Sessions;
using Autonogrammer.Contracts.Streaming;

namespace Autonogrammer.Web.Demo;

/// <summary>The mutable state of one Demo session: its log, stages, options, approvals, changes and the subscribers of its event stream. Used on the one UI context of the browser.</summary>
internal sealed class DemoSession
{
    private readonly List<Channel<StreamEvent>> _subscribers = [];
    private readonly Lock _gate = new();

    /// <summary>Gets the session identifier.</summary>
    public required string Id { get; init; }

    /// <summary>Gets what the session is.</summary>
    public CatalogSessionKind Kind { get; init; } = CatalogSessionKind.Agent;

    /// <summary>Gets the time the session was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Gets or sets the time of the last stored change.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the stored status.</summary>
    public SessionStatus Status { get; set; }

    /// <summary>Gets or sets a value indicating whether the operator closed the session.</summary>
    public bool Closed { get; set; }

    /// <summary>Gets or sets the Graph identifier.</summary>
    public string? GraphId { get; set; }

    /// <summary>Gets or sets a value indicating whether a scripted turn is in flight.</summary>
    public bool RunInFlight { get; set; }

    /// <summary>Gets or sets the number of steering messages the scripted turn has not yet acknowledged.</summary>
    public int PendingSteers { get; set; }

    /// <summary>Gets the stored messages.</summary>
    public List<Message> Messages { get; } = [];

    /// <summary>Gets the stored event log.</summary>
    public List<StreamEvent> Events { get; } = [];

    /// <summary>Gets the stage timeline.</summary>
    public List<StageEvent> Stages { get; } = [];

    /// <summary>Gets the engine options.</summary>
    public List<ConfigOption> Options { get; } = [];

    /// <summary>Gets the pending tool approvals.</summary>
    public List<PendingToolApproval> Approvals { get; } = [];

    /// <summary>Gets the changed files.</summary>
    public List<SessionChange> Changes { get; } = [];

    /// <summary>Gets or sets the task context, or null when no task is bound.</summary>
    public TaskContext? Context { get; set; }

    /// <summary>Gets or sets the holder of the task lease.</summary>
    public string? LeaseHolder { get; set; }

    /// <summary>Gets a value indicating whether a turn runs or waits for an approval.</summary>
    public bool IsActive => Status is SessionStatus.Running or SessionStatus.AwaitingToolApproval;

    /// <summary>Gets the rail status the API reports for the session.</summary>
    public CatalogRowStatus RowStatus
    {
        get
        {
            if (Closed && !IsActive)
            {
                return CatalogRowStatus.Closed;
            }

            return Kind == CatalogSessionKind.Terminal
                ? CatalogRowStatus.Running
                : Status switch
                {
                    SessionStatus.Idle => CatalogRowStatus.Ready,
                    SessionStatus.Running => CatalogRowStatus.Running,
                    SessionStatus.AwaitingToolApproval => CatalogRowStatus.NeedsApproval,
                    SessionStatus.Completed => CatalogRowStatus.Completed,
                    SessionStatus.Failed => CatalogRowStatus.Failed,
                    SessionStatus.Cancelled => CatalogRowStatus.Paused,
                    _ => CatalogRowStatus.Unknown,
                };
        }
    }

    /// <summary>Gets the rail group the API reports for the session.</summary>
    public CatalogRowGroup Group => RowStatus switch
    {
        CatalogRowStatus.NeedsApproval or CatalogRowStatus.WaitingForYou => CatalogRowGroup.NeedsYou,
        CatalogRowStatus.Running or CatalogRowStatus.Queued => CatalogRowGroup.Running,
        CatalogRowStatus.Lost => CatalogRowGroup.Lost,
        CatalogRowStatus.Completed or CatalogRowStatus.Failed => CatalogRowGroup.Finished,
        _ => CatalogRowGroup.Idle,
    };

    /// <summary>Gets the time of the last activity.</summary>
    public DateTimeOffset LastActivityAt => Events.Count > 0 && Events[^1].Timestamp > UpdatedAt ? Events[^1].Timestamp : UpdatedAt;

    /// <summary>Gets the sequence number the next event takes.</summary>
    public long NextSeq => Events.Count == 0 ? 1 : Events[^1].Seq + 1;

    /// <summary>Appends an event to the log and hands it to every subscriber.</summary>
    /// <param name="payload">The payload.</param>
    /// <param name="now">The event time.</param>
    /// <returns>The stored event.</returns>
    public StreamEvent Append(StreamEventPayload payload, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(payload);
        lock (_gate)
        {
            var evt = new StreamEvent { Seq = NextSeq, SessionId = Id, CorrelationId = Id, Timestamp = now, Payload = payload };
            Events.Add(evt);
            UpdatedAt = now;
            foreach (var subscriber in _subscribers)
            {
                _ = subscriber.Writer.TryWrite(evt);
            }

            return evt;
        }
    }

    /// <summary>Copies the events after a position, in order.</summary>
    /// <param name="afterSeq">The last sequence the reader has.</param>
    /// <returns>The later events.</returns>
    public IReadOnlyList<StreamEvent> EventsAfter(long afterSeq)
    {
        lock (_gate)
        {
            return [.. Events.Where(evt => evt.Seq > afterSeq)];
        }
    }

    /// <summary>Opens a subscription to the events appended from now on.</summary>
    /// <returns>The channel to read.</returns>
    public Channel<StreamEvent> Subscribe()
    {
        var channel = Channel.CreateUnbounded<StreamEvent>(new UnboundedChannelOptions { SingleReader = true });
        lock (_gate)
        {
            _subscribers.Add(channel);
        }

        return channel;
    }

    /// <summary>Closes a subscription.</summary>
    /// <param name="channel">The channel <see cref="Subscribe"/> returned.</param>
    public void Unsubscribe(Channel<StreamEvent> channel)
    {
        ArgumentNullException.ThrowIfNull(channel);
        lock (_gate)
        {
            _ = _subscribers.Remove(channel);
        }

        _ = channel.Writer.TryComplete();
    }

    /// <summary>Builds the stored session the API returns for <c>GET /sessions/{id}</c>.</summary>
    /// <returns>The session.</returns>
    public Session ToSession() => new()
    {
        Id = Id,
        OwnerId = DemoWorld.OwnerId,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Status = Status,
        Messages = [.. Messages],
        Events = EventsAfter(0),
    };

    /// <summary>Builds the catalogue row the API returns for <c>GET /sessions</c>.</summary>
    /// <returns>The row.</returns>
    public SessionCatalogEntry ToEntry() => new()
    {
        Id = Id,
        OwnerId = DemoWorld.OwnerId,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        Status = Status,
        Messages = [.. Messages],
        Events = EventsAfter(0),
        Kind = Kind,
        RowStatus = RowStatus,
        Group = Group,
        LastActivityAt = LastActivityAt,
        GraphId = GraphId,
        Closed = Closed,
        WorkspaceLabel = DemoWorld.WorkspaceName,
    };
}
