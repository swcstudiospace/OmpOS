namespace OmpOS.Presentation.Commands;

/// <summary>A slash command that the user typed, split into its name and the text that follows it.</summary>
/// <param name="Name">The command name without the leading slash.</param>
/// <param name="ArgumentText">The text after the name, exactly as the recognizer returns it. It is empty when nothing follows.</param>
public sealed record SlashCommandInvocation(string Name, string ArgumentText);
