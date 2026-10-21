namespace OmpOS.Harness.Abstractions;

/// <summary>
/// A non-terminal diagnostic raised while a turn runs. It neither ends the stream nor the turn:
/// the turn still ends with its own <see cref="CompletedEvent"/> or <see cref="ErrorEvent"/>.
/// </summary>
/// <param name="Message">The human-readable diagnostic message.</param>
public sealed record DiagnosticEvent(string Message) : HarnessEvent;
