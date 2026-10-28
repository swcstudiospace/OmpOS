namespace Autonogrammer.Presentation.Hooks;

/// <summary>A quarantined hook source with the reason and the number of its hooks, for the lift-quarantine action.</summary>
/// <param name="Source">The identifier of the source, to pass to <see cref="IHooksPageSource.LiftQuarantineAsync"/>.</param>
/// <param name="Reason">Why the source was quarantined.</param>
/// <param name="HookCount">How many listed hooks belong to the source.</param>
public sealed record HookQuarantineRow(string Source, string Reason, int HookCount);
