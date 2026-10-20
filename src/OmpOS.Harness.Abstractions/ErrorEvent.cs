namespace OmpOS.Harness.Abstractions;

/// <summary>A failure reported inside the stream without ending the transport.</summary>
/// <param name="Message">The human-readable message.</param>
/// <param name="Retryable">Whether the caller may retry the prompt.</param>
public sealed record ErrorEvent(string Message, bool Retryable) : HarnessEvent;
