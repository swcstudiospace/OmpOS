namespace Autonogrammer.Cowork.Abstractions;

/// <summary>The answer to an ask.</summary>
/// <param name="Outcome">How it was answered.</param>
public sealed record CoworkApprovalDecision(CoworkApprovalOutcome Outcome);
