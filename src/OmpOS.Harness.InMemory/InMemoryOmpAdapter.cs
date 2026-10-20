using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.InMemory;

/// <summary>
/// Deterministic, script-driven stand-in for an omp engine. Each session created from the
/// adapter replays the same <see cref="InMemoryScript"/> independently.
/// </summary>
public sealed class InMemoryOmpAdapter
{
    private readonly InMemoryScript _script;

    /// <summary>Initializes a new instance of the <see cref="InMemoryOmpAdapter"/> class.</summary>
    /// <param name="script">The script every session replays.</param>
    /// <exception cref="ArgumentOutOfRangeException">The script's channel capacity is below 1.</exception>
    public InMemoryOmpAdapter(InMemoryScript script)
    {
        ArgumentNullException.ThrowIfNull(script);
        if (script.ChannelCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(script), script.ChannelCapacity, "ChannelCapacity must be at least 1.");
        }

        _script = script;
    }

    /// <summary>Creates a new, not-yet-started session.</summary>
    /// <returns>A session in <see cref="HarnessState.Disconnected"/>.</returns>
    public IOmpSession CreateSession() => new InMemoryOmpSession(_script);
}
