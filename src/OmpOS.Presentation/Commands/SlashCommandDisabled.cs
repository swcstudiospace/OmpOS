namespace OmpOS.Presentation.Commands;

/// <summary>The name is a registered command that is disabled.</summary>
/// <param name="Command">The registered command.</param>
/// <param name="Reason">The disabled reason, exactly as the descriptor stores it.</param>
public sealed record SlashCommandDisabled(SlashCommandDescriptor Command, string Reason) : SlashCommandResolution;
