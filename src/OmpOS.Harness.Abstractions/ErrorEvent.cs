namespace OmpOS.Harness.Abstractions;

/// <summary>
/// The terminal event of a failed turn. It is the turn's only terminal event: it ends the stream, and no
/// <see cref="CompletedEvent"/> follows it. The transport stays open.
/// </summary>
/// <param name="Message">The human-readable message.</param>
/// <param name="Retryable">Whether the caller may retry the prompt.</param>
public sealed record ErrorEvent(string Message, bool Retryable) : HarnessEvent;
