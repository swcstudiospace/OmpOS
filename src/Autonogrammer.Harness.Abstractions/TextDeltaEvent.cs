namespace Autonogrammer.Harness.Abstractions;

/// <summary>An incremental piece of assistant text.</summary>
/// <param name="Text">The text delta.</param>
public sealed record TextDeltaEvent(string Text) : HarnessEvent;
