using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Autonogrammer.Contracts.Agents;

namespace Autonogrammer.Native.Agents;

/// <summary>
/// The stage source that carries the events a re-check publishes. A re-check looks at a plugin again and reports what it found; the
/// source never completes on its own, because a user may ask again at any time, and it ends when its observation is cancelled.
/// </summary>
internal sealed class RecheckStageSource : IStageSource
{
    private readonly Channel<StageEvent> _events = Channel.CreateUnbounded<StageEvent>(new UnboundedChannelOptions { SingleReader = true });

    /// <summary>Publishes an event to the observation of this source. It never blocks.</summary>
    /// <param name="stageEvent">The event.</param>
    /// <exception cref="ArgumentNullException"><paramref name="stageEvent"/> is null.</exception>
    public void Publish(StageEvent stageEvent)
    {
        ArgumentNullException.ThrowIfNull(stageEvent);
        _events.Writer.TryWrite(stageEvent);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var reader = _events.Reader;
        while (!cancellationToken.IsCancellationRequested)
        {
            bool available;
            try
            {
                available = await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                available = false;
            }

            if (!available)
            {
                yield break;
            }

            while (reader.TryRead(out var stageEvent))
            {
                yield return stageEvent;
            }
        }
    }
}
