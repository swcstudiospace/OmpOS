using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.InMemory;

/// <summary>
/// A deterministic script for <see cref="InMemoryOmpAdapter"/>: optional start and handshake
/// failures plus an ordered list of stream steps.
/// </summary>
public sealed record InMemoryScript
{
    /// <summary>The default bounded-channel capacity for one prompt stream.</summary>
    public const int DefaultChannelCapacity = 64;

    /// <summary>Gets the ordered stream steps.</summary>
    public required IReadOnlyList<InMemoryStep> Steps { get; init; }

    /// <summary>Gets the failure raised while starting the engine process, if any.</summary>
    public HarnessException? ProcessStartFailure { get; init; }

    /// <summary>Gets the failure raised during the handshake, if any.</summary>
    public HarnessException? HandshakeFailure { get; init; }

    /// <summary>
    /// Gets the capacity of the bounded event buffer. A script that emits more events than this
    /// before the consumer reads overflows and surfaces <see cref="BackpressureHarnessException"/>.
    /// </summary>
    public int ChannelCapacity { get; init; } = DefaultChannelCapacity;
}
