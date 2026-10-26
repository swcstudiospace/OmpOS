using System.Runtime.CompilerServices;
using OmpOS.Contracts.Agents;

namespace OmpOS.Harness.AgentLoop.Substrate;

/// <summary>
/// Observes the Substrate for one session and projects it as stage events. The stages are <c>health</c> (Succeeded or
/// Unavailable), <c>brief</c> (Succeeded with section and item counts, or Degraded), <c>lease</c> (from <c>graph_get</c> once the
/// Graph identifier is known: the holder surface and expiry only), and <c>ledger</c> with up to ten recent ledger events as child
/// stages <c>event:&lt;id&gt;</c>. Event titles and details are built only from the time, kind, actor and the declared payload
/// action, item and outcome. The source polls every two seconds. While the health check fails it emits Unavailable once and
/// backs off exponentially up to thirty seconds, then recovers by itself. A stage is emitted again only when it changed.
/// </summary>
public sealed class SubstrateStageObserver : IStageSource
{
    private const int RecentEventLimit = 10;

    private static readonly TimeSpan BriefRefresh = TimeSpan.FromSeconds(30);

    private readonly SubstrateGateway _gateway;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string, string?> _repositoryResolver;

    /// <summary>Creates an observer over a gateway.</summary>
    /// <param name="gateway">The Substrate gateway.</param>
    /// <param name="timeProvider">The clock that stamps stages and times the polling.</param>
    /// <param name="repositoryResolver">Names the repository of a workspace root as <c>owner/name</c>, or null. The git configuration of the workspace is read when null.</param>
    public SubstrateStageObserver(SubstrateGateway gateway, TimeProvider timeProvider, Func<string, string?>? repositoryResolver = null)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _gateway = gateway;
        _timeProvider = timeProvider;
        _repositoryResolver = repositoryResolver ?? WorkspaceRepositoryResolver.Resolve;
    }

    /// <summary>Gets the time between polls while the Substrate is healthy.</summary>
    public static TimeSpan PollInterval => TimeSpan.FromSeconds(2);

    /// <summary>Gets the longest time between polls while the health check keeps failing.</summary>
    public static TimeSpan MaxBackoff => TimeSpan.FromSeconds(30);

    /// <summary>Computes the time to wait after a poll.</summary>
    /// <param name="consecutiveHealthFailures">How many health checks in a row have failed, 0 when the last one succeeded.</param>
    /// <returns><see cref="PollInterval"/> when healthy, otherwise the poll interval doubled for every failure, up to <see cref="MaxBackoff"/>.</returns>
    public static TimeSpan DelayAfter(int consecutiveHealthFailures)
    {
        if (consecutiveHealthFailures <= 0)
        {
            return PollInterval;
        }

        var factor = 1L << Math.Min(consecutiveHealthFailures, 10);
        var delay = TimeSpan.FromTicks(PollInterval.Ticks * factor);
        return delay > MaxBackoff ? MaxBackoff : delay;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var state = new ObservationState();
        while (!cancellationToken.IsCancellationRequested)
        {
            List<StageEvent> batch;
            try
            {
                batch = await PollAsync(scope, state, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                yield break;
            }

            foreach (var stage in batch)
            {
                yield return stage;
            }

            if (!await DelayAsync(DelayAfter(state.HealthFailures), cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }
    }

    private static bool Changed(ObservationState state, StageEvent stage)
    {
        var signature = new StageSignature(stage.Status, stage.Title, stage.Detail);
        if (state.Last.TryGetValue(stage.StageId, out var previous) && previous == signature)
        {
            return false;
        }

        state.Last[stage.StageId] = signature;
        return true;
    }

    private static void Add(List<StageEvent> batch, ObservationState state, StageEvent stage)
    {
        if (Changed(state, stage))
        {
            batch.Add(stage);
        }
    }

    private async Task<bool> DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<List<StageEvent>> PollAsync(StageObservationScope scope, ObservationState state, CancellationToken cancellationToken)
    {
        var batch = new List<StageEvent>();
        var now = _timeProvider.GetUtcNow();
        var context = new SubstrateStageMapper.MappingContext(scope.SessionId, scope.GraphId, now);

        try
        {
            var health = await _gateway.HealthAsync(cancellationToken).ConfigureAwait(false);
            if (state.HealthFailures > 0)
            {
                state.NextBriefAt = null;
            }

            state.HealthFailures = 0;
            Add(batch, state, SubstrateStageMapper.Health(context, health));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var failure = ex as SubstrateUnavailableException ?? new SubstrateUnavailableException("GET /healthz", SubstrateFailureReason.Transport, null);
            state.HealthFailures++;
            Add(batch, state, SubstrateStageMapper.HealthUnavailable(context, failure));
            return batch;
        }

        await PollBriefAsync(scope, state, context, batch, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(scope.GraphId))
        {
            await PollLeaseAsync(scope.GraphId, state, context, batch, cancellationToken).ConfigureAwait(false);
        }

        await PollLedgerAsync(scope, state, context, batch, cancellationToken).ConfigureAwait(false);
        return batch;
    }

    private async Task PollBriefAsync(StageObservationScope scope, ObservationState state, SubstrateStageMapper.MappingContext context, List<StageEvent> batch, CancellationToken cancellationToken)
    {
        if (state.NextBriefAt is { } due && context.Now < due)
        {
            return;
        }

        state.NextBriefAt = context.Now + BriefRefresh;
        if (!state.RepositoryResolved)
        {
            state.Repository = _repositoryResolver(scope.WorkspaceRoot);
            state.RepositoryResolved = true;
        }

        try
        {
            var brief = await _gateway.GetBriefAsync(new SubstrateBriefRequest { Repo = state.Repository, GraphId = scope.GraphId }, cancellationToken).ConfigureAwait(false);
            Add(batch, state, SubstrateStageMapper.Brief(context, brief));
        }
        catch (SubstrateUnavailableException ex)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.BriefStageId, "Substrate brief", "The brief could not be read", ex, null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.BriefStageId, "Substrate brief", "The brief could not be read", null, null));
        }
    }

    private async Task PollLeaseAsync(string graphId, ObservationState state, SubstrateStageMapper.MappingContext context, List<StageEvent> batch, CancellationToken cancellationToken)
    {
        const string What = "The task lease could not be read";
        try
        {
            var result = await _gateway.CallToolAsync("graph_get", SubstrateToolArguments.Create(("graph_id", graphId)), cancellationToken).ConfigureAwait(false);
            var graph = result.IsError || !result.Text.TrimStart().StartsWith('{')
                ? null
                : SubstrateWire.TryParse(result.Text, SubstrateJsonContext.Default.GraphViewDto);
            Add(
                batch,
                state,
                graph is null
                    ? SubstrateStageMapper.LeaseNotRegistered(context)
                    : SubstrateStageMapper.Lease(context, graph));
        }
        catch (SubstrateUnavailableException ex)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.LeaseStageId, "Task lease", What, ex, null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.LeaseStageId, "Task lease", What, null, null));
        }
    }

    private async Task PollLedgerAsync(StageObservationScope scope, ObservationState state, SubstrateStageMapper.MappingContext context, List<StageEvent> batch, CancellationToken cancellationToken)
    {
        // The ledger is only ever read for this session's graph, or for this session when no graph exists yet.
        var query = !string.IsNullOrWhiteSpace(scope.GraphId)
            ? new SubstrateEventQuery { GraphId = scope.GraphId, Limit = RecentEventLimit }
            : string.IsNullOrWhiteSpace(scope.AcpSessionId)
                ? null
                : new SubstrateEventQuery { SessionId = scope.AcpSessionId, Limit = RecentEventLimit };
        if (query is null)
        {
            return;
        }

        const string What = "The event ledger could not be read";
        try
        {
            var page = await _gateway.QueryEventsAsync(query, cancellationToken).ConfigureAwait(false);
            var recent = page.Events.Take(RecentEventLimit).ToList();
            Add(batch, state, SubstrateStageMapper.Ledger(context, new SubstrateEventPage { Events = recent, Truncated = page.Truncated }));
            for (var index = recent.Count - 1; index >= 0; index--)
            {
                var stage = SubstrateStageMapper.Event(context, recent[index]);
                if (stage is not null && state.SeenEvents.Add(stage.StageId))
                {
                    Add(batch, state, stage);
                }
            }
        }
        catch (SubstrateUnavailableException ex)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.LedgerStageId, "Event ledger", What, ex, null));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Add(batch, state, SubstrateStageMapper.PartDegraded(context, SubstrateStageMapper.LedgerStageId, "Event ledger", What, null, null));
        }
    }

    private readonly record struct StageSignature(StageStatus Status, string Title, string? Detail);

    private sealed class ObservationState
    {
        internal Dictionary<string, StageSignature> Last { get; } = new(StringComparer.Ordinal);

        internal HashSet<string> SeenEvents { get; } = new(StringComparer.Ordinal);

        internal int HealthFailures { get; set; }

        internal DateTimeOffset? NextBriefAt { get; set; }

        internal string? Repository { get; set; }

        internal bool RepositoryResolved { get; set; }
    }
}
