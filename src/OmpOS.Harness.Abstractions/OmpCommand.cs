namespace OmpOS.Harness.Abstractions;

/// <summary>One slash command (or skill) that the engine advertises for a session. The engine owns the command and runs it when a prompt that starts with it is sent.</summary>
/// <param name="Name">The command name without the leading slash, exactly as the engine sends it (for example <c>model</c> or <c>skill:review</c>).</param>
/// <param name="Description">What the command does, as the engine words it, or <see langword="null"/> when the engine gave none.</param>
/// <param name="InputHint">The hint for the argument text that follows the command (for example <c>[on|ultra|off|status]</c>), or <see langword="null"/> when the command takes no input.</param>
public sealed record OmpCommand(string Name, string? Description, string? InputHint);
