namespace OmpOS.Harness.Abstractions;

/// <summary>The engine published or updated its plan.</summary>
/// <param name="Markdown">The plan as markdown.</param>
public sealed record PlanEvent(string Markdown) : HarnessEvent;
