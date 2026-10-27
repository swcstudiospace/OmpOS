namespace OmpOS.Presentation.Commands;

/// <summary>One command the engine advertises for the session, as the slash palette lists it. The palette lists it and completes its name; it never runs it.</summary>
/// <param name="Name">The command name without the leading slash. A name that is blank or holds white space cannot be typed and is not listed.</param>
/// <param name="Description">What the command does, or <see langword="null"/> when the engine gave none.</param>
/// <param name="InputHint">The hint for the text typed after the name, or <see langword="null"/> when the command takes no input.</param>
public sealed record SlashEngineCommand(string Name, string? Description, string? InputHint);
