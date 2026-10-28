using System.Globalization;
using System.Runtime.CompilerServices;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Harness.AgentLoop.Gsd;

/// <summary>
/// Projects the workflow phase of the session's workspace as the <c>phase</c> stage of <see cref="StageSource.Gsd"/>. The
/// workflow tool is polled through <see cref="GsdToolsClient"/>, which caches by the planning files' modification time, so a
/// poll that finds nothing new spawns no process. An event is emitted for the first observation and whenever the status or
/// detail changes. A missing tool or a workspace without planning files is an Unavailable stage, a failing query a Degraded
/// one; neither throws.
/// </summary>
public sealed class GsdStateObserver : IStageSource
{
    /// <summary>The stage identifier of the workflow phase stage.</summary>
    public const string PhaseStageId = "phase";

    /// <summary>The title of the workflow phase stage.</summary>
    public const string PhaseStageTitle = "Workflow phase";

    /// <summary>The detail shown when the workspace has no workflow state.</summary>
    public const string NoWorkflowStateDetail = "No workflow state in this workspace";

    /// <summary>The default time between two polls.</summary>
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromSeconds(5);

    private readonly GsdToolsClient _client;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollInterval;

    /// <summary>Initializes a new instance of the <see cref="GsdStateObserver"/> class.</summary>
    /// <param name="client">Reads the workflow state.</param>
    /// <param name="timeProvider">Supplies event timestamps and drives the poll delay.</param>
    /// <param name="pollInterval">The time between polls, or null for <see cref="DefaultPollInterval"/>.</param>
    public GsdStateObserver(GsdToolsClient client, TimeProvider timeProvider, TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var interval = pollInterval ?? DefaultPollInterval;
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(pollInterval), interval, "The poll interval must be positive.");
        }

        _client = client;
        _timeProvider = timeProvider;
        _pollInterval = interval;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(
        StageObservationScope scope,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);

        Observation? last = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var observation = await ObserveOnceAsync(scope, cancellationToken).ConfigureAwait(false);
            if (observation is null)
            {
                yield break;
            }

            if (!observation.Equals(last))
            {
                last = observation;
                yield return new StageEvent
                {
                    SessionId = scope.SessionId,
                    Source = StageSource.Gsd,
                    StageId = PhaseStageId,
                    Title = PhaseStageTitle,
                    Status = observation.Status,
                    Timestamp = _timeProvider.GetUtcNow(),
                    Detail = observation.Detail,
                    GraphId = scope.GraphId,
                };
            }

            if (!await DelayAsync(cancellationToken).ConfigureAwait(false))
            {
                yield break;
            }
        }
    }

    private async Task<Observation?> ObserveOnceAsync(StageObservationScope scope, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(scope.WorkspaceRoot))
        {
            return new Observation(StageStatus.Unavailable, NoWorkflowStateDetail);
        }

        try
        {
            var snapshot = await _client.GetWorkflowAsync(scope.WorkspaceRoot, cancellationToken).ConfigureAwait(false);
            return Describe(snapshot);
        }
        catch (GsdToolsException exception)
        {
            return exception.Failure switch
            {
                GsdToolsFailure.WorkspaceNotInitialized => new Observation(StageStatus.Unavailable, NoWorkflowStateDetail),
                GsdToolsFailure.ToolsNotFound => new Observation(StageStatus.Unavailable, NoWorkflowStateDetail + ": the GSD tools are not installed"),
                _ => new Observation(StageStatus.Degraded, "Workflow state could not be read: " + exception.Message),
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Observation Describe(GsdWorkflowSnapshot snapshot)
    {
        if (snapshot.CurrentPhase is not { } phase)
        {
            return new Observation(StageStatus.Pending, "No phases in the roadmap");
        }

        var detail = string.Create(
            CultureInfo.InvariantCulture,
            $"Phase {phase.Number}: {phase.Name}, {phase.PlansComplete} of {phase.PlansTotal} plans");
        var complete = string.Equals(phase.Status, GsdWorkflowSnapshot.CompleteStatus, StringComparison.Ordinal);
        return new Observation(complete ? StageStatus.Succeeded : StageStatus.Running, detail);
    }

    private async Task<bool> DelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_pollInterval, _timeProvider, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private sealed record Observation(StageStatus Status, string Detail);
}
