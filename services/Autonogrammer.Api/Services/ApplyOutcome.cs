namespace Autonogrammer.Api.Services;

/// <summary>The outcome of applying a change decision in the workspace.</summary>
/// <param name="Kind">What happened.</param>
/// <param name="Reason">A plain sentence for every kind other than <see cref="ApplyKind.Applied"/>; it never contains a path outside the reported one.</param>
internal sealed record ApplyOutcome(ApplyKind Kind, string? Reason);
