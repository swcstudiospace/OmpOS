namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The gate's decision.</summary>
/// <param name="Outcome">Allow, ask or deny.</param>
/// <param name="MustBeLocal">Whether only a decision on this machine satisfies an ask.</param>
/// <param name="Reason">A short reason, or null.</param>
public sealed record CoworkPolicyDecision(CoworkPolicyOutcome Outcome, bool MustBeLocal, string? Reason);
