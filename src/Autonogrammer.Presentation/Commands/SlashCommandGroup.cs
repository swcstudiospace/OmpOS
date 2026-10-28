namespace Autonogrammer.Presentation.Commands;

/// <summary>Where Cursor documents a slash command.</summary>
public enum SlashCommandGroup
{
    /// <summary>The CLI reference table, its aliases, and the commands the CLI changelog names.</summary>
    Cli = 0,

    /// <summary>The Agent chat and Agents Window commands.</summary>
    Chat = 1,

    /// <summary>A skill that Cursor ships inside its app.</summary>
    BuiltInSkill = 2,
}
