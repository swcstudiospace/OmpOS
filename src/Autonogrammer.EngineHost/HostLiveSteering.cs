using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.EngineHost;

/// <summary>
/// The live steering of an <see cref="InProcessEngineHost"/>. A host binds a new session after a fault, so a client cannot hold a
/// session's own steering; this one stays valid and forwards to whichever session the host has bound. Before a session is bound, and
/// when the bound session cannot take a message into a running turn, steering is unsupported and a steer finds no running turn.
/// </summary>
internal sealed class HostLiveSteering : IOmpLiveSteering
{
    private readonly System.Threading.Lock _gate = new();
    private IOmpLiveSteering? _bound;

    /// <inheritdoc />
    public bool IsSupported => Bound()?.IsSupported ?? false;

    /// <summary>Follows the live steering of <paramref name="session"/>, if it has one. A session already followed changes nothing.</summary>
    /// <param name="session">The session the host bound.</param>
    public void Bind(IOmpSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        lock (_gate)
        {
            _bound = session as IOmpLiveSteering;
        }
    }

    /// <summary>Stops following the bound session. The host calls it when it is disposed.</summary>
    public void Unbind()
    {
        lock (_gate)
        {
            _bound = null;
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
