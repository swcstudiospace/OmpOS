namespace OmpOS.Presentation.Commands;

/// <summary>The name is a registered command that is enabled.</summary>
/// <param name="Command">The registered command.</param>
public sealed record SlashCommandAvailable(SlashCommandDescriptor Command) : SlashCommandResolution;
