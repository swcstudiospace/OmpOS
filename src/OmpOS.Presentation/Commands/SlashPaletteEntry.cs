namespace OmpOS.Presentation.Commands;

/// <summary>One row of the slash palette: a registered command and whether the user can run it.</summary>
/// <param name="Command">The registered command the row stands for.</param>
public sealed record SlashPaletteEntry(SlashCommandDescriptor Command)
{
    /// <summary>Gets the command name without the leading slash.</summary>
    public string Name => Command.Name;

    /// <summary>Gets the text that tells the user what the command does.</summary>
    public string Description => Command.Description;

    /// <summary>Gets a value indicating whether the command can run. A disabled command stays in the list so its reason can be shown.</summary>
    public bool IsEnabled => Command.DisabledReason is null;

    /// <summary>Gets the reason the command is disabled, exactly as the descriptor stores it, or <see langword="null"/> when it is enabled.</summary>
    public string? DisabledReason => Command.DisabledReason;
}
