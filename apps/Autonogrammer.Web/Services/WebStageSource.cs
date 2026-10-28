using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Web.Services;

/// <summary>
/// The <see cref="IStageSource"/> of one session. <see cref="ObserveAsync"/> first yields the stages <c>GET /sessions/{id}/stages</c> reports
/// (the engine turn and one explained <c>Unavailable</c> stage per plugin source that published nothing), then every stage the owner hands to
/// <see cref="Publish"/> (the user event stream and the session log carry them). An unreachable API ends the observation with a fault, which the
/// session view model shows as a degraded stage; it never ends the agent.
/// </summary>
internal sealed class WebStageSource : IStageSource
{
    private readonly IOmpApiClient _api;
    private readonly string _sessionId;
    private readonly Channel<StageEvent> _live = Channel.CreateUnbounded<StageEvent>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Initializes a new instance of the <see cref="WebStageSource"/> class.</summary>
    /// <param name="api">The API client.</param>
    /// <param name="sessionId">The session whose stages this source reports.</param>
    internal WebStageSource(IOmpApiClient api, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        _api = api;
        _sessionId = sessionId;
    }

    /// <summary>Hands a stage that arrived on a stream to the observation.</summary>
    /// <param name="stage">The stage.</param>
    internal void Publish(StageEvent stage)
    {
        ArgumentNullException.ThrowIfNull(stage);
        _ = _live.Writer.TryWrite(stage);
    }

    /// <summary>Reads the stages again and publishes those of one source, so a plugin row that was unavailable can recover.</summary>
    /// <param name="source">The plugin source to re-check.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that completes when the stages were published.</returns>
    internal async Task RecheckAsync(StageSource source, CancellationToken cancellationToken)
    {
        var response = await _api.GetStagesAsync(_sessionId, cancellationToken).ConfigureAwait(true);
        foreach (var stage in response.Stages.Where(candidate => candidate.Source == source))
        {
            Publish(stage);
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var snapshot = await _api.GetStagesAsync(_sessionId, cancellationToken).ConfigureAwait(false);
        foreach (var stage in snapshot.Stages)
        {
            yield return stage;
        }

        await foreach (var stage in _live.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return stage;
        }
    }
}
