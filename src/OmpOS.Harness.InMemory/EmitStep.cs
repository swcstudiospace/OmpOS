using OmpOS.Harness.Abstractions;

namespace OmpOS.Harness.InMemory;

/// <summary>Emits one event into the stream.</summary>
/// <param name="Event">The event to emit.</param>
public sealed record EmitStep(HarnessEvent Event) : InMemoryStep;
