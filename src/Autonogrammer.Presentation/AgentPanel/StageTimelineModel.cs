using System.Windows.Input;
using Autonogrammer.Contracts.Agents;
using Autonogrammer.Presentation.Observable;

namespace Autonogrammer.Presentation.AgentPanel;

/// <summary>
/// The pipeline of one session as the Agent Panel shows it. It folds <see cref="StageEvent"/>s by key (session, source, stage), keeps the
/// latest event of each key, and projects them into source blocks for the Pipeline tab, pills for the pipeline strip, a roll-up count of
/// degraded stages, and one polite announcement per stage transition.
/// </summary>
/// <remarks>
/// <para>
/// Ordering: blocks follow pipeline order (Engine, Ultrathink, Swarm, Substrate, GSD, then Other for an unknown source). Inside a block
/// the top-level rows keep the order of their first observation (time, then arrival), so a row never jumps when a later event updates it;
/// each row is followed by its children (<see cref="StageEvent.ParentStageId"/>). A child whose parent is not known is shown as a top-level row.
/// </para>
/// <para>
/// Tolerance: an unknown <see cref="StageSource"/> or <see cref="StageStatus"/> becomes a generic row (the status reads <c>Unknown</c>)
/// and is never announced; nothing here throws for the content of an event. At most <see cref="MaxRows"/> keys are kept: a further new key
/// is counted in <see cref="OverflowCount"/> and dropped, while keys already shown keep updating.
/// </para>
/// <para>Not thread-safe: it runs on the UI context that owns the presentation models.</para>
/// </remarks>
public sealed class StageTimelineModel : ObservableModel
{
    /// <summary>The most stage keys kept.</summary>
    public const int MaxRows = 200;

    /// <summary>The deepest indentation of a child row.</summary>
    public const int MaxDepth = 4;

    /// <summary>The most characters kept of a stage title.</summary>
    public const int MaxTitleChars = 120;

    /// <summary>The most characters kept of a stage detail before the ellipsis.</summary>
    public const int MaxDetailChars = 240;

    /// <summary>The source of the <c>Other</c> block, which holds rows from a source this build does not know.</summary>
    public const StageSource OtherSource = (StageSource)(-1);

    private readonly Dictionary<(StageSource Source, string StageId), StageEntry> _entries = [];
    private readonly StageAnnouncer _announcer;
    private readonly Func<StageSource, CancellationToken, Task>? _recheck;
    private readonly Dictionary<StageSource, AgentPanelCommand> _retryCommands = [];
    private long _sequence;
    private PipelineSnapshot _snapshot;
    private GsdPhaseInfo? _phase;
    private string? _announcement;
    private int _announcementVersion;
    private int _overflowCount;
    private string _pipelineAccessibleName = string.Empty;

    /// <summary>Creates an empty timeline for one session.</summary>
    /// <param name="sessionId">The session whose events it folds. Events of any other session are ignored.</param>
    /// <param name="timeProvider">The clock of the subagent announcement throttle.</param>
    /// <param name="recheck">
    /// Re-checks the source of a stage on request (a health check or an observer restart); it never re-runs a model turn. Null means no
    /// stage offers retry.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="sessionId"/> is null, empty or white space.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public StageTimelineModel(string sessionId, TimeProvider timeProvider, Func<StageSource, CancellationToken, Task>? recheck = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentNullException.ThrowIfNull(timeProvider);

        SessionId = sessionId;
        _announcer = new StageAnnouncer(timeProvider);
        _recheck = recheck;
        _snapshot = PipelineProjection.Project([], recheck is not null, null);
        _pipelineAccessibleName = BuildAccessibleName(_snapshot);
    }

    /// <summary>Gets the session this timeline belongs to.</summary>
    public string SessionId { get; }

    /// <summary>Gets the Pipeline tab blocks in pipeline order. The Engine, Ultrathink, Swarm, Substrate and GSD blocks are always present.</summary>
    public IReadOnlyList<StageSourceBlock> Blocks => _snapshot.Blocks;

    /// <summary>Gets the pipeline strip pills: Ultrathink, Swarm, Substrate and GSD, in that order.</summary>
    public IReadOnlyList<PipelinePill> Pills => _snapshot.Pills;

    /// <summary>Gets the accessible name of the pipeline strip: <c>Pipeline: </c>, the pill texts joined by commas, then <c>. Open details.</c>.</summary>
    public string PipelineAccessibleName => _pipelineAccessibleName;

    /// <summary>
    /// Gets the latest announcement for the polite <c>Pipeline announcements</c> live region, or null before the first one. A view only
    /// binds this string and never builds announcement text.
    /// </summary>
    public string? StatusAnnouncement => _announcement;

    /// <summary>Gets a number that grows with every announcement, including one that repeats the previous text.</summary>
    public int AnnouncementVersion => _announcementVersion;

    /// <summary>Gets the number of top-level plugin stages that are skipped, degraded, failed or unavailable. The panel notice shows it.</summary>
    public int DegradedStageCount => _snapshot.DegradedCount;

    /// <summary>Gets whether all four plugin sources reported and every one is unavailable, so the agent runs as plain omp.</summary>
    public bool AllPluginsUnavailable => _snapshot.AllPluginsUnavailable;

    /// <summary>Gets whether any event has been folded.</summary>
    public bool HasEvents => _entries.Count > 0;

    /// <summary>Gets the number of new stage keys dropped because <see cref="MaxRows"/> were already kept.</summary>
    public int OverflowCount => _overflowCount;

    /// <summary>Gets the workflow phase the GSD pill shows, or null when it is unknown.</summary>
    public GsdPhaseInfo? Phase => _phase;

    /// <summary>Folds one event. An event for another session, an older observation of a known key, or an identical event changes nothing.</summary>
    /// <param name="stageEvent">The event.</param>
    /// <returns>True when the timeline changed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stageEvent"/> is null.</exception>
    public bool Apply(StageEvent stageEvent)
    {
        ArgumentNullException.ThrowIfNull(stageEvent);

        if (!string.Equals(stageEvent.SessionId, SessionId, StringComparison.Ordinal))
        {
            return false;
        }

        var key = (stageEvent.Source, stageEvent.StageId);
        StageStatus? previous = null;
        if (_entries.TryGetValue(key, out var existing))
        {
            if (stageEvent.Timestamp < existing.Latest.Timestamp || stageEvent == existing.Latest)
            {
                return false;
            }

            previous = existing.Latest.Status;
            existing.Latest = stageEvent;
        }
        else
        {
            if (_entries.Count >= MaxRows)
            {
                _overflowCount++;
                NotifyPropertyChanged(nameof(OverflowCount));
                return false;
            }

            _entries[key] = new StageEntry(stageEvent, _sequence++);
        }

        var sentence = _announcer.Observe(stageEvent, previous);
        Rebuild();
        if (sentence is not null)
        {
            Announce(sentence);
        }

        return true;
    }

    /// <summary>Folds several events in order.</summary>
    /// <param name="stageEvents">The events.</param>
    /// <returns>The number of events that changed the timeline.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="stageEvents"/> is null.</exception>
    public int ApplyAll(IEnumerable<StageEvent> stageEvents)
    {
        ArgumentNullException.ThrowIfNull(stageEvents);

        var changed = 0;
        foreach (var stageEvent in stageEvents)
        {
            if (Apply(stageEvent))
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Announces a sentence that is not a stage transition, for example that the agent finished or was closed. The announcement version
    /// grows even when the text repeats, so a live region can speak it again.
    /// </summary>
    /// <param name="sentence">The plain sentence.</param>
    /// <exception cref="ArgumentException"><paramref name="sentence"/> is null, empty or white space.</exception>
    public void Announce(string sentence)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sentence);

        _announcement = sentence;
        _announcementVersion++;
        NotifyPropertyChanged(nameof(StatusAnnouncement));
        NotifyPropertyChanged(nameof(AnnouncementVersion));
    }

    /// <summary>Announces the subagent transitions that were held back by the one-second throttle, if any.</summary>
    public void FlushPending()
    {
        var sentence = _announcer.Flush();
        if (sentence is not null)
        {
            Announce(sentence);
        }
    }

    /// <summary>Sets the workflow phase the GSD pill and the run summary show.</summary>
    /// <param name="phase">The phase, or null when it is unknown.</param>
    public void SetPhase(GsdPhaseInfo? phase)
    {
        if (Equals(_phase, phase))
        {
            return;
        }

        _phase = phase;
        Rebuild();
        NotifyPropertyChanged(nameof(Phase));
    }

    /// <summary>Returns the rolled-up status of one source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The status, or null while the source has no events.</returns>
    public StageStatus? StatusOf(StageSource source) =>
        _snapshot.Blocks.FirstOrDefault(block => block.Source == source)?.RollUpStatus;

    /// <summary>
    /// Builds the body of the run summary sentence from the stage events, for example
    /// <c>Ultrathink: planned. Swarm: 5 of 5 done. Substrate: ok, 2 steering messages recorded. GSD: 46, plan 2 of 5.</c>
    /// Sources without events are left out, and the panel never invents a count.
    /// </summary>
    /// <param name="recordedSteeringMessages">The number of steering messages Substrate recorded, added to the Substrate clause when above zero.</param>
    /// <returns>The sentences, or an empty string when no source has events.</returns>
    public string BuildRunSummary(int recordedSteeringMessages)
    {
        var parts = new List<string>();
        foreach (var (source, clause) in _snapshot.SummaryClauses)
        {
            var text = source == StageSource.Substrate && recordedSteeringMessages > 0
                ? $"{clause}, {recordedSteeringMessages} steering {(recordedSteeringMessages == 1 ? "message" : "messages")} recorded"
                : clause;
            parts.Add($"{PipelineProjection.NameOf(source)}: {text}.");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Returns the command that re-checks the source of a stage. It does nothing when no re-check handler was given.</summary>
    /// <param name="source">The source to re-check.</param>
    /// <returns>The same command instance on every call for one source.</returns>
    public ICommand RetryStageCommand(StageSource source)
    {
        if (_retryCommands.TryGetValue(source, out var command))
        {
            return command;
        }

        command = new AgentPanelCommand(
            _ => RetryStageAsync(source, CancellationToken.None),
            static _ => { },
            _ => CanRetry(source));
        _retryCommands[source] = command;
        return command;
    }

    /// <summary>
    /// Re-checks the source of a stage. A fault of the check is announced as a sentence and never thrown, so a broken plugin cannot take the
    /// panel down.
    /// </summary>
    /// <param name="source">The source to re-check.</param>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>A task that completes when the check ended.</returns>
    public async Task RetryStageAsync(StageSource source, CancellationToken cancellationToken)
    {
        var recheck = _recheck;
        if (recheck is null || !PipelineProjection.IsRecheckable(source))
        {
            return;
        }

        try
        {
            await recheck(source, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // The caller cancelled the check; nothing to say.
        }
        catch (Exception fault) when (fault is not OutOfMemoryException)
        {
            Announce(AgentPanelCopy.RecheckFailed(PipelineProjection.NameOf(source)));
        }
    }

    private bool CanRetry(StageSource source) => _recheck is not null && PipelineProjection.IsRecheckable(source);

    private void Rebuild()
    {
        _snapshot = PipelineProjection.Project(_entries.Values, _recheck is not null, _phase);
        NotifyPropertyChanged(nameof(Blocks));
        NotifyPropertyChanged(nameof(Pills));
        NotifyPropertyChanged(nameof(HasEvents));
        NotifyPropertyChanged(nameof(DegradedStageCount));
        NotifyPropertyChanged(nameof(AllPluginsUnavailable));
        SetProperty(ref _pipelineAccessibleName, BuildAccessibleName(_snapshot), nameof(PipelineAccessibleName));
    }

    private static string BuildAccessibleName(PipelineSnapshot snapshot) =>
        $"{AgentPanelCopy.PipelineAccessibleNamePrefix}{string.Join(", ", snapshot.Pills.Select(pill => pill.Text))}. Open details.";
}
