using Autonogrammer.Harness.Abstractions;

namespace Autonogrammer.Harness.InMemory;

/// <summary>Fails the stream with the given exception after the preceding events.</summary>
/// <param name="Error">The exception to surface to the consumer.</param>
public sealed record FailStep(HarnessException Error) : InMemoryStep;
