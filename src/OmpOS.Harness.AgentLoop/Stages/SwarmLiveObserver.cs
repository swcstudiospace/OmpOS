using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// Observes the AgentSwarm subagents of one session through the orchestrator's side channels: the <c>live.json</c> snapshot
/// (<c>swarm.live.v1</c>) and the tail of <c>events.jsonl</c>. The top-level stage <c>swarm</c> reports the swarm as a whole
/// and each subagent run is a child stage <c>agent:&lt;run id&gt;</c>. Both files are optional and may be malformed, partial or
/// growing without bound: reads are capped, a bad file is a degraded stage and never an exception, and only identifiers,
/// states, counters and short titles reach the events. Paths come only from <see cref="SessionStateLayout"/> for the scope's
/// own directory, so one session never reads another's.
/// </summary>
public sealed class SwarmLiveObserver : IStageSource
{
    /// <summary>The largest <c>live.json</c> the observer reads. The producer caps the document at 64 runs.</summary>
    internal const int MaxLiveFileBytes = 1024 * 1024;

    private static readonly Action<ILogger, FileReadStatus, Exception?> LiveFileUnusable =
        LoggerMessage.Define<FileReadStatus>(
            LogLevel.Warning,
            new EventId(1, "SwarmLiveFileUnusable"),
            "The swarm live file could not be used ({Status}); the event log alone feeds the subagent rows until it can.");

    private readonly SwarmLiveObserverOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<SwarmLiveObserver> _logger;

    /// <summary>Creates an observer.</summary>
    /// <param name="options">The options. The defaults when null.</param>
    /// <param name="timeProvider">The clock that stamps events and paces polling. The system clock when null.</param>
    /// <param name="logger">Receives unreadable-file diagnostics. Null discards them.</param>
    public SwarmLiveObserver(
        SwarmLiveObserverOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<SwarmLiveObserver>? logger = null)
    {
        _options = options ?? new SwarmLiveObserverOptions();
        if (_options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.PollInterval, "The poll interval must be positive.");
        }

        _clock = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<SwarmLiveObserver>.Instance;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var deduper = new StageEventDeduper(StageSource.Swarm, scope.SessionId, _clock);
        var state = new PollState();
        while (!cancellationToken.IsCancellationRequested)
        {
            List<StageEvent> events;
            try
            {
                events = deduper.Filter(await PollAsync(scope, state, cancellationToken).ConfigureAwait(false));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                ReportLiveUnusable(state, FileReadStatus.Unreadable, ex);
                events = deduper.Filter([new StageSnapshot(SwarmRunTable.SwarmStageId, "Subagents", StageStatus.Degraded, "Swarm status could not be read", null, scope.GraphId)]);
            }

            foreach (var stageEvent in events)
            {
                yield return stageEvent;
            }

            try
            {
                await Task.Delay(_options.PollInterval, _clock, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
        }
    }

    private static SwarmLiveDocument? ParseLive(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var document = JsonSerializer.Deserialize(utf8, StagesJsonContext.Default.SwarmLiveDocument);
            return document is not null && string.Equals(document.Schema, SwarmLiveDocument.SchemaId, StringComparison.Ordinal) ? document : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool MentionsRun(ReadOnlySpan<byte> line) =>
        line.IndexOf("\"swarm.run."u8) >= 0 || line.IndexOf("\"subagent."u8) >= 0;

    private static SwarmEventRecord? ParseEvent(ReadOnlySpan<byte> line)
    {
        try
        {
            return JsonSerializer.Deserialize(line, StagesJsonContext.Default.SwarmEventRecord);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<List<StageSnapshot>> PollAsync(StageObservationScope scope, PollState state, CancellationToken cancellationToken)
    {
        string livePath;
        string eventsPath;
        try
        {
            livePath = SessionStateLayout.SwarmLiveFile(scope.SessionStateDirectory);
            eventsPath = SessionStateLayout.SwarmEventsFile(scope.SessionStateDirectory);
        }
        catch (ArgumentException)
        {
            return [new StageSnapshot(SwarmRunTable.SwarmStageId, "Subagents", StageStatus.Degraded, "Swarm status could not be read", null, scope.GraphId)];
        }

        await ReadLiveAsync(livePath, state, cancellationToken).ConfigureAwait(false);

        var tail = await state.Tail.PollAsync(eventsPath, cancellationToken).ConfigureAwait(false);
        if (tail.Status != FileReadStatus.Missing)
        {
            state.EventsSeen = true;
        }

        foreach (var line in tail.Lines)
        {
            if (!MentionsRun(line.Span))
            {
                continue;
            }

            var record = ParseEvent(line.Span);
            if (record is not null)
            {
                state.Table.ApplyEvent(record);
            }
        }

        return state.Table.Project(state.LiveSeen || state.EventsSeen, state.LiveUnreadable, scope.GraphId);
    }

    private async Task ReadLiveAsync(string livePath, PollState state, CancellationToken cancellationToken)
    {
        var live = await StageFileReader.ReadAllAsync(livePath, MaxLiveFileBytes, cancellationToken).ConfigureAwait(false);
        switch (live.Status)
        {
            case FileReadStatus.Missing:
                break;
            case FileReadStatus.Ok:
                {
                    state.LiveSeen = true;
                    var document = ParseLive(live.Content.Span);
                    if (document is null)
                    {
                        state.LiveUnreadable = true;
                        ReportLiveUnusable(state, FileReadStatus.Unreadable, null);
                    }
                    else
                    {
                        state.LiveUnreadable = false;
                        state.LoggedLiveUnusable = false;
                        state.Table.ApplyLive(document);
                    }

                    break;
                }
            default:
                state.LiveSeen = true;
                state.LiveUnreadable = true;
                ReportLiveUnusable(state, live.Status, live.Error);
                break;
        }
    }

    private void ReportLiveUnusable(PollState state, FileReadStatus status, Exception? error)
    {
        if (state.LoggedLiveUnusable)
        {
            return;
        }

        state.LoggedLiveUnusable = true;
        LiveFileUnusable(_logger, status, error);
    }

    private sealed class PollState
    {
        public SwarmRunTable Table { get; } = new();

        public SwarmEventTail Tail { get; } = new();

        public bool LiveSeen { get; set; }

        public bool EventsSeen { get; set; }

        public bool LiveUnreadable { get; set; }

        public bool LoggedLiveUnusable { get; set; }
    }
}
