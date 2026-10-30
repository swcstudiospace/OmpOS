namespace Autonogrammer.Harness.Abstractions;

/// <summary>An incremental piece of assistant reasoning.</summary>
/// <param name="Text">The thought text.</param>
public sealed record ThoughtEvent(string Text) : HarnessEvent;
