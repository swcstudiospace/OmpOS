using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OmpOS.Contracts.Agents;
using OmpOS.Harness.AgentLoop.Launch;

namespace OmpOS.Harness.AgentLoop.Stages;

/// <summary>
/// Observes the Ultrathink prompt planner of one session through its session record. Ultrathink writes that record once, when
/// planning has finished, so there is no live progress to report: the <c>plan</c> stage is pending until the record exists and
/// then reports what the plan contains. A record that cannot be read is a degraded stage, never an exception, and the observer
/// keeps polling, because the planner may rewrite the record in place (for example when issue tracking completes). The file is
/// found only through <see cref="SessionStateLayout"/> for the scope's own directory, so one session never reads another's.
/// When the scope has no omp session identifier (an omp terminal session) the observer reads the newest <c>*.json</c> record in
/// the scope's own Ultrathink sessions directory instead of waiting for the identifier.
/// </summary>
public sealed class UltrathinkStateObserver : IStageSource
{
    /// <summary>The identifier of the plan stage.</summary>
    public const string PlanStageId = "plan";

    /// <summary>The identifier of the issue-tracking stage, emitted only when tracking is incomplete or recovered.</summary>
    public const string TrackingStageId = "tracking";

    private const string PlanTitle = "Prompt plan";
    private const string TrackingTitle = "Issue tracking";

    private static readonly Action<ILogger, FileReadStatus, Exception?> PlanFileUnusable =
        LoggerMessage.Define<FileReadStatus>(
            LogLevel.Warning,
            new EventId(1, "UltrathinkPlanFileUnusable"),
            "The Ultrathink plan file could not be used ({Status}); the plan stage is degraded until it can.");

    private readonly UltrathinkStateObserverOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<UltrathinkStateObserver> _logger;

    /// <summary>Creates an observer.</summary>
    /// <param name="options">The options. The defaults when null.</param>
    /// <param name="timeProvider">The clock that stamps events and paces polling. The system clock when null.</param>
    /// <param name="logger">Receives unreadable-file diagnostics. Null discards them.</param>
    public UltrathinkStateObserver(
        UltrathinkStateObserverOptions? options = null,
        TimeProvider? timeProvider = null,
        ILogger<UltrathinkStateObserver>? logger = null)
    {
        _options = options ?? new UltrathinkStateObserverOptions();
        if (_options.PollInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), _options.PollInterval, "The poll interval must be positive.");
        }

        _clock = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<UltrathinkStateObserver>.Instance;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var deduper = new StageEventDeduper(StageSource.Ultrathink, scope.SessionId, _clock);
        if (!_options.PlanningEnabled)
        {
            foreach (var skipped in deduper.Filter([new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Skipped, "Planning is switched off for this run")]))
            {
                yield return skipped;
            }

            yield break;
        }

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
                state.Hash = null;
                ReportUnusable(state, FileReadStatus.Unreadable, ex);
                events = deduper.Filter(Unreadable(state));
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

    private static List<StageSnapshot> Unreadable(PollState state) => Remember(state, [new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Degraded, "Plan file could not be read")]);

    private static List<StageSnapshot> Remember(PollState state, List<StageSnapshot> snapshots)
    {
        state.Last = snapshots;
        return snapshots;
    }

    private static List<StageSnapshot> Project(UltrathinkPlanSummary summary, PollState state)
    {
        var snapshots = new List<StageSnapshot>(2);
        if (summary.IsFallback)
        {
            snapshots.Add(new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Degraded, "Planner fell back to a basic plan: no graph", null, summary.GraphId));
        }
        else
        {
            var detail = string.Create(
                CultureInfo.InvariantCulture,
                $"{summary.NodeCount} nodes, {summary.WaveCount} waves, {summary.ClarificationCount} clarifications");
            snapshots.Add(new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Succeeded, detail, null, summary.GraphId));
        }

        if (summary.TrackingStatus is "partial" or "failed")
        {
            state.TrackingWasDegraded = true;
            snapshots.Add(new StageSnapshot(TrackingStageId, TrackingTitle, StageStatus.Degraded, "Issue tracking status: " + summary.TrackingStatus));
        }
        else if (state.TrackingWasDegraded && string.Equals(summary.TrackingStatus, "complete", StringComparison.Ordinal))
        {
            snapshots.Add(new StageSnapshot(TrackingStageId, TrackingTitle, StageStatus.Succeeded, "Issue tracking status: complete"));
        }

        return snapshots;
    }

    private async Task<List<StageSnapshot>> PollAsync(StageObservationScope scope, PollState state, CancellationToken cancellationToken)
    {
        string path;
        if (scope.AcpSessionId is null)
        {
            // An omp terminal session has no ACP identifier. Its session directory is private, so the newest record in it is its own.
            var newest = NewestRecord(scope.SessionStateDirectory);
            if (newest is null)
            {
                return state.Last ?? [new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Pending, "Waiting for the planner")];
            }

            path = newest;
        }
        else
        {
            try
            {
                path = SessionStateLayout.UltrathinkSessionFile(scope.SessionStateDirectory, scope.AcpSessionId);
            }
            catch (ArgumentException)
            {
                return Unreadable(state);
            }
        }

        var read = await StageFileReader.ReadAllAsync(path, UltrathinkPlanParser.MaxFileBytes, cancellationToken).ConfigureAwait(false);
        switch (read.Status)
        {
            case FileReadStatus.Missing:
                return state.Last ?? [new StageSnapshot(PlanStageId, PlanTitle, StageStatus.Pending, "Waiting for the planner")];
            case FileReadStatus.Ok:
                break;
            default:
                state.Hash = null;
                ReportUnusable(state, read.Status, read.Error);
                return Unreadable(state);
        }

        var hash = SHA256.HashData(read.Content.Span);
        if (state.Last is not null && state.Hash is not null && hash.AsSpan().SequenceEqual(state.Hash))
        {
            return state.Last;
        }

        state.Hash = hash;
        var summary = UltrathinkPlanParser.Parse(read.Content.Span);
        if (summary is null)
        {
            ReportUnusable(state, FileReadStatus.Unreadable, null);
            return Unreadable(state);
        }

        state.LoggedUnusable = false;
        return Remember(state, Project(summary, state));
    }

    private static string? NewestRecord(string sessionStateDirectory)
    {
        string directory;
        try
        {
            directory = Path.Combine(SessionStateLayout.UltrathinkDirectory(sessionStateDirectory), "sessions");
        }
        catch (ArgumentException)
        {
            return null;
        }

        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles("*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(static file => file.LastWriteTimeUtc)
                .ThenByDescending(static file => file.Name, StringComparer.Ordinal)
                .Select(static file => file.FullName)
                .FirstOrDefault();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void ReportUnusable(PollState state, FileReadStatus status, Exception? error)
    {
        if (state.LoggedUnusable)
        {
            return;
        }

        state.LoggedUnusable = true;
        PlanFileUnusable(_logger, status, error);
    }

    private sealed class PollState
    {
        public byte[]? Hash { get; set; }

        public List<StageSnapshot>? Last { get; set; }

        public bool TrackingWasDegraded { get; set; }

        public bool LoggedUnusable { get; set; }
    }
}
