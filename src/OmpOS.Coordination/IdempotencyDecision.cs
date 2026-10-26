namespace OmpOS.Coordination;

/// <summary>The result of claiming an idempotency key.</summary>
/// <param name="Outcome">What the claim found.</param>
/// <param name="ResponseJson">The stored response when <paramref name="Outcome"/> is <see cref="IdempotencyOutcome.Replay"/>. Otherwise <see langword="null"/>.</param>
public sealed record IdempotencyDecision(IdempotencyOutcome Outcome, string? ResponseJson);
