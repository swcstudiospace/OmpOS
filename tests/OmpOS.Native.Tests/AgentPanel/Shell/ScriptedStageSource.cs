using System.Runtime.CompilerServices;
using System.Threading.Channels;
using OmpOS.Contracts.Agents;

namespace OmpOS.Native.Tests.AgentPanel.Shell;

/// <summary>The stage source of a session under test: it yields the events the test emits, in order, until it is cancelled.</summary>
internal sealed class ScriptedStageSource : IStageSource
{
    private readonly Channel<StageEvent> _events = Channel.CreateUnbounded<StageEvent>();

    /// <summary>Gets the scope the observation was started with, or null before it started.</summary>
    public StageObservationScope? ObservedScope { get; private set; }

    /// <summary>Queues one stage event for the session.</summary>
    /// <param name="stageEvent">The event.</param>
    public void Emit(StageEvent stageEvent) => _events.Writer.TryWrite(stageEvent);

    /// <summary>Ends the observation with a fault, as a source whose files cannot be read does.</summary>
    /// <param name="fault">The fault the observer's enumeration throws.</param>
    public void Fault(Exception fault) => _events.Writer.TryComplete(fault);

    /// <inheritdoc />
    public async IAsyncEnumerable<StageEvent> ObserveAsync(StageObservationScope scope, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ObservedScope = scope;
        await foreach (var stageEvent in _events.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(true))
        {
            yield return stageEvent;
        }
    }
}
