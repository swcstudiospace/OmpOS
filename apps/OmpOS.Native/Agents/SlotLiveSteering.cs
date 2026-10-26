using OmpOS.Harness.Abstractions;

namespace OmpOS.Native.Agents;

/// <summary>
/// The live steering of one session slot. A slot composes its engine lazily and again after the session is closed and reopened, so the
/// runtime cannot hold the steering of one engine: this one stays valid and forwards to the engine the slot holds now. With no engine
/// steering is unsupported and a steer finds no running turn.
/// </summary>
internal sealed class SlotLiveSteering : IOmpLiveSteering
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpLiveSteering? _bound;

    /// <inheritdoc />
    public bool IsSupported => Bound()?.IsSupported ?? false;

    /// <summary>Follows the live steering of the engine the slot holds now, or none.</summary>
    /// <param name="steering">The steering of the slot's engine host, or null when the slot has no engine.</param>
    public void Bind(IOmpLiveSteering? steering)
    {
        lock (_gate)
        {
            _bound = steering;
        }
    }

    /// <inheritdoc />
    public Task<OmpSteerResult> SteerAsync(string text, OmpSteerLane queue, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(text);

        return Bound() is { } steering
            ? steering.SteerAsync(text, queue, cancellationToken)
            : Task.FromResult(new OmpSteerResult(OmpSteerOutcome.NotRunning, queue, "No turn is running."));
    }

    private IOmpLiveSteering? Bound()
    {
        lock (_gate)
        {
            return _bound;
        }
    }
}
