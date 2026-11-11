namespace Autonogrammer.Cowork.Memory;

/// <summary>A durable outcome, optionally carrying a successful or refused mutation.</summary>
/// <param name="Status">The outcome.</param>
/// <param name="Mutation">The entry or refusal, when applicable.</param>
public sealed record MemoryIdempotentResult(MemoryIdempotentStatus Status, MemoryMutation? Mutation);
