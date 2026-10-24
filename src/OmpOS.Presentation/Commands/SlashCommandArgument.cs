namespace OmpOS.Presentation.Commands;

/// <summary>One named argument of a slash command. A command lists its arguments in the order they are typed.</summary>
public sealed record SlashCommandArgument
{
    /// <summary>Gets the argument name. It is unique within its command.</summary>
    public required string Name { get; init; }

    /// <summary>Gets a value indicating whether the command cannot run without this argument.</summary>
    public required bool IsRequired { get; init; }

    /// <summary>Gets the text that tells the user what the argument is for.</summary>
    public required string Description { get; init; }
}
