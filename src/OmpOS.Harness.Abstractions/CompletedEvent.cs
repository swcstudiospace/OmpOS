namespace OmpOS.Harness.Abstractions;

/// <summary>The turn finished; the stream is complete.</summary>
/// <param name="Reason">The stop reason, for example <c>end_turn</c> or <c>cancelled</c>.</param>
public sealed record CompletedEvent(string Reason) : HarnessEvent;
