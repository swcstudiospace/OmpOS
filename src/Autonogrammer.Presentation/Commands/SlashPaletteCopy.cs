using System.Globalization;

namespace Autonogrammer.Presentation.Commands;

/// <summary>Every user-facing sentence of the slash palette. The view and its tests read the same constants, so a copy change is made here and nowhere else.</summary>
public static class SlashPaletteCopy
{
    /// <summary>The badge of a command the omp engine advertised.</summary>
    public const string SourceEngine = "Engine";

    /// <summary>The badge of a command of the Autonogrammer catalogue that Autonogrammer runs or forwards.</summary>
    public const string SourceAutonogrammer = "Autonogrammer";

    /// <summary>The badge of a Cursor command that Autonogrammer lists but cannot run.</summary>
    public const string SourceCursor = "Cursor";

    /// <summary>The badge of a discovered custom command.</summary>
    public const string SourceCommand = "Command";

    /// <summary>The badge of a discovered skill.</summary>
    public const string SourceSkill = "Skill";

    /// <summary>The badge of a discovered subagent.</summary>
    public const string SourceSubagent = "Subagent";

    /// <summary>The tag of a row whose file is in the open workspace.</summary>
    public const string ScopeProject = "project";

    /// <summary>The tag of a row whose file is in the user's home directory.</summary>
    public const string ScopeUser = "user";

    /// <summary>The input hint of a skill row.</summary>
    public const string SkillHint = "[arguments]";

    /// <summary>The input hint of a subagent row.</summary>
    public const string SubagentHint = "<task>";

    /// <summary>Builds the reason a Cursor built-in skill row is disabled when no skill of that name was found.</summary>
    /// <param name="name">The skill name.</param>
    /// <returns>The sentence.</returns>
    public static string NoSkillInstalled(string name) =>
        "Cursor ships this skill inside its app. Add a SKILL.md named " + name + " under .autonogrammer/skills to use it.";

    /// <summary>The reason a native action row is disabled when the window cannot run it.</summary>
    public const string ActionNotInThisWindow = "This window cannot run this command.";

    /// <summary>The accessible name of the list of commands.</summary>
    public const string ListName = "Slash commands";

    /// <summary>The help text of the list: how to move through it and what each key does.</summary>
    public const string ListHelp = "Up and Down choose a command. Enter or Tab puts it in the message. Escape closes the list. Nothing runs until the message is sent.";

    /// <summary>The accessible name of the live region that announces how many commands match.</summary>
    public const string CountName = "Slash command results";

    /// <summary>The accessible name of the live region that says why no command is listed, or why the highlighted command cannot be chosen.</summary>
    public const string MessageName = "Slash command message";

    /// <summary>The announcement when no command matches the typed name.</summary>
    public const string NoCommands = "No commands";

    /// <summary>Builds the announcement of how many commands match the typed name.</summary>
    /// <param name="count">The number of rows the palette lists.</param>
    /// <returns>The sentence the count line and the live region carry.</returns>
    public static string ResultCount(int count) => count switch
    {
        <= 0 => NoCommands,
        1 => "1 command",
        _ => string.Create(CultureInfo.InvariantCulture, $"{count} commands"),
    };

    /// <summary>Builds the accessible name of one row: the command, its input hint, what it does and where it comes from.</summary>
    /// <param name="entry">The row.</param>
    /// <returns>The name a screen reader speaks for the row.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entry"/> is null.</exception>
    public static string EntryName(SlashPaletteEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var parts = new List<string> { entry.DisplayName };
        if (entry.HasInputHint)
        {
            parts.Add(entry.InputHint!);
        }

        if (entry.HasDescription)
        {
            parts.Add(entry.Description);
        }

        parts.Add(entry.SourceLabel);
        if (entry.HasScope)
        {
            parts.Add(entry.ScopeLabel);
        }

        return string.Join(", ", parts);
    }

    /// <summary>Returns the badge text of a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The badge: Engine, Autonogrammer, Cursor, Command, Skill or Subagent.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="source"/> is not defined.</exception>
    public static string SourceBadge(SlashPaletteSource source) => source switch
    {
        SlashPaletteSource.Engine => SourceEngine,
        SlashPaletteSource.Autonogrammer => SourceAutonogrammer,
        SlashPaletteSource.Cursor => SourceCursor,
        SlashPaletteSource.Command => SourceCommand,
        SlashPaletteSource.Skill => SourceSkill,
        SlashPaletteSource.Subagent => SourceSubagent,
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "The source is not defined."),
    };

    /// <summary>Returns the scope tag text.</summary>
    /// <param name="scope">The scope.</param>
    /// <returns><c>project</c> or <c>user</c>; empty for a built-in row.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope"/> is not defined.</exception>
    public static string ScopeTag(SlashScope scope) => scope switch
    {
        SlashScope.BuiltIn => string.Empty,
        SlashScope.Project => ScopeProject,
        SlashScope.User => ScopeUser,
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "The scope is not defined."),
    };
}
