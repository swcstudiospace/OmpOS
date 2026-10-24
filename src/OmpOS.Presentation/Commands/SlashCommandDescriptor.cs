namespace OmpOS.Presentation.Commands;

/// <summary>
/// The data that describes one slash command: its name, arguments, availability, and the handler the host dispatches it to.
/// The descriptor holds data only; it runs nothing.
/// </summary>
public sealed record SlashCommandDescriptor
{
    /// <summary>
    /// Gets the command name without the leading slash. The name is one that the slash-command recognizer accepts, so a
    /// user can type it. The registry rejects any other name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>Gets the arguments in the order they are typed.</summary>
    public IReadOnlyList<SlashCommandArgument> Arguments { get; init; } = [];

    /// <summary>Gets the text that tells the user what the command does.</summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the platforms on which the command is available. The registry does not filter by platform; the host applies this
    /// value. The registry refuses a value that holds a bit <see cref="CommandPlatforms"/> does not define.
    /// </summary>
    public required CommandPlatforms Platforms { get; init; }

    /// <summary>Gets the reason the command is disabled, or <see langword="null"/> when it is enabled. The reason is returned unchanged.</summary>
    public string? DisabledReason { get; init; }

    /// <summary>
    /// Gets the identifier of the handler that runs the command. It is an identifier and not a delegate, so the descriptor
    /// stays plain data; a host maps the identifier to its handler.
    /// </summary>
    public required string HandlerId { get; init; }
}
